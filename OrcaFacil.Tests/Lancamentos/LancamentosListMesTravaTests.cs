using System.Text;

namespace OrcaFacil.Tests.Lancamentos;

/// <summary>
/// A lista, o menu e o formulário são Razor. O teste lê o fonte sem comentários:
/// um comentário com o mesmo texto não mantém a trava verde.
/// </summary>
public class LancamentosListMesTravaTests
{
    private const string LeituraDoMes = "ConsultaMesLancamentos.Resolver(MesConsulta, DateTime.Today)";
    private const string TrocaDeMes = "NavigateTo($\"/lancamentos?mes={mes.ToString(\"yyyy-MM\", CultureInfo.InvariantCulture)}\", replace: true)";
    private const string MenuLancamentos = "Href=\"/lancamentos\"";
    private const string VoltaDoFormulario = "NavigateTo(ConsultaMesLancamentos.Rota(MesConsulta))";
    private const string NovoComMes = "NavigateTo($\"/lancamentos/novo?mes={_mesReferencia.ToString(\"yyyy-MM\", CultureInfo.InvariantCulture)}\")";
    private const string EditarComMes = "NavigateTo($\"/lancamentos/editar/{id}?mes={_mesReferencia.ToString(\"yyyy-MM\", CultureInfo.InvariantCulture)}\")";
    private const string CopiaComMes = "ConsultaMesLancamentos.Rota(MesConsulta, \"/lancamentos/novo\")";
    private const string VoltaSemMes = "NavigateTo(\"/lancamentos\")";

    [Fact]
    public void Lista_LeMesConsulta_SetasSubstituemAUrl_EMenuAbreLancamentos()
    {
        var lista = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentosList.razor"));
        var menu = SemComentarios(Ler("OrcaFacil", "Components", "Layout", "NavMenu.razor"));

        Assert.True(Contem(lista, LeituraDoMes));
        Assert.True(Contem(lista, TrocaDeMes));
        Assert.True(Contem(menu, MenuLancamentos));

        var semLeitura = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentosList.razor")
            .Replace(LeituraDoMes, "/* " + LeituraDoMes + " */", StringComparison.Ordinal));
        Assert.False(Contem(semLeitura, LeituraDoMes));

        var semTroca = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentosList.razor")
            .Replace(TrocaDeMes, "// " + TrocaDeMes, StringComparison.Ordinal));
        Assert.False(Contem(semTroca, TrocaDeMes));

        var menuComentado = SemComentarios(Ler("OrcaFacil", "Components", "Layout", "NavMenu.razor")
            .Replace(MenuLancamentos, "<!-- " + MenuLancamentos + " -->", StringComparison.Ordinal));
        Assert.False(Contem(menuComentado, MenuLancamentos));

        const string soComentario = """
            @* ConsultaMesLancamentos.Resolver(MesConsulta, DateTime.Today) *@
            // NavigateTo($"/lancamentos?mes={mes.ToString("yyyy-MM", CultureInfo.InvariantCulture)}", replace: true)
            <!-- Href="/lancamentos" -->
            """;
        var comentario = SemComentarios(soComentario);
        Assert.False(Contem(comentario, LeituraDoMes));
        Assert.False(Contem(comentario, TrocaDeMes));
        Assert.False(Contem(comentario, MenuLancamentos));
    }

    [Fact]
    public void Formulario_VoltaAoMesDeOrigem_EComentarioNaoConta()
    {
        var formulario = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor"));
        var lista = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentosList.razor"));

        Assert.Equal(1, Contar(formulario, VoltaDoFormulario));
        Assert.Equal(3, Contar(formulario, "Voltar();"));
        Assert.Equal(2, Contar(formulario, "OnClick=\"Voltar\""));
        Assert.Equal(1, Contar(formulario, "OnCancelar=\"Voltar\""));
        Assert.Equal(1, Contar(formulario, "OnVoltar=\"Voltar\""));
        Assert.True(Contem(formulario, CopiaComMes));
        Assert.False(Contem(formulario, VoltaSemMes));
        Assert.True(Contem(lista, NovoComMes));
        Assert.True(Contem(lista, EditarComMes));

        var semVolta = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor")
            .Replace(VoltaDoFormulario, "/* " + VoltaDoFormulario + " */", StringComparison.Ordinal));
        Assert.Equal(0, Contar(semVolta, VoltaDoFormulario));

        var semChamada = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor")
            .Replace("Voltar();", "// Voltar();", StringComparison.Ordinal));
        Assert.Equal(0, Contar(semChamada, "Voltar();"));

        var semNovo = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentosList.razor")
            .Replace(NovoComMes, "// " + NovoComMes, StringComparison.Ordinal));
        Assert.False(Contem(semNovo, NovoComMes));

        var semEditar = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentosList.razor")
            .Replace(EditarComMes, "@* " + EditarComMes + " *@", StringComparison.Ordinal));
        Assert.False(Contem(semEditar, EditarComMes));

        var setaTrocada = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor")
            .Replace("OnVoltar=\"Voltar\"", "OnVoltar=\"Sair\"", StringComparison.Ordinal));
        Assert.Equal(0, Contar(setaTrocada, "OnVoltar=\"Voltar\""));

        const string soComentario = """
            @* NavigateTo(ConsultaMesLancamentos.Rota(MesConsulta)) *@
            // Voltar();
            // NavigateTo($"/lancamentos/novo?mes={_mesReferencia.ToString("yyyy-MM", CultureInfo.InvariantCulture)}")
            /* NavigateTo($"/lancamentos/editar/{id}?mes={_mesReferencia.ToString("yyyy-MM", CultureInfo.InvariantCulture)}") */
            <!-- ConsultaMesLancamentos.Rota(MesConsulta, "/lancamentos/novo") -->
            // NavigateTo("/lancamentos")
            @* OnVoltar="Voltar" *@
            OnClick="Voltar"
            """;
        var comentario = SemComentarios(soComentario);
        Assert.False(Contem(comentario, VoltaDoFormulario));
        Assert.False(Contem(comentario, "Voltar();"));
        Assert.False(Contem(comentario, NovoComMes));
        Assert.False(Contem(comentario, EditarComMes));
        Assert.False(Contem(comentario, CopiaComMes));
        Assert.False(Contem(comentario, VoltaSemMes));
        Assert.Equal(0, Contar(comentario, "OnVoltar=\"Voltar\""));
        Assert.True(Contem(comentario, "OnClick=\"Voltar\""));
    }

    private static int Contar(string fonte, string trecho)
    {
        var total = 0;
        var cursor = 0;
        while ((cursor = fonte.IndexOf(trecho, cursor, StringComparison.Ordinal)) >= 0)
        {
            total++;
            cursor += trecho.Length;
        }

        return total;
    }

    private static bool Contem(string fonte, string trecho)
        => fonte.Contains(trecho, StringComparison.Ordinal);

    private static string Ler(params string[] partes)
        => File.ReadAllText(Path.Combine(new[] { Raiz() }.Concat(partes).ToArray()));

    private static string Raiz()
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

    private static string SemComentarios(string fonte)
    {
        var texto = new StringBuilder(fonte.Length);
        for (var i = 0; i < fonte.Length; i++)
        {
            var c = fonte[i];
            if (c == '"')
            {
                texto.Append(c);
                for (i++; i < fonte.Length; i++)
                {
                    texto.Append(fonte[i]);
                    if (fonte[i] == '\\' && i + 1 < fonte.Length)
                    {
                        texto.Append(fonte[++i]);
                        continue;
                    }

                    if (fonte[i] == '"')
                        break;
                }

                continue;
            }

            if (c == '@' && i + 1 < fonte.Length && fonte[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < fonte.Length && !(fonte[i] == '*' && fonte[i + 1] == '@'))
                    i++;
                if (i + 1 < fonte.Length)
                    i++;
                continue;
            }

            if (c == '<' && ComecaCom(fonte, i, "<!--"))
            {
                i += 4;
                while (i + 2 < fonte.Length && !ComecaCom(fonte, i, "-->"))
                    i++;
                if (i + 2 < fonte.Length)
                    i += 2;
                continue;
            }

            if (c == '/' && i + 1 < fonte.Length && fonte[i + 1] == '/')
            {
                while (i < fonte.Length && fonte[i] != '\n')
                    i++;
                if (i < fonte.Length)
                    texto.Append('\n');
                continue;
            }

            if (c == '/' && i + 1 < fonte.Length && fonte[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < fonte.Length && !(fonte[i] == '*' && fonte[i + 1] == '/'))
                    i++;
                if (i + 1 < fonte.Length)
                    i++;
                continue;
            }

            texto.Append(c);
        }

        return texto.ToString();
    }

    private static bool ComecaCom(string fonte, int indice, string trecho)
    {
        if (indice + trecho.Length > fonte.Length)
            return false;

        for (var i = 0; i < trecho.Length; i++)
        {
            if (fonte[indice + i] != trecho[i])
                return false;
        }

        return true;
    }
}
