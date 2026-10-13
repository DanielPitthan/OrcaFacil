namespace OrcaFacil.Core.Noticias;

public sealed record Noticia(string Titulo, string Link, string Veiculo, DateTimeOffset? PublicadaEm);
