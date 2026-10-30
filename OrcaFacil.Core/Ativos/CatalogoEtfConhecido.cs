using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.B3;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Ativos;

/// <summary>
/// ETFs da B3 que o extrato não separa: a classe do parser continua ação.
/// No cadastro, ação com ticker desta lista vira <see cref="TipoAtivo.Etf"/>.
/// FII e BDR, informados no formulário ou já gravados, permanecem.
/// ETF de ações usa o bem 07/06 e a alíquota de 15% no DARF, sem a isenção de R$ 20 mil.
/// ETF de renda fixa usa o bem 07/08 e não entra na apuração mensal:
/// o ganho vai para rendimentos de tributação exclusiva.
/// </summary>
public static class CatalogoEtfConhecido
{
    private static readonly HashSet<string> Tickers = new(StringComparer.Ordinal)
    {
        "ACWI11",
        "ASIA11",
        "B3BR11",
        "B5MB11",
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
        "BLFT11",
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
        "FIXA11",
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
        "WRLD11",
        "XFIX11",
        "XINA11",
        "XMAL11"
    };

    /// <summary>
    /// Fundos de índice de renda fixa. A corretora retém o imposto na fonte
    /// (Lei nº 13.043/2014, art. 2º). Não entram no DARF mensal.
    /// </summary>
    private static readonly HashSet<string> RendaFixa = new(StringComparer.Ordinal)
    {
        "B5MB11",
        "B5P211",
        "BLFT11",
        "FIXA11",
        "IMAB11",
        "IRFM11",
        "LFTB11",
        "LFTS11"
    };

    public static bool EhConhecido(string? ticker) => Tickers.Contains(Codigo(ticker));

    public static bool EhRendaFixa(string? ticker) => RendaFixa.Contains(Codigo(ticker));

    /// <summary>Tipo gravado na importação. ETF conhecido vence a classe do extrato.</summary>
    public static TipoAtivo TipoNaImportacao(string? ticker, ClasseAtivoB3 classe)
    {
        if (EhConhecido(ticker))
            return TipoAtivo.Etf;

        return classe == ClasseAtivoB3.FII ? TipoAtivo.Fii : TipoAtivo.Acao;
    }

    /// <summary>
    /// Formulário e reclassificação usam a mesma regra: só ação conhecida vira ETF.
    /// FII e BDR ficam como foram informados. Papel fora do catálogo não vira ETF.
    /// </summary>
    public static TipoAtivo TipoNoCadastro(string? ticker, TipoAtivo informado)
        => informado == TipoAtivo.Acao && EhConhecido(ticker) ? TipoAtivo.Etf : informado;

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
        if (!acoes.Any(ativo => EhConhecido(ativo.Ticker)))
            return 0;

        var alterados = 0;
        await AvisarAposMudancaAsync(context, () => alterados = Reclassificar(acoes), cancellationToken);
        if (alterados > 0)
            await context.SaveChangesAsync(cancellationToken);

        return alterados;
    }

    /// <summary>
    /// Compara a apuração dos meses fechados antes e depois de <paramref name="aplicar"/>.
    /// O aviso só entra no contexto; quem chamou grava.
    /// </summary>
    public static async Task AvisarAposMudancaAsync(
        AppDbContext context,
        Action aplicar,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(aplicar);

        var fechados = await context.FechamentosPeriodo.AsNoTracking()
            .Where(fechamento => fechamento.Fechado)
            .ToListAsync(cancellationToken);
        List<OperacaoAtivo>? operacoes = null;
        List<OperacaoApuracao>? antes = null;
        List<RegraFiscal>? regras = null;
        if (fechados.Count > 0)
        {
            operacoes = await context.OperacoesAtivo
                .Include(operacao => operacao.Ativo)
                .Include(operacao => operacao.MembroFamilia)
                .ToListAsync(cancellationToken);
            regras = await context.RegrasFiscais.AsNoTracking().ToListAsync(cancellationToken);
            antes = operacoes.Select(ApuradorIrMensal.De).ToList();
        }

        aplicar();

        if (antes is null || operacoes is null || regras is null)
            return;

        var depois = operacoes.Select(ApuradorIrMensal.De).ToList();
        var meses = AvisoDarfComplementar.MesesAlterados(antes, depois, regras, fechados);
        if (meses.Count == 0)
            return;

        context.LogsEvento.Add(new LogEvento
        {
            Tipo = TipoLogEvento.Sistema,
            Mensagem = AvisoDarfComplementar.Mensagem(meses)
        });
    }

    /// <summary>
    /// Tira espaço no meio e o F do fracionário. "BOVA11 F" e "BOVA11F" são BOVA11.
    /// <see cref="TickerB3.EhValido"/> não aceita espaço: isto só reconhece o que já foi gravado.
    /// </summary>
    private static string Codigo(string? ticker)
    {
        var semEspaco = new string((ticker ?? string.Empty).Where(caractere => !char.IsWhiteSpace(caractere)).ToArray());
        var codigo = TickerB3.Normalizar(semEspaco);
        if (codigo.Length >= 6 && codigo[^1] == 'F' && char.IsDigit(codigo[^2]) && !Tickers.Contains(codigo))
            return codigo[..^1];

        return codigo;
    }
}
