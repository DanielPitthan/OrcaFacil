namespace OrcaFacil.Tests.Tema;

/// <summary>
/// O rótulo de 12px usa --of-text-muted, que aponta para TextDisabled.
/// No claro o padrão do MudBlazor (preto a 38%) fica em 2,64:1 sobre #EFF3F7.
/// O chip do mês fechado pinta esse texto sobre GrayLighter, #E0E0E0.
/// </summary>
public class TextDisabledContrasteTests
{
    [Theory]
    [InlineData("PaletteLight", "#EFF3F7")]
    [InlineData("PaletteLight", "#FFFFFF")]
    [InlineData("PaletteLight", "#E0E0E0")]
    [InlineData("PaletteDark", "#12151B")]
    [InlineData("PaletteDark", "#1B1F27")]
    public void TextDisabled_TemContrasteDeTextoWcag(string paleta, string fundo)
    {
        var cor = TextDisabled(LerTema(), paleta);
        var razao = Contraste(cor, fundo);

        Assert.True(razao >= 4.5, $"{paleta} {cor} sobre {fundo} = {razao:0.00}");
    }

    [Fact]
    public void TemaEscuro_MantemTextDisabled()
    {
        var cor = TextDisabled(LerTema(), "PaletteDark");
        Assert.Equal("#9AA3B2", cor);
        Assert.True(Contraste(cor, "#12151B") >= 4.5);
        Assert.True(Contraste(cor, "#1B1F27") >= 4.5);
    }

    [Fact]
    public void MutacaoPara636C78_FalhaSobreOCinzaDoChip()
    {
        var fonte = LerTema();
        var atual = TextDisabled(fonte, "PaletteLight");
        var mutado = fonte.Replace(
            $"TextDisabled = \"{atual}\"",
            "TextDisabled = \"#636C78\"",
            StringComparison.Ordinal);

        Assert.NotEqual(fonte, mutado);
        var cor = TextDisabled(mutado, "PaletteLight");
        Assert.Equal("#636C78", cor);

        var razao = Contraste(cor, "#E0E0E0");
        Assert.False(razao >= 4.5, $"{cor} sobre #E0E0E0 = {razao:0.00}");
    }

    private static string LerTema() =>
        SemComentariosDeLinha(File.ReadAllText(CaminhoTema()));

    private static string TextDisabled(string fonte, string paleta)
    {
        var bloco = BlocoDaPaleta(fonte, paleta);
        const string chave = "TextDisabled = \"";
        var inicio = bloco.IndexOf(chave, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"{paleta} não define TextDisabled.");

        inicio += chave.Length;
        var fim = bloco.IndexOf('"', inicio);
        return bloco[inicio..fim];
    }

    private static string BlocoDaPaleta(string fonte, string paleta)
    {
        var marcador = paleta + " = new " + paleta;
        var inicio = fonte.IndexOf(marcador, StringComparison.Ordinal);
        if (inicio < 0)
            throw new InvalidOperationException($"Paleta não encontrada: {paleta}.");

        var abre = fonte.IndexOf('{', inicio);
        var fecha = FecharBloco(fonte, abre);
        return fonte[abre..(fecha + 1)];
    }

    private static int FecharBloco(string fonte, int abre)
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
                    return i;
            }
        }

        throw new InvalidOperationException("Bloco da paleta sem fechamento.");
    }

    private static string SemComentariosDeLinha(string fonte)
    {
        var linhas = fonte.Split('\n');
        for (var i = 0; i < linhas.Length; i++)
        {
            var corte = linhas[i].IndexOf("//", StringComparison.Ordinal);
            if (corte >= 0)
                linhas[i] = linhas[i][..corte];
        }

        return string.Join('\n', linhas);
    }

    private static string CaminhoTema()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var caminho = Path.Combine(dir.FullName, "OrcaFacil", "Theme", "OrcaFacilThemes.cs");
            if (File.Exists(caminho))
                return caminho;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar OrcaFacilThemes.cs.");
    }

    private static double Contraste(string frente, string fundo)
    {
        var clara = Math.Max(Luminancia(frente), Luminancia(fundo));
        var escura = Math.Min(Luminancia(frente), Luminancia(fundo));
        return (clara + 0.05) / (escura + 0.05);
    }

    private static double Luminancia(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Canal(hex[..2]);
        var g = Canal(hex.Substring(2, 2));
        var b = Canal(hex.Substring(4, 2));
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    private static double Canal(string hex)
    {
        var valor = Convert.ToInt32(hex, 16) / 255d;
        return valor <= 0.04045 ? valor / 12.92 : Math.Pow((valor + 0.055) / 1.055, 2.4);
    }
}
