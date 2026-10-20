using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Services;

/// <summary>
/// Único caminho de gravação do provento. O valor líquido vira receita do mês do pagamento,
/// no mesmo formato de lançamento do resultado realizado, na categoria Proventos.
/// Não recalcula a venda.
/// </summary>
public class ProventoService
{
    public const string MensagemHashRepetido = "Já existe um provento com este hash de importação.";

    private const string ObservacaoLancamento = "Gerado pelo provento do ativo.";

    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ProventoService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public Task<Provento> SalvarAsync(
        ProventoFormModel modelo,
        int membroFamiliaId,
        CancellationToken cancellationToken = default)
        => EmTransacaoAsync(
            (context, ct) => SalvarNoContextoAsync(context, modelo, membroFamiliaId, ct),
            cancellationToken);

    /// <summary>
    /// Grava na transação que o chamador já abriu. Não confirma nem desfaz.
    /// A importação do extrato usa isto para gravar várias linhas de uma vez.
    /// </summary>
    public async Task<Provento> SalvarNoContextoAsync(
        AppDbContext context,
        ProventoFormModel modelo,
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

        Provento? existente = null;
        if (modelo.Id is int id)
        {
            existente = await context.Proventos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                ?? throw new InvalidOperationException("Provento não encontrado.");

            if (existente.MembroFamiliaId != membroFamiliaId)
                throw new InvalidOperationException("Provento não encontrado.");
        }
        else if (!ativo.Ativo)
        {
            throw new InvalidOperationException("Este ativo está inativo e não aceita novos proventos.");
        }

        if (existente is not null && existente.AtivoId != modelo.AtivoId && !ativo.Ativo)
            throw new InvalidOperationException("Este ativo está inativo e não aceita novos proventos.");

        var hash = existente?.HashImportacao ?? NormalizarHash(modelo.HashImportacao);
        if (existente is null && hash is not null)
            await GarantirHashLivreAsync(context, hash, cancellationToken);

        Transacao? lancamento = null;
        if (existente is not null)
        {
            lancamento = await context.Transacoes
                .FirstOrDefaultAsync(t => t.ProventoId == existente.Id, cancellationToken);
        }

        var periodos = await CarregarFechadosAsync(context, cancellationToken);
        var datas = new List<DateTime> { modelo.DataPagamento.Date };
        if (existente is not null)
            datas.Add(existente.DataPagamento);
        if (lancamento is not null)
            datas.Add(lancamento.Data);
        GarantirMesAberto(periodos, ativo.Ticker, datas);

        var categoria = await LancamentoResultadoAtivo.ObterOuCriarCategoriaProventosAsync(
            context, cancellationToken);
        var descricao = MontarDescricao(modelo.Tipo, ativo.Ticker);
        var data = modelo.DataPagamento.Date;
        var valor = Math.Round(modelo.ValorLiquido, 2, MidpointRounding.AwayFromZero);

        Provento alvo;
        if (existente is null)
        {
            alvo = new Provento
            {
                MembroFamiliaId = membroFamiliaId,
                Origem = modelo.Origem,
                HashImportacao = hash
            };
            CopiarCampos(alvo, modelo);
            context.Proventos.Add(alvo);

            var criado = LancamentoResultadoAtivo.Criar(
                membroFamiliaId, valor, data, TipoTransacao.Receita, categoria, descricao, ObservacaoLancamento);
            criado.Provento = alvo;
            context.Transacoes.Add(criado);
        }
        else
        {
            alvo = existente;
            var proventoIgual = alvo.AtivoId == modelo.AtivoId
                && alvo.Tipo == modelo.Tipo
                && alvo.DataPagamento.Date == data
                && alvo.ValorLiquido == valor;
            var lancamentoIgual = lancamento is not null
                && LancamentoResultadoAtivo.Igual(
                    lancamento, valor, data, TipoTransacao.Receita, categoria, descricao, ObservacaoLancamento);

            if (proventoIgual && lancamentoIgual)
                return alvo;

            if (!proventoIgual)
            {
                CopiarCampos(alvo, modelo);
                alvo.AtualizadoEm = DateTime.UtcNow;
            }

            if (lancamento is null)
            {
                var criado = LancamentoResultadoAtivo.Criar(
                    membroFamiliaId, valor, data, TipoTransacao.Receita, categoria, descricao, ObservacaoLancamento);
                criado.ProventoId = alvo.Id;
                context.Transacoes.Add(criado);
            }
            else if (!lancamentoIgual)
            {
                LancamentoResultadoAtivo.Aplicar(
                    lancamento, valor, data, TipoTransacao.Receita, categoria, descricao, ObservacaoLancamento);
            }
        }

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

        var provento = await context.Proventos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Provento não encontrado.");

        if (provento.MembroFamiliaId != membroFamiliaId)
            throw new InvalidOperationException("Provento não encontrado.");

        var ativo = await context.AtivosFinanceiros.FirstAsync(a => a.Id == provento.AtivoId, cancellationToken);
        var lancamento = await context.Transacoes
            .FirstOrDefaultAsync(t => t.ProventoId == provento.Id, cancellationToken);
        var periodos = await CarregarFechadosAsync(context, cancellationToken);
        var datas = new List<DateTime> { provento.DataPagamento };
        if (lancamento is not null)
            datas.Add(lancamento.Data);
        GarantirMesAberto(periodos, ativo.Ticker, datas);

        if (lancamento is not null)
            context.Transacoes.Remove(lancamento);
        context.Proventos.Remove(provento);

        await context.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
    }

    private static void GarantirMesAberto(
        HashSet<(int Mes, int Ano)> periodos,
        string ticker,
        IEnumerable<DateTime> datas)
    {
        var afetados = datas
            .Where(data => PeriodoFechado(periodos, data))
            .Select(data => $"{ticker} em {data:dd/MM/yyyy}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (afetados.Count == 0)
            return;

        throw new InvalidOperationException(
            "Mês fechado. A gravação alteraria o lançamento de "
            + string.Join("; ", afetados)
            + ". Nada foi gravado.");
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
        var repetido = await context.Proventos.AnyAsync(p => p.HashImportacao == hash, cancellationToken);
        if (repetido)
            throw new InvalidOperationException(MensagemHashRepetido);
    }

    private static void CopiarCampos(Provento destino, ProventoFormModel modelo)
    {
        destino.AtivoId = modelo.AtivoId;
        destino.Tipo = modelo.Tipo;
        destino.DataPagamento = modelo.DataPagamento.Date;
        destino.ValorLiquido = Math.Round(modelo.ValorLiquido, 2, MidpointRounding.AwayFromZero);
    }

    private static string MontarDescricao(TipoProvento tipo, string ticker)
    {
        var prefixo = tipo == TipoProvento.Jcp ? "JCP — " : "Rendimento — ";
        var texto = prefixo + ticker;
        return texto.Length <= 120 ? texto : texto[..120];
    }

    private static void ValidarCampos(ProventoFormModel modelo)
    {
        if (modelo.AtivoId <= 0)
            throw new InvalidOperationException("Selecione um ativo.");
        if (Math.Round(modelo.ValorLiquido, 2, MidpointRounding.AwayFromZero) <= 0)
            throw new InvalidOperationException("O valor líquido deve ser maior que zero.");

        var hash = NormalizarHash(modelo.HashImportacao);
        if (hash is not null && hash.Length > 64)
            throw new InvalidOperationException("O hash de importação deve ter no máximo 64 caracteres.");
    }

    private static string? NormalizarHash(string? hash)
        => string.IsNullOrWhiteSpace(hash) ? null : hash.Trim();

    private static bool EhHashRepetido(DbUpdateException ex)
    {
        var texto = ex.InnerException?.Message ?? ex.Message;
        return texto.Contains("HashImportacao", StringComparison.Ordinal);
    }

    private static bool PeriodoFechado(HashSet<(int Mes, int Ano)> periodos, DateTime data)
        => periodos.Contains((data.Month, data.Year));
}
