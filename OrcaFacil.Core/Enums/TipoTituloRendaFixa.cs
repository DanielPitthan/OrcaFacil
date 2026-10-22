namespace OrcaFacil.Core.Enums;

/// <summary>
/// O inteiro gravado é o contrato. Valores novos entram no fim:
/// um título já salvo como Outro (4) continua Outro.
/// </summary>
public enum TipoTituloRendaFixa
{
    CDB = 0,
    TesouroPrefixado = 1,
    TesouroIPCA = 2,
    TesouroSelic = 3,
    Outro = 4,
    LCI = 5,
    LCA = 6
}
