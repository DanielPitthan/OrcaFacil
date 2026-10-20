using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.RendaFixa;

namespace OrcaFacil.Core.Services;

/// <summary>
/// Único caminho de gravação da renda fixa: lança, edita e exclui a operação e,
/// na mesma transação, recalcula a sequência do título e o lançamento do resultado.
/// Lucro vira receita do mês e prejuízo vira despesa. O IR é o valor informado;
/// alíquota não fica no código.
/// </summary>
public class RendaFixaService
{
    public const string CategoriaGanho = "Ganho realizado em ativos";
    public const string CategoriaPerda = "Perda realizada em ativos";

    public const string MensagemHashRepetido = "Já existe uma operação com este hash de importação.";
    public const string MensagemTituloInativo = "Este título está inativo e não aceita novas operações.";

    private const string ObservacaoLancamento = "Gerado pelo resgate de renda fixa.";

    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public RendaFixaService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PosicaoRendaFixaCalculada> ObterPosicaoAsync(int tituloId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var titulo = await context.TitulosRendaFixa.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tituloId, cancellationToken)
            ?? throw new InvalidOperationException("Título não encontrado.");

        var operacoes = await context.OperacoesRendaFixa.AsNoTracking()
            .Where(o => o.TituloRendaFixaId == tituloId)
            .ToListAsync(cancellationToken);

        return CalculadoraRendaFixa.Calcular(operacoes.Select(o => ParaMovimento(o, titulo.Codigo)));
    }

    public Task<OperacaoRendaFixa> SalvarAsync(
        OperacaoRendaFixaFormModel modelo,
        int membroFamiliaId,
        CancellationToken cancellationToken = default)
        => EmTransacaoAsync(
            (context, ct) => SalvarNoContextoAsync(context, modelo, membroFamiliaId, ct),
            cancellationToken);

    /// <summary>
    /// Grava na transação que o chamador já abriu. Não confirma nem desfaz.
    /// A importação do extrato usa isto para gravar várias linhas de uma vez.
    /// </summary>
    public async Task<OperacaoRendaFixa> SalvarNoContextoAsync(
        AppDbContext context,
        OperacaoRendaFixaFormModel modelo,
        int membroFamiliaId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(modelo);
        ValidarCampos(modelo);

        var titulo = await context.TitulosRendaFixa
            .FirstOrDefaultAsync(t => t.Id == modelo.TituloRendaFixaId, cancellationToken)
            ?? throw new InvalidOperationException("Título não encontrado.");

        if (!await context.MembrosFamilia.AnyAsync(m => m.Id == membroFamiliaId, cancellationToken))
            throw new InvalidOperationException("Perfil não encontrado.");

        var operacoes = await context.OperacoesRendaFixa
            .Where(o => o.TituloRendaFixaId == titulo.Id)
            .ToListAsync(cancellationToken);

        OperacaoRendaFixa? existente = null;
        if (modelo.Id is int id)
        {
            existente = operacoes.FirstOrDefault(o => o.Id == id);
            if (existente is null)
            {
                var emOutroTitulo = await context.OperacoesRendaFixa.AnyAsync(o => o.Id == id, cancellationToken);
                throw new InvalidOperationException(emOutroTitulo
                    ? "A operação permanece no título em que foi lançada."
                    : "Operação não encontrada.");
            }
        }
        else if (!titulo.Ativo)
        {
            throw new InvalidOperationException(MensagemTituloInativo);
        }

        var periodos = await CarregarFechadosAsync(context, cancellationToken);
        var lancamentos = await CarregarLancamentosAsync(context, operacoes, cancellationToken);

        if (existente is not null)
            GarantirOperacaoEditavel(lancamentos, existente.Id, periodos);

        var hash = existente?.HashImportacao ?? NormalizarHash(modelo.HashImportacao);
        if (existente is null && hash is not null)
            await GarantirHashLivreAsync(context, hash, ignorarId: 0, cancellationToken);

        OperacaoRendaFixa alvo;
        if (existente is null)
        {
            alvo = new OperacaoRendaFixa
            {
                TituloRendaFixaId = titulo.Id,
                Origem = modelo.Origem,
                HashImportacao = hash
            };
            CopiarCampos(alvo, modelo);
            operacoes.Add(alvo);
        }
        else
        {
            alvo = existente;
            CopiarCampos(alvo, modelo);
            alvo.AtualizadoEm = DateTime.UtcNow;
        }

        var posicao = CalculadoraRendaFixa.Calcular(operacoes.Select(o => ParaMovimento(o, titulo.Codigo)));
        GarantirMesAberto(titulo.Codigo, posicao, lancamentos, periodos);

        if (existente is null)
            context.OperacoesRendaFixa.Add(alvo);

        await SincronizarLancamentosAsync(context, titulo, operacoes, posicao, lancamentos, membroFamiliaId, cancellationToken);

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

        var operacao = await context.OperacoesRendaFixa.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Operação não encontrada.");

        var titulo = await context.TitulosRendaFixa.FirstAsync(t => t.Id == operacao.TituloRendaFixaId, cancellationToken);
        var operacoes = await context.OperacoesRendaFixa
            .Where(o => o.TituloRendaFixaId == titulo.Id)
            .ToListAsync(cancellationToken);

        var periodos = await CarregarFechadosAsync(context, cancellationToken);
        var lancamentos = await CarregarLancamentosAsync(context, operacoes, cancellationToken);
        GarantirOperacaoEditavel(lancamentos, operacao.Id, periodos);

        operacoes.Remove(operacao);
        var posicao = CalculadoraRendaFixa.Calcular(operacoes.Select(o => ParaMovimento(o, titulo.Codigo)));
        GarantirMesAberto(titulo.Codigo, posicao, lancamentos, periodos);

        await SincronizarLancamentosAsync(context, titulo, operacoes, posicao, lancamentos, membroFamiliaId, cancellationToken);
        context.OperacoesRendaFixa.Remove(operacao);

        await context.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
    }

    /// <summary>Apaga o título, as operações e os lançamentos. Recusa se algum lançamento está em mês fechado.</summary>
    public async Task ExcluirTituloAsync(int tituloId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transacao = await context.Database.BeginTransactionAsync(cancellationToken);

        var titulo = await context.TitulosRendaFixa.FirstOrDefaultAsync(t => t.Id == tituloId, cancellationToken)
            ?? throw new InvalidOperationException("Título não encontrado.");

        var operacoes = await context.OperacoesRendaFixa
            .Where(o => o.TituloRendaFixaId == tituloId)
            .ToListAsync(cancellationToken);
        var lancamentos = await CarregarLancamentosAsync(context, operacoes, cancellationToken);
        var periodos = await CarregarFechadosAsync(context, cancellationToken);

        var bloqueados = lancamentos.Where(l => PeriodoFechado(periodos, l.Data)).ToList();
        if (bloqueados.Count > 0)
        {
            var lista = string.Join("; ", bloqueados.Select(l => $"{titulo.Codigo} em {l.Data:dd/MM/yyyy}"));
            throw new InvalidOperationException(
                $"Mês fechado. O título tem resultado já lançado: {lista}. Nada foi gravado.");
        }

        context.Transacoes.RemoveRange(lancamentos);
        context.OperacoesRendaFixa.RemoveRange(operacoes);
        context.TitulosRendaFixa.Remove(titulo);
        await context.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
    }

    private static void GarantirOperacaoEditavel(
        IReadOnlyList<Transacao> lancamentos,
        int operacaoId,
        HashSet<(int Mes, int Ano)> periodos)
    {
        var lancamento = lancamentos.FirstOrDefault(t => t.OperacaoRendaFixaId == operacaoId);
        if (lancamento is not null && PeriodoFechado(periodos, lancamento.Data))
        {
            throw new InvalidOperationException(
                $"O mês {lancamento.Data:MM/yyyy} está fechado. A operação fica só leitura. Nada foi gravado.");
        }
    }

    private static void GarantirMesAberto(
        string codigo,
        PosicaoRendaFixaCalculada posicao,
        IReadOnlyList<Transacao> lancamentos,
        HashSet<(int Mes, int Ano)> periodos)
    {
        var afetados = new List<string>();
        var resultados = posicao.Resgates.ToDictionary(r => r.Id);

        foreach (var lancamento in lancamentos)
        {
            var opId = lancamento.OperacaoRendaFixaId!.Value;
            resultados.TryGetValue(opId, out var resultado);
            var novo = resultado?.Resultado ?? 0m;
            var novaData = resultado?.Data ?? lancamento.Data;
            if (!Diverge(lancamento, novo, novaData))
                continue;

            if (PeriodoFechado(periodos, lancamento.Data) || PeriodoFechado(periodos, novaData))
                afetados.Add($"{codigo} em {lancamento.Data:dd/MM/yyyy}");
        }

        foreach (var resultado in posicao.Resgates)
        {
            if (resultado.Resultado == 0m)
                continue;
            if (lancamentos.Any(t => t.OperacaoRendaFixaId == resultado.Id))
                continue;
            if (PeriodoFechado(periodos, resultado.Data))
                afetados.Add($"{codigo} em {resultado.Data:dd/MM/yyyy}");
        }

        if (afetados.Count == 0)
            return;

        throw new InvalidOperationException(
            "Mês fechado. A gravação mudaria o resultado do resgate já lançado: "
            + string.Join("; ", afetados.Distinct(StringComparer.Ordinal))
            + ". Nada foi gravado.");
    }

    private static bool Diverge(Transacao lancamento, decimal resultado, DateTime data)
    {
        if (resultado == 0m)
            return true;

        var tipo = resultado > 0m ? TipoTransacao.Receita : TipoTransacao.Despesa;
        return lancamento.Valor != Math.Abs(resultado)
            || lancamento.Tipo != tipo
            || lancamento.Data.Date != data.Date;
    }

    private async Task SincronizarLancamentosAsync(
        AppDbContext context,
        TituloRendaFixa titulo,
        IReadOnlyList<OperacaoRendaFixa> operacoes,
        PosicaoRendaFixaCalculada posicao,
        IReadOnlyList<Transacao> lancamentos,
        int membroFamiliaId,
        CancellationToken cancellationToken)
    {
        var porOperacao = lancamentos.ToDictionary(t => t.OperacaoRendaFixaId!.Value);
        var operacaoPorId = operacoes.Where(o => o.Id != 0).ToDictionary(o => o.Id);
        var nova = operacoes.FirstOrDefault(o => o.Id == 0);
        if (nova is not null)
            operacaoPorId[0] = nova;

        foreach (var resultado in posicao.Resgates)
        {
            porOperacao.TryGetValue(resultado.Id, out var lancamento);
            if (resultado.Resultado == 0m)
                continue;

            var tipo = resultado.Resultado > 0m ? TipoTransacao.Receita : TipoTransacao.Despesa;
            var categoria = await ObterOuCriarCategoriaAsync(context, tipo, cancellationToken);
            var valor = Math.Abs(resultado.Resultado);
            var data = resultado.Data.Date;
            var descricao = MontarDescricao(tipo, titulo.Codigo);

            if (lancamento is null)
            {
                var operacao = operacaoPorId[resultado.Id];
                var criado = new Transacao
                {
                    Descricao = descricao,
                    Valor = valor,
                    Data = data,
                    Tipo = tipo,
                    Recorrencia = TipoRecorrencia.Unica,
                    Pago = true,
                    Observacao = ObservacaoLancamento,
                    MembroFamiliaId = membroFamiliaId,
                    Categoria = categoria
                };

                if (operacao.Id == 0)
                    criado.OperacaoRendaFixa = operacao;
                else
                    criado.OperacaoRendaFixaId = operacao.Id;

                context.Transacoes.Add(criado);
                continue;
            }

            if (lancamento.Valor == valor
                && lancamento.Tipo == tipo
                && lancamento.Data.Date == data
                && lancamento.CategoriaId == categoria.Id
                && lancamento.Descricao == descricao
                && lancamento.Pago)
            {
                continue;
            }

            lancamento.Descricao = descricao;
            lancamento.Valor = valor;
            lancamento.Data = data;
            lancamento.Tipo = tipo;
            lancamento.Recorrencia = TipoRecorrencia.Unica;
            lancamento.Pago = true;
            lancamento.Observacao = ObservacaoLancamento;
            if (categoria.Id == 0)
                lancamento.Categoria = categoria;
            else
                lancamento.CategoriaId = categoria.Id;
            lancamento.AtualizadoEm = DateTime.UtcNow;
        }

        // Resultado zero ou operação que saiu da sequência: o lançamento sai. Quem ficou igual não é regravado.
        var idsComResultado = posicao.Resgates.Where(r => r.Resultado != 0m).Select(r => r.Id).ToHashSet();
        foreach (var lancamento in lancamentos)
        {
            if (lancamento.OperacaoRendaFixaId is int opId && !idsComResultado.Contains(opId))
                context.Transacoes.Remove(lancamento);
        }
    }

    private static async Task<Categoria> ObterOuCriarCategoriaAsync(
        AppDbContext context,
        TipoTransacao tipo,
        CancellationToken cancellationToken)
    {
        var nome = tipo == TipoTransacao.Receita ? CategoriaGanho : CategoriaPerda;
        var existente = context.Categorias.Local.FirstOrDefault(c => string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase))
            ?? await context.Categorias.FirstOrDefaultAsync(c => c.Nome == nome, cancellationToken);
        if (existente is not null)
            return existente;

        var nova = new Categoria
        {
            Nome = nome,
            Icone = tipo == TipoTransacao.Receita ? "📈" : "📉",
            CorHex = tipo == TipoTransacao.Receita ? "#6DA538" : "#659ECD",
            TipoPadrao = tipo,
            EhPadraoDoSistema = true,
            PermiteItensFatura = false,
            Ativo = true
        };
        context.Categorias.Add(nova);
        return nova;
    }

    private static async Task<List<Transacao>> CarregarLancamentosAsync(
        AppDbContext context,
        IReadOnlyList<OperacaoRendaFixa> operacoes,
        CancellationToken cancellationToken)
    {
        var ids = operacoes.Where(o => o.Id != 0).Select(o => o.Id).ToList();
        if (ids.Count == 0)
            return [];

        return await context.Transacoes
            .Where(t => t.OperacaoRendaFixaId != null && ids.Contains(t.OperacaoRendaFixaId.Value))
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
        int ignorarId,
        CancellationToken cancellationToken)
    {
        var repetido = await context.OperacoesRendaFixa.AnyAsync(
            o => o.HashImportacao == hash && o.Id != ignorarId,
            cancellationToken);
        if (repetido)
            throw new InvalidOperationException(MensagemHashRepetido);
    }

    private static void CopiarCampos(OperacaoRendaFixa destino, OperacaoRendaFixaFormModel modelo)
    {
        destino.Data = modelo.Data.Date;
        destino.Tipo = modelo.Tipo;
        destino.Quantidade = modelo.Quantidade;
        destino.PrecoUnitario = modelo.PrecoUnitario;
        destino.Valor = modelo.Valor;
        destino.Custos = modelo.Custos;
        destino.IrRetido = modelo.IrRetido;
    }

    private static MovimentoRendaFixa ParaMovimento(OperacaoRendaFixa operacao, string codigo) => new()
    {
        Id = operacao.Id,
        Data = operacao.Data,
        Tipo = operacao.Tipo,
        Quantidade = operacao.Quantidade,
        Valor = operacao.Valor,
        Custos = operacao.Custos,
        IrRetido = operacao.IrRetido,
        Codigo = codigo
    };

    private static string MontarDescricao(TipoTransacao tipo, string codigo)
    {
        var prefixo = tipo == TipoTransacao.Receita
            ? "Ganho realizado em renda fixa — "
            : "Perda realizada em renda fixa — ";
        var texto = prefixo + codigo;
        return texto.Length <= 120 ? texto : texto[..120];
    }

    private static void ValidarCampos(OperacaoRendaFixaFormModel modelo)
    {
        if (modelo.TituloRendaFixaId <= 0)
            throw new InvalidOperationException("Selecione um título.");
        if (!Enum.IsDefined(modelo.Tipo))
            throw new InvalidOperationException("Informe se a operação é aplicação ou resgate.");
        if (modelo.Data == default)
            throw new InvalidOperationException("Informe a data.");
        if (modelo.Data.Date > DateTime.Today)
            throw new InvalidOperationException("A data não pode ser futura.");
        if (modelo.Quantidade <= 0)
            throw new InvalidOperationException("A quantidade deve ser maior que zero.");
        if (Escala(modelo.Quantidade) > 8)
            throw new InvalidOperationException("A quantidade aceita no máximo 8 casas decimais.");
        if (modelo.PrecoUnitario < 0)
            throw new InvalidOperationException("O preço não pode ser negativo.");
        if (Escala(modelo.PrecoUnitario) > 8)
            throw new InvalidOperationException("O preço aceita no máximo 8 casas decimais.");
        if (modelo.Valor <= 0)
            throw new InvalidOperationException("O valor deve ser maior que zero.");
        if (Escala(modelo.Valor) > 2)
            throw new InvalidOperationException("O valor aceita no máximo 2 casas decimais.");
        if (modelo.Custos < 0)
            throw new InvalidOperationException("Os custos não podem ser negativos.");
        if (Escala(modelo.Custos) > 2)
            throw new InvalidOperationException("Os custos aceitam no máximo 2 casas decimais.");
        if (modelo.IrRetido < 0)
            throw new InvalidOperationException("O IR retido não pode ser negativo.");
        if (Escala(modelo.IrRetido) > 2)
            throw new InvalidOperationException("O IR retido aceita no máximo 2 casas decimais.");

        var hash = NormalizarHash(modelo.HashImportacao);
        if (hash is not null && hash.Length > 64)
            throw new InvalidOperationException("O hash de importação deve ter no máximo 64 caracteres.");
    }

    private static string? NormalizarHash(string? hash)
        => string.IsNullOrWhiteSpace(hash) ? null : hash.Trim();

    private static int Escala(decimal valor)
        => (decimal.GetBits(valor)[3] >> 16) & 0x7F;

    private static bool EhHashRepetido(DbUpdateException ex)
    {
        var texto = ex.InnerException?.Message ?? ex.Message;
        return texto.Contains("HashImportacao", StringComparison.Ordinal);
    }

    private static bool PeriodoFechado(HashSet<(int Mes, int Ano)> periodos, DateTime data)
        => periodos.Contains((data.Month, data.Year));
}
