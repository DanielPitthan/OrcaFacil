using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Core.Services;

/// <summary>
/// Linha válida da prévia. <see cref="Duplicada"/> compara o hash da linha com o hash gravado
/// dos itens importados. Item lançado na mão, sem hash, usa a data da fatura.
/// Também marca a repetição dentro do próprio arquivo.
/// </summary>
public sealed record LinhaPreviaImportacaoFaturaCsv(
    int Numero,
    DateTime Data,
    string Descricao,
    string Descritivo,
    decimal Valor,
    bool EhEstorno,
    int CategoriaId,
    string NomeCategoria,
    NaturezaItemLancamento Natureza,
    int? ParcelaAtual,
    int? TotalParcelas,
    string Hash,
    bool Duplicada);

/// <summary>
/// Prévia da importação. Não grava.
/// Os totais somam as linhas válidas que ainda não estão na fatura.
/// <see cref="TotalFatura"/> e <see cref="SomaItensAtual"/> vêm da fatura gravada.
/// O valor digitado na tela, ainda não salvo, não entra no teto.
/// </summary>
public sealed record PreviaImportacaoFaturaCsv(
    bool ArquivoAceito,
    string? ErroArquivo,
    bool MesFechado,
    IReadOnlyList<LinhaPreviaImportacaoFaturaCsv> LinhasValidas,
    IReadOnlyList<FaturaCsv.ErroLinha> Erros,
    decimal TotalCompras,
    decimal TotalEstornos,
    decimal TotalLiquido,
    decimal TotalFatura = 0m,
    decimal SomaItensAtual = 0m);

/// <summary>O que a confirmação gravou. Totais são das linhas novas, já arredondados.</summary>
public sealed record ResultadoImportacaoFaturaCsv(
    int Gravados,
    int DuplicatasIgnoradas,
    decimal TotalCompras,
    decimal TotalEstornos,
    decimal TotalLiquido,
    decimal TotalFatura);

/// <summary>
/// Escolha da confirmação. Sem números, entram todas as linhas válidas.
/// Duplicata repetida é ignorada, salvo quando <see cref="IgnorarDuplicatas"/> é falso: aí a confirmação inteira é recusada.
/// </summary>
public sealed record PedidoConfirmacaoImportacaoFaturaCsv(
    IReadOnlyCollection<int>? LinhasEscolhidas = null,
    bool IgnorarDuplicatas = true);

/// <summary>
/// Importa o CSV padronizado para uma fatura de cartão já aberta.
/// A prévia não grava. A confirmação grava as linhas válidas escolhidas numa transação só:
/// compra entra como item; valor negativo vira estorno avulso e abate o total.
/// Se o arquivo tem erro, o mês está fechado ou qualquer linha escolhida falha, nada é gravado.
/// </summary>
public class ImportacaoFaturaCsvService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly EstornoFaturaService _estornos;

    public ImportacaoFaturaCsvService(
        IDbContextFactory<AppDbContext> contextFactory,
        EstornoFaturaService estornos)
    {
        _contextFactory = contextFactory;
        _estornos = estornos;
    }

    public Task<PreviaImportacaoFaturaCsv> PreverAsync(
        int faturaId,
        string texto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return PreverAsync(faturaId, FaturaCsvLayout.Codificar(texto), cancellationToken);
    }

    public async Task<PreviaImportacaoFaturaCsv> PreverAsync(
        int faturaId,
        byte[] arquivo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arquivo);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var fatura = await CarregarAsync(context, faturaId, acompanhar: false, cancellationToken);
        var mesFechado = await EstornoFaturaService.MesEstaFechadoAsync(context, fatura, cancellationToken);
        var leitura = await LerAsync(context, arquivo, cancellationToken);
        return MontarPrevia(fatura, mesFechado, leitura);
    }

    public Task<ResultadoImportacaoFaturaCsv> ConfirmarAsync(
        int faturaId,
        string texto,
        PedidoConfirmacaoImportacaoFaturaCsv? pedido = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return ConfirmarAsync(faturaId, FaturaCsvLayout.Codificar(texto), pedido, cancellationToken);
    }

    public async Task<ResultadoImportacaoFaturaCsv> ConfirmarAsync(
        int faturaId,
        byte[] arquivo,
        PedidoConfirmacaoImportacaoFaturaCsv? pedido = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        pedido ??= new PedidoConfirmacaoImportacaoFaturaCsv();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var fatura = await CarregarAsync(context, faturaId, acompanhar: true, cancellationToken);
        await EstornoFaturaService.ExigirMesAbertoAsync(context, fatura, cancellationToken);

        var leitura = await LerAsync(context, arquivo, cancellationToken);
        if (!leitura.ArquivoAceito)
            throw new InvalidOperationException(leitura.ErroArquivo);

        if (leitura.Erros.Count > 0)
            throw new InvalidOperationException("O arquivo tem linhas com erro. Nada foi importado.");

        var escolhidas = Selecionar(leitura.Linhas, pedido.LinhasEscolhidas);
        var hashes = ColetarHashes(fatura);
        var gravar = new List<FaturaCsv.Linha>();
        var ignoradas = 0;
        foreach (var linha in escolhidas)
        {
            if (!hashes.Add(linha.Hash))
            {
                if (!pedido.IgnorarDuplicatas)
                    throw new InvalidOperationException($"A linha {linha.Numero} repete um item já lançado nesta fatura. Nada foi importado.");

                ignoradas++;
                continue;
            }

            gravar.Add(linha);
        }

        if (gravar.Count == 0)
        {
            return new ResultadoImportacaoFaturaCsv(
                0, ignoradas, 0, 0, 0, fatura.Valor);
        }

        if (fatura.Tipo != TipoTransacao.Despesa || fatura.Categoria?.PermiteItensFatura != true)
            throw new InvalidOperationException("Esta categoria não permite itens na fatura.");

        await using var transacao = await context.Database.BeginTransactionAsync(cancellationToken);
        foreach (var linha in gravar)
        {
            if (linha.EhEstorno)
            {
                _estornos.Aplicar(
                    fatura,
                    linha.Valor,
                    linha.Descricao,
                    linha.CategoriaId,
                    linha.Descritivo,
                    linha.Hash,
                    arredondar: true);
                continue;
            }

            var ordem = fatura.Itens.Count == 0 ? 0 : fatura.Itens.Max(i => i.Ordem) + 1;
            fatura.Itens.Add(CriarCompra(linha, ordem));
            ItensFaturaRegras.Validar(
                fatura.Valor,
                fatura.CategoriaId,
                permiteItens: true,
                fatura.Tipo,
                fatura.Itens.ToList());
        }

        fatura.AtualizadoEm = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        var compras = Somar(gravar, estorno: false);
        var estornos = Somar(gravar, estorno: true);
        return new ResultadoImportacaoFaturaCsv(
            gravar.Count,
            ignoradas,
            compras,
            estornos,
            DinheiroEstorno.Arredondar(compras - estornos),
            fatura.Valor);
    }

    private static PreviaImportacaoFaturaCsv MontarPrevia(Transacao fatura, bool mesFechado, FaturaCsv.Leitura leitura)
    {
        var totalGravado = fatura.Valor;
        var somaItens = fatura.Itens.Sum(ItensFaturaRegras.ValorAssinado);
        if (!leitura.ArquivoAceito)
        {
            return new PreviaImportacaoFaturaCsv(
                false, leitura.ErroArquivo, mesFechado, [], [], 0, 0, 0, totalGravado, somaItens);
        }

        var hashes = ColetarHashes(fatura);
        var linhas = new List<LinhaPreviaImportacaoFaturaCsv>(leitura.Linhas.Count);
        foreach (var linha in leitura.Linhas)
        {
            var duplicada = !hashes.Add(linha.Hash);
            linhas.Add(new LinhaPreviaImportacaoFaturaCsv(
                linha.Numero,
                linha.Data,
                linha.Descricao,
                linha.Descritivo,
                linha.Valor,
                linha.EhEstorno,
                linha.CategoriaId,
                linha.NomeCategoria,
                linha.Natureza,
                linha.ParcelaAtual,
                linha.TotalParcelas,
                linha.Hash,
                duplicada));
        }

        var novas = linhas.Where(l => !l.Duplicada).ToList();
        var compras = DinheiroEstorno.Arredondar(novas.Where(l => !l.EhEstorno).Sum(l => l.Valor));
        var estornos = DinheiroEstorno.Arredondar(novas.Where(l => l.EhEstorno).Sum(l => l.Valor));
        return new PreviaImportacaoFaturaCsv(
            true,
            null,
            mesFechado,
            linhas,
            leitura.Erros,
            compras,
            estornos,
            DinheiroEstorno.Arredondar(compras - estornos),
            totalGravado,
            somaItens);
    }

    private static List<FaturaCsv.Linha> Selecionar(
        IReadOnlyList<FaturaCsv.Linha> validas,
        IReadOnlyCollection<int>? linhasEscolhidas)
    {
        if (linhasEscolhidas is null)
            return validas.ToList();

        var porNumero = validas.ToDictionary(l => l.Numero);
        foreach (var numero in linhasEscolhidas.OrderBy(n => n))
        {
            if (!porNumero.ContainsKey(numero))
                throw new InvalidOperationException($"A linha {numero} não pode ser importada. Nada foi importado.");
        }

        var escolhidas = linhasEscolhidas as HashSet<int> ?? linhasEscolhidas.ToHashSet();
        return validas.Where(l => escolhidas.Contains(l.Numero)).ToList();
    }

    private static ItemLancamento CriarCompra(FaturaCsv.Linha linha, int ordem) => new()
    {
        Descricao = linha.Descricao.Trim(),
        Descritivo = ItensFaturaInclusao.NormalizarDescritivo(linha.Descritivo),
        Valor = DinheiroEstorno.Arredondar(linha.Valor),
        EhEstorno = false,
        Natureza = linha.Natureza,
        ParcelaAtual = linha.Natureza == NaturezaItemLancamento.Parcelada ? linha.ParcelaAtual : null,
        TotalParcelas = linha.Natureza == NaturezaItemLancamento.Parcelada ? linha.TotalParcelas : null,
        CategoriaId = linha.CategoriaId,
        Ordem = ordem,
        HashImportacao = linha.Hash
    };

    private static HashSet<string> ColetarHashes(Transacao fatura)
    {
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in fatura.Itens)
            hashes.Add(HashParaDeduplicacao(fatura, item));

        return hashes;
    }

    /// <summary>
    /// Item importado usa só o hash gravado, da data da linha.
    /// Recalcular na data da fatura misturaria, por exemplo, o Uber do dia 02 com o do dia 18.
    /// Sem hash, o item foi lançado na mão e a comparação usa a data da fatura.
    /// </summary>
    private static string HashParaDeduplicacao(Transacao fatura, ItemLancamento item)
    {
        if (!string.IsNullOrWhiteSpace(item.HashImportacao))
            return item.HashImportacao.Trim();

        var descritivo = string.IsNullOrWhiteSpace(item.Descritivo)
            ? item.Descricao?.Trim() ?? string.Empty
            : item.Descritivo.Trim();

        return FaturaCsv.CalcularHash(
            fatura.Data,
            item.Descricao?.Trim() ?? string.Empty,
            item.Valor,
            item.EhEstorno,
            item.CategoriaId,
            item.Natureza,
            item.ParcelaAtual,
            item.TotalParcelas,
            descritivo);
    }

    private static decimal Somar(IEnumerable<FaturaCsv.Linha> linhas, bool estorno)
        => DinheiroEstorno.Arredondar(linhas.Where(l => l.EhEstorno == estorno).Sum(l => l.Valor));

    private static async Task<FaturaCsv.Leitura> LerAsync(
        AppDbContext context,
        byte[] arquivo,
        CancellationToken cancellationToken)
    {
        var categorias = await context.Categorias.AsNoTracking().ToListAsync(cancellationToken);
        return FaturaCsv.Ler(arquivo, categorias);
    }

    private static async Task<Transacao> CarregarAsync(
        AppDbContext context,
        int faturaId,
        bool acompanhar,
        CancellationToken cancellationToken)
    {
        var consulta = context.Transacoes.AsQueryable();
        if (!acompanhar)
            consulta = consulta.AsNoTracking();

        return await consulta
            .Include(t => t.Categoria)
            .Include(t => t.Itens)
            .FirstOrDefaultAsync(t => t.Id == faturaId, cancellationToken)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");
    }
}
