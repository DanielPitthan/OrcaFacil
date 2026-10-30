using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.B3;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Ativos;

/// <summary>
/// ETFs da B3 que o extrato não separa: a classe do parser continua ação.
/// No cadastro viram <see cref="TipoAtivo.Etf"/> e a ficha de bens usa 07/06.
/// Fundo de índice de renda fixa nesta lista também fica nesse tipo:
/// o cadastro tem um único ETF e a ficha não escolhe 07/08.
/// </summary>
public static class CatalogoEtfConhecido
{
    private static readonly HashSet<string> Tickers = new(StringComparer.Ordinal)
    {
        "ASIA11",
        "B3BR11",
        "B5P211",
        "BBOI11",
        "BBOV11",
        "BBSD11",
        "BCIC11",
        "BDEF11",
        "BDOM11",
        "BEST11",
        "BITH11",
        "BIZD11",
        "BMMT11",
        "BNKS11",
        "BOVA11",
        "BOVB11",
        "BOVS11",
        "BOVV11",
        "BOVX11",
        "BRAX11",
        "BRAZ11",
        "BREW11",
        "BRIA11",
        "BRXC11",
        "BTER11",
        "DIVO11",
        "DOLA11",
        "EMEG11",
        "ETHE11",
        "FIND11",
        "GOLD11",
        "GOLX11",
        "GPUS11",
        "HASH11",
        "HIGH11",
        "IMAB11",
        "IRFM11",
        "IVVB11",
        "LFTB11",
        "LFTS11",
        "MUND11",
        "NASD11",
        "PIBB11",
        "QBTC11",
        "SMAC11",
        "SMAL11",
        "SPBZ11",
        "SPXI11",
        "SPXR11",
        "XFIX11",
        "XINA11",
        "XMAL11"
    };

    public static bool EhConhecido(string? ticker)
    {
        var codigo = TickerB3.Normalizar(ticker);
        if (Tickers.Contains(codigo))
            return true;

        // Fracionário: o F final não faz parte do papel (BOVA11F é o mesmo ETF).
        if (codigo.Length >= 6 && codigo[^1] == 'F' && char.IsDigit(codigo[^2]))
            return Tickers.Contains(codigo[..^1]);

        return false;
    }

    /// <summary>Tipo gravado na importação. ETF conhecido vence a classe do extrato.</summary>
    public static TipoAtivo TipoNaImportacao(string? ticker, ClasseAtivoB3 classe)
    {
        if (EhConhecido(ticker))
            return TipoAtivo.Etf;

        return classe == ClasseAtivoB3.FII ? TipoAtivo.Fii : TipoAtivo.Acao;
    }

    /// <summary>No formulário, ticker conhecido fica ETF. Os demais respeitam o tipo informado.</summary>
    public static TipoAtivo TipoNoCadastro(string? ticker, TipoAtivo informado)
        => EhConhecido(ticker) ? TipoAtivo.Etf : informado;

    /// <summary>
    /// Ação com ticker conhecido passa a ETF. FII, BDR e quem já é ETF ficam.
    /// A segunda passagem não altera nada.
    /// </summary>
    public static int Reclassificar(IEnumerable<AtivoFinanceiro> ativos)
    {
        ArgumentNullException.ThrowIfNull(ativos);

        var alterados = 0;
        foreach (var ativo in ativos)
        {
            if (ativo.Tipo != TipoAtivo.Acao || !EhConhecido(ativo.Ticker))
                continue;

            ativo.Tipo = TipoAtivo.Etf;
            alterados++;
        }

        return alterados;
    }

    public static async Task<int> ReclassificarAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var acoes = await context.AtivosFinanceiros
            .Where(ativo => ativo.Tipo == TipoAtivo.Acao)
            .ToListAsync(cancellationToken);
        var alterados = Reclassificar(acoes);
        if (alterados > 0)
            await context.SaveChangesAsync(cancellationToken);

        return alterados;
    }
}
