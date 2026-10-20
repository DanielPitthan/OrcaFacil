namespace OrcaFacil.Core.B3;

/// <summary>
/// Classe inferida do produto. Fiagro entra como <see cref="FII"/>.
/// Fundo de índice fica em <see cref="Acao"/>: o contrato não prevê ETF.
/// </summary>
public enum ClasseAtivoB3
{
    Acao,
    FII,
    RendaFixa,
    Outro
}
