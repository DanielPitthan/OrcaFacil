namespace OrcaFacil.Tests.Llm;

public class RedutorFotoFonteTests
{
    [Fact]
    public void Android_AmostraAntesDoTeto_DescartaBitmap_EUsaGlobal()
    {
        var fonte = File.ReadAllText(Path.Combine(
            EncontrarRaiz(),
            "OrcaFacil",
            "Platforms",
            "Android",
            "Fotos",
            "RedutorFotoAndroid.cs"));

        Assert.Contains("PreparoFotoComprovante.CalcularAmostra", fonte, StringComparison.Ordinal);
        Assert.Contains("InSampleSize = amostra", fonte, StringComparison.Ordinal);
        Assert.Contains("Recycle()", fonte, StringComparison.Ordinal);
        Assert.Contains("global::Android.", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("LogWarning", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("LogInformation", fonte, StringComparison.Ordinal);
        Assert.Contains("OrientacaoExif.Rotate90", fonte, StringComparison.Ordinal);
        Assert.Contains("OrientacaoExif.Rotate270", fonte, StringComparison.Ordinal);
        Assert.Contains("OrientacaoExif.FlipHorizontal", fonte, StringComparison.Ordinal);
        Assert.Contains("OrientacaoExif.Transverse", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void Servico_MantemFotoPath_EApagaTemporarioNoFinally()
    {
        var fonte = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil", "Services", "FotoPerfilService.cs"));

        Assert.Contains("using OrcaFacil.Core.Armazenamento;", fonte, StringComparison.Ordinal);
        Assert.Contains("FotoPath.CaminhoDentroDaPasta(PastaFotos, valor)", fonte, StringComparison.Ordinal);
        Assert.Contains("return Path.GetFileName(destino);", fonte, StringComparison.Ordinal);
        Assert.Contains("FileSystem.AppDataDirectory, \"perfil\"", fonte, StringComparison.Ordinal);
        Assert.Contains("foto-{DateTime.Now:yyyyMMddHHmmss}.jpg", fonte, StringComparison.Ordinal);
        Assert.Contains("private const float LadoMaximo = 512f;", fonte, StringComparison.Ordinal);
        Assert.Contains("ApagarNovidades", fonte, StringComparison.Ordinal);
        Assert.Contains("LimpezaFotoTemporaria.PodeApagar", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("<<<<<<<", fonte, StringComparison.Ordinal);
        Assert.Equal(1, fonte.Split('\n').Count(linha => linha.Contains("LogWarning", StringComparison.Ordinal)));
    }

    private static string EncontrarRaiz()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.slnx")))
                return diretorio.FullName;

            diretorio = diretorio.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
