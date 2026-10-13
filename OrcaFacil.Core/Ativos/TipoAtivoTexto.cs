using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Ativos;

public static class TipoAtivoTexto
{
    public static string Rotulo(this TipoAtivo tipo) => tipo switch
    {
        TipoAtivo.Acao => "Ação",
        TipoAtivo.Fii => "FII",
        TipoAtivo.Etf => "ETF",
        TipoAtivo.Bdr => "BDR",
        _ => tipo.ToString()
    };
}
