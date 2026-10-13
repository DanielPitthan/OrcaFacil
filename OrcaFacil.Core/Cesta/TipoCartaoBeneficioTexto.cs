using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Cesta;

public static class TipoCartaoBeneficioTexto
{
    public static string Rotulo(this TipoCartaoBeneficio tipo) => tipo switch
    {
        TipoCartaoBeneficio.ValeAlimentacao => "Vale Alimentação",
        TipoCartaoBeneficio.ValeRefeicao => "Vale Refeição",
        TipoCartaoBeneficio.Outro => "Outro benefício",
        _ => tipo.ToString()
    };
}
