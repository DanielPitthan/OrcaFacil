namespace OrcaFacil.Core.B3;

/// <summary>Registro rejeitado. A linha do arquivo não entra em <see cref="ResultadoParseB3.Linhas"/>.</summary>
public sealed record ErroParseB3(int Linha, string Motivo);
