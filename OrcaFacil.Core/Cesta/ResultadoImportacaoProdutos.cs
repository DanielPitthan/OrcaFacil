namespace OrcaFacil.Core.Cesta;

public sealed record ResultadoImportacaoProdutos(int Importados, int Ignorados, IReadOnlyList<string> Erros);
