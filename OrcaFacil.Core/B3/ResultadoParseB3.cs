namespace OrcaFacil.Core.B3;

/// <summary>
/// Resultado da leitura. Cada registro do arquivo aparece em <see cref="Linhas"/> ou em <see cref="Erros"/>.
/// Linha em branco não é registro.
/// </summary>
public sealed record ResultadoParseB3(
    IReadOnlyList<LinhaExtratoB3> Linhas,
    IReadOnlyList<ErroParseB3> Erros);
