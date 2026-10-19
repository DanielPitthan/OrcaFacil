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
        var bounds = fonte.IndexOf("InJustDecodeBounds = true", StringComparison.Ordinal);
        var amostra = fonte.IndexOf("InSampleSize = amostra", StringComparison.Ordinal);
        Assert.True(bounds >= 0 && bounds < amostra);
        Assert.Contains("Recycle()", fonte, StringComparison.Ordinal);
        Assert.Contains("global::Android.", fonte, StringComparison.Ordinal);
        Assert.Contains("global::Android.Graphics.BitmapFactory", fonte, StringComparison.Ordinal);
        Assert.Contains("CompressFormat.Jpeg", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("ImageSharp", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Magick", fonte, StringComparison.Ordinal);
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
        foreach (var assinatura in new[]
        {
            "Task<string?> TirarSelfieAsync()",
            "Task<string?> EscolherDaGaleriaAsync()",
            "Task<FotoComprovantePronta> PrepararComprovanteAsync(",
            "Task<string> SalvarAsync("
        })
        {
            var metodo = ExtrairBloco(fonte, assinatura);
            var finalmente = ExtrairFinally(metodo);
            Assert.Contains("ApagarTemporarioDoFluxo(", finalmente, StringComparison.Ordinal);
            Assert.DoesNotContain("ApagarTemporarioDoFluxo(", TrechoAntesDoFinally(metodo), StringComparison.Ordinal);
            Assert.DoesNotContain("LimpezaFotoTemporaria.ListarRemocao", TrechoAntesDoFinally(metodo), StringComparison.Ordinal);
        }

        var limpeza = ExtrairBloco(fonte, "void ApagarTemporarioDoFluxo(");
        Assert.Contains("LimpezaFotoTemporaria.ListarRemocao", limpeza, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchOption.AllDirectories", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("FotografarCaches", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("ApagarNovidades", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("<<<<<<<", fonte, StringComparison.Ordinal);
        Assert.Equal(1, fonte.Split('\n').Count(linha => linha.Contains("LogWarning", StringComparison.Ordinal)));
        Assert.Contains("EscolherMediaType(bytes, arquivo.FileName)", fonte, StringComparison.Ordinal);
        Assert.Contains("CodificarGraphicsAsync(", fonte, StringComparison.Ordinal);
        Assert.Contains("ImageFormat.Jpeg", fonte, StringComparison.Ordinal);
        Assert.Contains("TetoBytesAntesDoDecode", fonte, StringComparison.Ordinal);
        Assert.Contains("CabeAntesDoDecode", fonte, StringComparison.Ordinal);
        Assert.Contains("FotoComprovantePronta.AcimaDoTetoBruto()", fonte, StringComparison.Ordinal);

        var copiar = ExtrairBloco(fonte, "Task<byte[]> CopiarAsync(");
        var tipo = copiar.IndexOf("EscolherMediaType(", StringComparison.Ordinal);
        var restante = copiar.IndexOf("var buffer", StringComparison.Ordinal);
        Assert.True(tipo >= 0 && tipo < restante);
        Assert.Contains("TetoBytesAntesDoDecode", copiar, StringComparison.Ordinal);
        Assert.Contains("FotoComprovantePronta.FormatoNaoConvertido()", fonte, StringComparison.Ordinal);
        Assert.Contains("EscolherMediaType(jpeg) is null", fonte, StringComparison.Ordinal);
    }

    private static string TrechoAntesDoFinally(string metodo)
    {
        var indice = metodo.IndexOf("finally", StringComparison.Ordinal);
        Assert.True(indice > 0);
        return metodo[..indice];
    }

    private static string ExtrairFinally(string metodo)
    {
        var indice = metodo.IndexOf("finally", StringComparison.Ordinal);
        Assert.True(indice >= 0);
        return ExtrairChaves(metodo, metodo.IndexOf('{', indice));
    }

    private static string ExtrairBloco(string fonte, string assinatura)
    {
        var indice = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(indice >= 0, assinatura);
        return ExtrairChaves(fonte, fonte.IndexOf('{', indice));
    }

    private static string ExtrairChaves(string fonte, int abre)
    {
        var profundidade = 0;
        for (var i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{')
                profundidade++;
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                    return fonte[abre..(i + 1)];
            }
        }

        throw new InvalidOperationException("Bloco sem fechamento.");
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
