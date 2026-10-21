using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// Distingue Fiagro só na exportação da DIRPF.
/// O sinal é o mesmo da importação: o nome contém "fiagro".
/// O tipo do ativo permanece FII, e o classificador da B3 não muda.
/// </summary>
public static class ReconhecedorFiagroDirpf
{
    public static bool Eh(AtivoFinanceiro ativo)
    {
        ArgumentNullException.ThrowIfNull(ativo);
        return ativo.Tipo == TipoAtivo.Fii
            && ativo.Nome.Contains("fiagro", StringComparison.OrdinalIgnoreCase);
    }
}
