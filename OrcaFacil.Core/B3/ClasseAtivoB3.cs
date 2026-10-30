namespace OrcaFacil.Core.B3;

/// <summary>
/// Classe inferida do produto. Fiagro entra como <see cref="FII"/>.
/// Fundo de índice fica em <see cref="Acao"/>: o contrato não prevê ETF.
/// No cadastro, ETF conhecido é o tipo ETF e a DIRPF usa 07/06.
/// </summary>
public enum ClasseAtivoB3
{
    Acao,
    FII,
    RendaFixa,
    Outro
}
