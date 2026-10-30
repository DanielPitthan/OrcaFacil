using System.ComponentModel.DataAnnotations;
using System.Text;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Tests.Lancamentos;

public class LancamentoCategoriaFormTests
{
    [Fact]
    public void CategoriaEmBranco_NaoPassaNaValidacao()
    {
        var modelo = new TransacaoFormModel
        {
            Descricao = "Mercado",
            Valor = 12.5m,
            Data = new DateTime(2026, 10, 26),
            CategoriaId = 0
        };

        var erros = new List<ValidationResult>();
        var valido = Validator.TryValidateObject(modelo, new ValidationContext(modelo), erros, validateAllProperties: true);

        Assert.False(valido);
        var categoria = Assert.Single(erros, erro => erro.MemberNames.Contains(nameof(TransacaoFormModel.CategoriaId)));
        Assert.Equal("Selecione uma categoria", categoria.ErrorMessage);
    }

    [Theory]
    [InlineData(TipoTransacao.Despesa, TipoTransacao.Receita)]
    [InlineData(TipoTransacao.Receita, TipoTransacao.Despesa)]
    public void TipoGravado_SegueACategoria_EDescartaOTipoDoFormulario(TipoTransacao tipoPadrao, TipoTransacao tipoDoFormulario)
    {
        Assert.NotEqual(tipoDoFormulario, tipoPadrao);
        Assert.Equal(tipoPadrao, FluxoLancamento.TipoGravado(tipoPadrao));
    }

    [Fact]
    public void CriarEAtualizar_GravamOTipoDaCategoria_EMantemObservacao()
    {
        var fonte = File.ReadAllText(Path.Combine(Raiz(), "OrcaFacil", "Services", "TransacaoService.cs"));

        foreach (var metodo in new[] { "CriarAsync", "AtualizarAsync" })
        {
            var corpo = ExtrairBloco(fonte, metodo + "(");
            var deriva = corpo.IndexOf("dto.Tipo = FluxoLancamento.TipoGravado(categoria.TipoPadrao)", StringComparison.Ordinal);
            var mapeia = corpo.IndexOf("MapearItensAsync(dto, categoria)", StringComparison.Ordinal);
            var grava = corpo.IndexOf("Tipo = dto.Tipo", StringComparison.Ordinal);

            Assert.True(deriva >= 0, metodo);
            Assert.True(mapeia > deriva, metodo);
            Assert.True(grava > deriva, metodo);
        }

        Assert.Contains("Observacao = dto.Observacao", fonte, StringComparison.Ordinal);
        Assert.Contains("transacao.Observacao = dto.Observacao", fonte, StringComparison.Ordinal);
        Assert.NotNull(typeof(Transacao).GetProperty(nameof(Transacao.Observacao)));
    }

    [Fact]
    public void Formularios_ComecamPorCategoriaDescricaoEData_SemTipoNemObservacao()
    {
        var formulario = File.ReadAllText(Path.Combine(
            Raiz(), "OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor"));
        var painel = File.ReadAllText(Path.Combine(
            Raiz(), "OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor"));
        var marcacao = SemComentarios(AntesDoCodigo(formulario));
        var marcacaoPainel = SemComentarios(AntesDoCodigo(painel));
        var papeis = Trechos(marcacao, "<MudPaper", "</MudPaper>");

        Assert.Equal(2, papeis.Count);
        foreach (var papel in papeis)
        {
            var categoria = papel.IndexOf("Label=\"Categoria\"", StringComparison.Ordinal);
            var descricao = papel.IndexOf("Label=\"Descrição\"", StringComparison.Ordinal);
            var data = papel.IndexOf("Label=\"Data\"", StringComparison.Ordinal);
            var valor = papel.IndexOf("Label=\"Valor\"", StringComparison.Ordinal);

            Assert.True(categoria >= 0);
            Assert.True(categoria < descricao);
            Assert.True(descricao < data);
            Assert.True(data < valor);
            Assert.DoesNotContain("Label=\"Tipo\"", papel, StringComparison.Ordinal);
            Assert.DoesNotContain("Observação", papel, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("MudSelect T=\"TipoTransacao\"", marcacao, StringComparison.Ordinal);
        Assert.DoesNotContain("Label=\"Tipo\"", marcacao, StringComparison.Ordinal);
        Assert.DoesNotContain("Observação", marcacao, StringComparison.Ordinal);
        Assert.DoesNotContain("Observação", marcacaoPainel, StringComparison.Ordinal);
        Assert.DoesNotContain("Label=\"Tipo\"", marcacaoPainel, StringComparison.Ordinal);
        Assert.Contains("_modelo.Observacao = transacao.Observacao", formulario, StringComparison.Ordinal);

        var filtro = formulario[formulario.IndexOf("CategoriasFiltradas =>", StringComparison.Ordinal)..];
        filtro = filtro[..filtro.IndexOf(';', StringComparison.Ordinal)];
        Assert.Contains("c.Ativo", filtro, StringComparison.Ordinal);
        Assert.DoesNotContain("_modelo.Tipo", filtro, StringComparison.Ordinal);

        var garantia = ExtrairBloco(formulario, "void GarantirCategoriaValida()");
        var emBranco = garantia.IndexOf("if (_modelo.CategoriaId <= 0)", StringComparison.Ordinal);
        var inicial = garantia.IndexOf("CategoriaInicial", StringComparison.Ordinal);
        Assert.True(emBranco >= 0);
        Assert.True(emBranco < inicial);
    }

    private static string AntesDoCodigo(string fonte)
    {
        var indice = fonte.IndexOf("@code", StringComparison.Ordinal);
        Assert.True(indice > 0);
        return fonte[..indice];
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

    private static List<string> Trechos(string fonte, string inicio, string fim)
    {
        var lista = new List<string>();
        var cursor = 0;
        while (true)
        {
            var abre = fonte.IndexOf(inicio, cursor, StringComparison.Ordinal);
            if (abre < 0)
                return lista;

            var fecha = fonte.IndexOf(fim, abre, StringComparison.Ordinal);
            Assert.True(fecha > abre);
            lista.Add(fonte[abre..(fecha + fim.Length)]);
            cursor = fecha + fim.Length;
        }
    }

    private static string SemComentarios(string fonte)
    {
        var texto = new StringBuilder(fonte.Length);
        for (var i = 0; i < fonte.Length; i++)
        {
            if (fonte[i] == '@' && i + 1 < fonte.Length && fonte[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < fonte.Length && !(fonte[i] == '*' && fonte[i + 1] == '@'))
                    i++;
                if (i + 1 < fonte.Length)
                    i++;
                continue;
            }

            if (i + 3 < fonte.Length && fonte[i] == '<' && fonte[i + 1] == '!' && fonte[i + 2] == '-' && fonte[i + 3] == '-')
            {
                i += 4;
                while (i + 2 < fonte.Length && !(fonte[i] == '-' && fonte[i + 1] == '-' && fonte[i + 2] == '>'))
                    i++;
                if (i + 2 < fonte.Length)
                    i += 2;
                continue;
            }

            texto.Append(fonte[i]);
        }

        return texto.ToString();
    }

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
}
