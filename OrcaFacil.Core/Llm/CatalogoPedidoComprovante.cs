using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Escolhe o catálogo que vai no pedido e formata cada produto como <c>id|nome</c>.
/// O teto vem da tabela <see cref="RegraCatalogoComprovante"/>; o padrão é 300.
/// Acima do teto, seguem os produtos mais usados.
/// </summary>
public static class CatalogoPedidoComprovante
{
    public const int LimitePadrao = RegraCatalogoComprovante.LimitePadrao;

    public const int TamanhoMaximoNome = 80;

    public static int NormalizarLimite(int? valor) =>
        valor is >= 1 ? valor.Value : LimitePadrao;

    public static async Task<int> LimiteAsync(AppDbContext contexto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var valor = await contexto.RegrasCatalogoComprovante
            .AsNoTracking()
            .OrderBy(regra => regra.Id)
            .Select(regra => (int?)regra.LimiteProdutos)
            .FirstOrDefaultAsync(cancellationToken);
        return NormalizarLimite(valor);
    }

    public static async Task<IReadOnlyList<ProdutoCesta>> EscolherAsync(
        AppDbContext contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var limite = await LimiteAsync(contexto, cancellationToken);
        var produtos = await contexto.ProdutosCesta.AsNoTracking().ToListAsync(cancellationToken);
        var usosLista = await contexto.ItensCompraCesta
            .AsNoTracking()
            .GroupBy(item => item.ProdutoCestaId)
            .Select(grupo => new { grupo.Key, Usos = grupo.Count() })
            .ToListAsync(cancellationToken);
        var usos = usosLista.ToDictionary(item => item.Key, item => item.Usos);
        return Selecionar(produtos, usos, limite);
    }

    public static async Task<string> MontarAsync(AppDbContext contexto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return Texto(await EscolherAsync(contexto, cancellationToken));
    }

    public static IReadOnlyList<ProdutoCesta> Selecionar(
        IEnumerable<ProdutoCesta> catalogo,
        IReadOnlyDictionary<int, int> usos,
        int limite)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(usos);
        var teto = NormalizarLimite(limite);
        return catalogo
            .Where(produto => produto is not null && produto.Id > 0 && !string.IsNullOrWhiteSpace(produto.Nome))
            .GroupBy(produto => produto.Id)
            .Select(grupo => grupo.First())
            .OrderByDescending(produto => usos.TryGetValue(produto.Id, out var vezes) ? vezes : 0)
            .ThenBy(produto => produto.Id)
            .Take(teto)
            .ToList();
    }

    public static string Texto(IReadOnlyList<ProdutoCesta> produtos)
    {
        ArgumentNullException.ThrowIfNull(produtos);
        var sb = new StringBuilder();
        foreach (var produto in produtos)
        {
            if (produto is null || produto.Id <= 0 || string.IsNullOrWhiteSpace(produto.Nome))
                continue;

            if (sb.Length > 0)
                sb.Append('\n');

            sb.Append(produto.Id.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(Sanitizar(produto.Nome));
        }

        return sb.ToString();
    }

    private static string Sanitizar(string nome)
    {
        var texto = nome.Replace('\r', ' ').Replace('\n', ' ').Replace('|', ' ').Trim();
        texto = string.Join(' ', texto.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (texto.Length <= TamanhoMaximoNome)
            return texto;

        var corte = texto[..TamanhoMaximoNome];
        if (char.IsHighSurrogate(corte[^1]))
            corte = corte[..^1];

        return corte.TrimEnd();
    }
}
