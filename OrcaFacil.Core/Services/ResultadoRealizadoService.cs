using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Services;

/// <summary>
/// Único caminho de gravação da renda variável: lança, edita e exclui a operação e,
/// na mesma transação, recalcula a sequência do ativo e o lançamento do resultado.
/// Lucro vira receita do mês e prejuízo vira despesa, com base no Valor da Operação.
/// Sem total informado, operação nova ou com quantidade ou preço alterados grava
/// quantidade × preço. Edição que não mexe nesses dois campos preserva o total gravado.
/// A regra fiscal vem da tabela vigente na data da venda. Alíquota não entra na conta.
/// </summary>
public class ResultadoRealizadoService
{
    public const string CategoriaGanho = LancamentoResultadoAtivo.CategoriaGanho;
    public const string CategoriaPerda = LancamentoResultadoAtivo.CategoriaPerda;

    public const string MensagemHashRepetido = "Já existe uma operação com este hash de importação.";

    private const string ObservacaoLancamento = "Gerado pela venda do ativo.";

    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ResultadoRealizadoService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public Task<OperacaoAtivo> SalvarAsync(
        OperacaoAtivoFormModel modelo,
        int membroFamiliaId,
        CancellationToken cancellationToken = default)
        => EmTransacaoAsync(
            (context, ct) => SalvarNoContextoAsync(context, modelo, membroFamiliaId, ct),
            cancellationToken);

    /// <summary>
    /// Grava na transação que o chamador já abriu. Não confirma nem desfaz.
    /// A importação do extrato usa isto para gravar várias linhas de uma vez.
    /// </summary>
    public async Task<OperacaoAtivo> SalvarNoContextoAsync(
        AppDbContext context,
        OperacaoAtivoFormModel modelo,
        int membroFamiliaId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(modelo);
        ValidarCampos(modelo);

        if (!await context.MembrosFamilia.AnyAsync(m => m.Id == membroFamiliaId, cancellationToken))
            throw new InvalidOperationException("Perfil não encontrado.");

        var ativo = await context.AtivosFinanceiros
            .FirstOrDefaultAsync(a => a.Id == modelo.AtivoId, cancellationToken)
            ?? throw new InvalidOperationException("Ativo não encontrado.");

        OperacaoAtivo? existente = null;
        if (modelo.Id is int id)
        {
            existente = await context.OperacoesAtivo
                .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
                ?? throw new InvalidOperationException("Operação não encontrada.");

            if (existente.MembroFamiliaId != membroFamiliaId)
                throw new InvalidOperationException("Operação não encontrada.");
        }
        else if (!ativo.Ativo)
        {
            throw new InvalidOperationException("Este ativo está inativo e não aceita novas operações.");
        }

        if (existente is not null && existente.AtivoId != modelo.AtivoId && !ativo.Ativo)
            throw new InvalidOperationException("Este ativo está inativo e não aceita novas operações.");

        var hash = existente?.HashImportacao ?? NormalizarHash(modelo.HashImportacao);
        if (existente is null && hash is not null)
            await GarantirHashLivreAsync(context, hash, cancellationToken);

        var ativoIds = new HashSet<int> { modelo.AtivoId };
        if (existente is not null)
            ativoIds.Add(existente.AtivoId);

        var operacoes = await context.OperacoesAtivo
            .Where(o => o.MembroFamiliaId == membroFamiliaId && ativoIds.Contains(o.AtivoId))
            .ToListAsync(cancellationToken);

        OperacaoAtivo alvo;
        if (existente is null)
        {
            alvo = new OperacaoAtivo
            {
                MembroFamiliaId = membroFamiliaId,
                Origem = modelo.Origem,
                HashImportacao = hash
            };
            CopiarCampos(alvo, modelo, nova: true);
            operacoes.Add(alvo);
        }
        else
        {
            alvo = operacoes.First(o => o.Id == existente.Id);
            CopiarCampos(alvo, modelo, nova: false);
            alvo.AtualizadoEm = DateTime.UtcNow;
        }

        var idsDosAtivos = operacoes.Select(o => o.AtivoId).Distinct().ToList();
        var ativos = await context.AtivosFinanceiros
            .Where(a => idsDosAtivos.Contains(a.Id))
            .ToListAsync(cancellationToken);
        if (ativos.All(a => a.Id != ativo.Id))
            ativos.Add(ativo);

        var regras = new TabelaRegrasFiscais(
            await context.RegrasFiscais.AsNoTracking().ToListAsync(cancellationToken));
        var periodos = await CarregarFechadosAsync(context, cancellationToken);
        var planos = new List<PlanoAtivo>();

        foreach (var grupo in operacoes.GroupBy(o => o.AtivoId))
        {
            var doAtivo = ativos.First(a => a.Id == grupo.Key);
            var posicao = CalculadoraResultadoRealizado.Calcular(
                grupo.Select(ParaMovimento),
                data => regras.Para(data, doAtivo.Tipo));
            var lancamentos = await CarregarLancamentosAsync(context, grupo, cancellationToken);
            planos.Add(new PlanoAtivo(doAtivo, grupo.ToList(), posicao, lancamentos));
        }

        await GarantirMesAbertoAsync(context, planos, periodos, cancellationToken);

        if (existente is null)
            context.OperacoesAtivo.Add(alvo);

        foreach (var plano in planos)
            await SincronizarLancamentosAsync(context, plano, membroFamiliaId, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EhHashRepetido(ex))
        {
            throw new InvalidOperationException(MensagemHashRepetido, ex);
        }

        return alvo;
    }

    private async Task<T> EmTransacaoAsync<T>(
        Func<AppDbContext, CancellationToken, Task<T>> acao,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transacao = await context.Database.BeginTransactionAsync(cancellationToken);
        var resultado = await acao(context, cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return resultado;
    }

    public async Task ExcluirAsync(int id, int membroFamiliaId, CancellationToken cancellationToken = default)
    {
        if (membroFamiliaId <= 0)
            throw new InvalidOperationException("Perfil não encontrado.");

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transacao = await context.Database.BeginTransactionAsync(cancellationToken);

        if (!await context.MembrosFamilia.AnyAsync(m => m.Id == membroFamiliaId, cancellationToken))
            throw new InvalidOperationException("Perfil não encontrado.");

        var operacao = await context.OperacoesAtivo.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Operação não encontrada.");

        if (operacao.MembroFamiliaId != membroFamiliaId)
            throw new InvalidOperationException("Operação não encontrada.");

        var ativo = await context.AtivosFinanceiros.FirstAsync(a => a.Id == operacao.AtivoId, cancellationToken);
        var operacoes = await context.OperacoesAtivo
            .Where(o => o.AtivoId == ativo.Id && o.MembroFamiliaId == membroFamiliaId)
            .ToListAsync(cancellationToken);
        operacoes.Remove(operacao);

        var regras = new TabelaRegrasFiscais(
            await context.RegrasFiscais.AsNoTracking().ToListAsync(cancellationToken));
        var periodos = await CarregarFechadosAsync(context, cancellationToken);
        var posicao = CalculadoraResultadoRealizado.Calcular(
            operacoes.Select(ParaMovimento),
            data => regras.Para(data, ativo.Tipo));
        var lancamentos = await CarregarLancamentosAsync(context, operacoes.Append(operacao), cancellationToken);
        var plano = new PlanoAtivo(ativo, operacoes, posicao, lancamentos);

        await GarantirMesAbertoAsync(context, [plano], periodos, cancellationToken);
        await SincronizarLancamentosAsync(context, plano, membroFamiliaId, cancellationToken);
        context.OperacoesAtivo.Remove(operacao);

        await context.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
    }

    private static async Task GarantirMesAbertoAsync(
        AppDbContext context,
        IReadOnlyList<PlanoAtivo> planos,
        HashSet<(int Mes, int Ano)> periodos,
        CancellationToken cancellationToken)
    {
        var afetados = new List<string>();
        foreach (var plano in planos)
        {
            var resultados = plano.Posicao.Vendas.ToDictionary(v => v.Id);
            foreach (var lancamento in plano.Lancamentos)
            {
                var opId = lancamento.OperacaoAtivoId!.Value;
                resultados.TryGetValue(opId, out var resultado);
                var novo = resultado?.Resultado ?? 0m;
                var data = resultado?.Data ?? lancamento.Data;
                if (!await AlterariaAsync(context, lancamento, novo, data, plano.Ativo.Ticker, cancellationToken))
                    continue;

                if (PeriodoFechado(periodos, lancamento.Data) || PeriodoFechado(periodos, data))
                    afetados.Add($"{plano.Ativo.Ticker} em {lancamento.Data:dd/MM/yyyy}");
            }

            foreach (var resultado in plano.Posicao.Vendas)
            {
                if (resultado.Resultado == 0m)
                    continue;
                if (plano.Lancamentos.Any(t => t.OperacaoAtivoId == resultado.Id))
                    continue;
                if (PeriodoFechado(periodos, resultado.Data))
                    afetados.Add($"{plano.Ativo.Ticker} em {resultado.Data:dd/MM/yyyy}");
            }
        }

        if (afetados.Count == 0)
            return;

        throw new InvalidOperationException(
            "Mês fechado. A gravação alteraria o lançamento de "
            + string.Join("; ", afetados.Distinct(StringComparer.Ordinal))
            + ". Nada foi gravado.");
    }

    private static async Task<bool> AlterariaAsync(
        AppDbContext context,
        Transacao lancamento,
        decimal resultado,
        DateTime data,
        string ticker,
        CancellationToken cancellationToken)
    {
        if (resultado == 0m)
            return true;

        var tipo = resultado > 0m ? TipoTransacao.Receita : TipoTransacao.Despesa;
        var categoria = await LancamentoResultadoAtivo.ObterOuCriarCategoriaAsync(context, tipo, cancellationToken);
        return !LancamentoResultadoAtivo.Igual(
            lancamento,
            Math.Abs(resultado),
            data,
            tipo,
            categoria,
            MontarDescricao(tipo, ticker),
            ObservacaoLancamento);
    }

    private static async Task SincronizarLancamentosAsync(
        AppDbContext context,
        PlanoAtivo plano,
        int membroFamiliaId,
        CancellationToken cancellationToken)
    {
        var porOperacao = plano.Lancamentos
            .Where(t => t.OperacaoAtivoId is not null)
            .ToDictionary(t => t.OperacaoAtivoId!.Value);
        var operacaoPorId = plano.Operacoes.Where(o => o.Id != 0).ToDictionary(o => o.Id);
        var nova = plano.Operacoes.FirstOrDefault(o => o.Id == 0);
        if (nova is not null)
            operacaoPorId[0] = nova;

        foreach (var resultado in plano.Posicao.Vendas)
        {
            porOperacao.TryGetValue(resultado.Id, out var lancamento);
            if (resultado.Resultado == 0m)
                continue;

            var tipo = resultado.Resultado > 0m ? TipoTransacao.Receita : TipoTransacao.Despesa;
            var categoria = await LancamentoResultadoAtivo.ObterOuCriarCategoriaAsync(context, tipo, cancellationToken);
            var valor = Math.Abs(resultado.Resultado);
            var data = resultado.Data.Date;
            var descricao = MontarDescricao(tipo, plano.Ativo.Ticker);

            if (lancamento is null)
            {
                var operacao = operacaoPorId[resultado.Id];
                var criado = LancamentoResultadoAtivo.Criar(
                    membroFamiliaId, valor, data, tipo, categoria, descricao, ObservacaoLancamento);

                if (operacao.Id == 0)
                    criado.OperacaoAtivo = operacao;
                else
                    criado.OperacaoAtivoId = operacao.Id;

                context.Transacoes.Add(criado);
                continue;
            }

            if (LancamentoResultadoAtivo.Igual(lancamento, valor, data, tipo, categoria, descricao, ObservacaoLancamento))
                continue;

            LancamentoResultadoAtivo.Aplicar(lancamento, valor, data, tipo, categoria, descricao, ObservacaoLancamento);
        }

        var idsComResultado = plano.Posicao.Vendas.Where(v => v.Resultado != 0m).Select(v => v.Id).ToHashSet();
        foreach (var lancamento in plano.Lancamentos)
        {
            if (lancamento.OperacaoAtivoId is int opId && !idsComResultado.Contains(opId))
                context.Transacoes.Remove(lancamento);
        }
    }

    private static async Task<List<Transacao>> CarregarLancamentosAsync(
        AppDbContext context,
        IEnumerable<OperacaoAtivo> operacoes,
        CancellationToken cancellationToken)
    {
        var ids = operacoes.Where(o => o.Id != 0).Select(o => o.Id).ToList();
        if (ids.Count == 0)
            return [];

        return await context.Transacoes
            .Where(t => t.OperacaoAtivoId != null && ids.Contains(t.OperacaoAtivoId.Value))
            .ToListAsync(cancellationToken);
    }

    private static async Task<HashSet<(int Mes, int Ano)>> CarregarFechadosAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var fechados = await context.FechamentosPeriodo
            .Where(f => f.Fechado)
            .Select(f => new { f.Mes, f.Ano })
            .ToListAsync(cancellationToken);

        return fechados.Select(f => (f.Mes, f.Ano)).ToHashSet();
    }

    private static async Task GarantirHashLivreAsync(
        AppDbContext context,
        string hash,
        CancellationToken cancellationToken)
    {
        var repetido = await context.OperacoesAtivo.AnyAsync(o => o.HashImportacao == hash, cancellationToken);
        if (repetido)
            throw new InvalidOperationException(MensagemHashRepetido);
    }

    private static void CopiarCampos(OperacaoAtivo destino, OperacaoAtivoFormModel modelo, bool nova)
    {
        var valor = ResolverValorOperacao(destino, modelo, nova);
        destino.AtivoId = modelo.AtivoId;
        destino.Tipo = modelo.Tipo;
        destino.Data = modelo.Data.Date;
        destino.Quantidade = modelo.Quantidade;
        destino.PrecoUnitario = modelo.PrecoUnitario;
        destino.ValorOperacao = valor;
        destino.Custos = modelo.Custos;
        destino.Corretora = NormalizarCorretora(modelo.Corretora);
    }

    private static MovimentoPosicao ParaMovimento(OperacaoAtivo operacao) => new()
    {
        Id = operacao.Id,
        Data = operacao.Data,
        Tipo = operacao.Tipo,
        Quantidade = operacao.Quantidade,
        PrecoUnitario = operacao.PrecoUnitario,
        ValorOperacao = operacao.ValorOperacao,
        Custos = operacao.Custos
    };

    private static string MontarDescricao(TipoTransacao tipo, string ticker)
    {
        var prefixo = tipo == TipoTransacao.Receita
            ? "Ganho realizado em ativos — "
            : "Perda realizada em ativos — ";
        var texto = prefixo + ticker;
        return texto.Length <= 120 ? texto : texto[..120];
    }

    private static void ValidarCampos(OperacaoAtivoFormModel modelo)
    {
        if (modelo.AtivoId <= 0)
            throw new InvalidOperationException("Selecione um ativo.");
        if (modelo.Quantidade <= 0)
            throw new InvalidOperationException("A quantidade deve ser maior que zero.");
        if (modelo.PrecoUnitario <= 0)
            throw new InvalidOperationException("O preço deve ser maior que zero.");
        if (modelo.ValorOperacao is <= 0)
            throw new InvalidOperationException("O valor da operação deve ser maior que zero.");
        if (modelo.Custos < 0)
            throw new InvalidOperationException("Os custos não podem ser negativos.");
        if (modelo.Corretora?.Trim().Length > 40)
            throw new InvalidOperationException("A corretora deve ter no máximo 40 caracteres.");

        var hash = NormalizarHash(modelo.HashImportacao);
        if (hash is not null && hash.Length > 64)
            throw new InvalidOperationException("O hash de importação deve ter no máximo 64 caracteres.");
    }

    private static decimal ResolverValorOperacao(OperacaoAtivo destino, OperacaoAtivoFormModel modelo, bool nova)
    {
        if (modelo.ValorOperacao is decimal informado)
            return informado;

        if (!nova
            && destino.Quantidade == modelo.Quantidade
            && destino.PrecoUnitario == modelo.PrecoUnitario)
            return destino.ValorOperacao;

        return modelo.Quantidade * modelo.PrecoUnitario;
    }

    private static string? NormalizarHash(string? hash)
        => string.IsNullOrWhiteSpace(hash) ? null : hash.Trim();

    private static string? NormalizarCorretora(string? corretora)
        => string.IsNullOrWhiteSpace(corretora) ? null : corretora.Trim();

    private static bool EhHashRepetido(DbUpdateException ex)
    {
        var texto = ex.InnerException?.Message ?? ex.Message;
        return texto.Contains("HashImportacao", StringComparison.Ordinal);
    }

    private static bool PeriodoFechado(HashSet<(int Mes, int Ano)> periodos, DateTime data)
        => periodos.Contains((data.Month, data.Year));

    private sealed record PlanoAtivo(
        AtivoFinanceiro Ativo,
        IReadOnlyList<OperacaoAtivo> Operacoes,
        PosicaoComResultado Posicao,
        IReadOnlyList<Transacao> Lancamentos);
}
