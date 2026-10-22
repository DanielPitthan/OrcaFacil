namespace OrcaFacil.Tests.Tema;

/// <summary>
/// O outlined selecionado do MudBlazor 9.9 pinta :hover, :focus-visible e :active
/// com rgba(primary, 0.12). No escuro isso vira #252F41 e o texto #0B1220 cai para 1,39:1.
/// A regra do painel precisa cobrir os quatro estados com o primário sólido.
/// </summary>
public class ChipInteligenciaContrasteTests
{
    [Theory]
    [InlineData("html.of-tema-claro", "#1E2A33", "#659ECD")]
    [InlineData("html.of-tema-escuro", "#0B1220", "#6EA8FE")]
    public void ChipSelecionado_CobreOsQuatroEstadosComHexEContraste(string tema, string texto, string fundo)
    {
        var css = File.ReadAllText(Caminho("InteligenciaArtificialPanel.razor.css"));
        var bloco = BlocoDoTema(css, tema);
        var seletores = Seletores(bloco);

        Assert.Equal(4, seletores.Length);
        Assert.Contains(seletores, s => TerminaEm(s, ".mud-chip-selected"));
        Assert.Contains(seletores, s => TerminaEm(s, ".mud-chip-selected:hover"));
        Assert.Contains(seletores, s => TerminaEm(s, ".mud-chip-selected:focus-visible"));
        Assert.Contains(seletores, s => TerminaEm(s, ".mud-chip-selected:active"));
        Assert.All(seletores, s => Assert.Contains($":global({tema})", s, StringComparison.Ordinal));
        Assert.All(seletores, s => Assert.Contains(
            ".mud-chip.mud-chip-outlined.mud-chip-color-primary.mud-chip-selected",
            s,
            StringComparison.Ordinal));

        var declaracao = bloco[(bloco.IndexOf('{') + 1)..bloco.IndexOf('}')];
        Assert.Contains($"background-color: {fundo};", declaracao, StringComparison.Ordinal);
        Assert.Contains($"color: {texto};", declaracao, StringComparison.Ordinal);
        Assert.DoesNotContain("#252F41", bloco, StringComparison.OrdinalIgnoreCase);

        var razao = Contraste(texto, fundo);
        Assert.True(razao >= 4.5, $"{texto} sobre {fundo} = {razao:0.00}");
    }

    [Fact]
    public void ChipNaoSelecionadoNoTemaClaro_UsaBordaDoTexto()
    {
        var css = File.ReadAllText(Caminho("InteligenciaArtificialPanel.razor.css"));

        Assert.Contains("border-color: var(--of-text);", css, StringComparison.Ordinal);
        Assert.Contains(
            ":global(html.of-tema-claro) .of-ia-chips ::deep .mud-chip.mud-chip-outlined:not(.mud-chip-selected)",
            css,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CssDoChip_NaoUsaOFundoCompostoDoHover()
    {
        var css = File.ReadAllText(Caminho("InteligenciaArtificialPanel.razor.css"));
        Assert.DoesNotContain("#252F41", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Painel_PassaCancelamentoEMarcaOChipPressionado()
    {
        var razor = File.ReadAllText(Caminho("InteligenciaArtificialPanel.razor"));
        Assert.Contains("UserAttributes=\"@AtributosChip(provedor)\"", razor, StringComparison.Ordinal);
        Assert.Contains("[\"aria-pressed\"]", razor, StringComparison.Ordinal);
        Assert.Contains(
            "Cliente.TestarConexaoAsync(_url, chave, _modelo, _cancelamento.Token)",
            razor,
            StringComparison.Ordinal);
        Assert.Contains("ResolverChaveAsync(_cancelamento.Token)", razor, StringComparison.Ordinal);
    }

    private static bool TerminaEm(string seletor, string sufixo) =>
        seletor.Trim().EndsWith(sufixo, StringComparison.Ordinal);

    private static string[] Seletores(string bloco)
    {
        var cabeca = bloco[..bloco.IndexOf('{')];
        return cabeca.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string BlocoDoTema(string css, string tema)
    {
        var marcador = $":global({tema})";
        var inicio = css.IndexOf(marcador, StringComparison.Ordinal);
        if (inicio < 0)
            throw new InvalidOperationException($"Regra de {tema} não encontrada.");

        var fecha = css.IndexOf('}', inicio);
        if (fecha < 0)
            throw new InvalidOperationException($"Bloco de {tema} sem fechamento.");

        return css[inicio..(fecha + 1)];
    }

    private static string Caminho(string arquivo)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var caminho = Path.Combine(
                dir.FullName,
                "OrcaFacil",
                "Components",
                "Pages",
                "Configuracoes",
                arquivo);
            if (File.Exists(caminho))
                return caminho;

            dir = dir.Parent;
        }

        throw new InvalidOperationException($"Não foi possível localizar {arquivo}.");
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
