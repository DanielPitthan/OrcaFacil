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
    [InlineData(false)]
    [InlineData(true)]
    public void ReceitaNova_NaoNascePaga_PagoEDoUsuario(bool pagoEscolhido)
    {
        var modelo = new TransacaoFormModel
        {
            Descricao = "Salário",
            CategoriaId = 8,
            Tipo = TipoTransacao.Despesa,
            Pago = pagoEscolhido,
            Recorrencia = TipoRecorrencia.Mensal
        };

        modelo.Tipo = FluxoLancamento.TipoNaCriacao(TipoTransacao.Receita);

        Assert.Equal(TipoTransacao.Receita, modelo.Tipo);
        Assert.Equal(pagoEscolhido, modelo.Pago);
        Assert.Equal(TipoRecorrencia.Mensal, modelo.Recorrencia);
    }

    [Fact]
    public void EditarSemTrocarCategoria_MantemTipoEPagoGravados()
    {
        const int moradia = 4;
        var tipo = FluxoLancamento.TipoNaEdicao(
            categoriaGravadaId: moradia,
            categoriaInformadaId: moradia,
            tipoGravado: TipoTransacao.Despesa,
            tipoPadraoInformado: TipoTransacao.Receita);

        Assert.Equal(TipoTransacao.Despesa, tipo);
    }

    [Fact]
    public void TrocarCategoria_SoMudaOTipo()
    {
        var modelo = new TransacaoFormModel
        {
            CategoriaId = 4,
            Tipo = TipoTransacao.Despesa,
            Pago = false,
            Recorrencia = TipoRecorrencia.Mensal
        };
        var pago = modelo.Pago;
        var recorrencia = modelo.Recorrencia;

        modelo.CategoriaId = 8;
        modelo.Tipo = FluxoLancamento.TipoNaEdicao(4, modelo.CategoriaId, TipoTransacao.Despesa, TipoTransacao.Receita);

        Assert.Equal(TipoTransacao.Receita, modelo.Tipo);
        Assert.Equal(pago, modelo.Pago);
        Assert.Equal(recorrencia, modelo.Recorrencia);
        Assert.False(modelo.Pago);
        Assert.Equal(TipoRecorrencia.Mensal, modelo.Recorrencia);
    }

    [Fact]
    public void CriarEAtualizar_GravamOTipoDaCategoria_EMantemObservacao()
    {
        var fonte = File.ReadAllText(Path.Combine(Raiz(), "OrcaFacil", "Services", "TransacaoService.cs"));
        var criar = ExtrairBloco(fonte, "CriarAsync(");
        var atualizar = ExtrairBloco(fonte, "AtualizarAsync(");

        var criaTipo = criar.IndexOf("dto.Tipo = FluxoLancamento.TipoNaCriacao(categoria.TipoPadrao)", StringComparison.Ordinal);
        Assert.True(criaTipo >= 0);
        Assert.True(criar.IndexOf("MapearItensAsync(dto, categoria)", StringComparison.Ordinal) > criaTipo);
        Assert.True(criar.IndexOf("Tipo = dto.Tipo", StringComparison.Ordinal) > criaTipo);
        Assert.Contains("Pago = dto.Pago", criar, StringComparison.Ordinal);
        Assert.DoesNotContain("dto.Tipo == TipoTransacao.Despesa ? dto.Pago : true", criar, StringComparison.Ordinal);

        var editaTipo = atualizar.IndexOf("dto.Tipo = FluxoLancamento.TipoNaEdicao(", StringComparison.Ordinal);
        Assert.True(editaTipo >= 0);
        Assert.Contains("transacao.CategoriaId", atualizar, StringComparison.Ordinal);
        Assert.Contains("transacao.Tipo", atualizar, StringComparison.Ordinal);
        Assert.Contains("categoria.TipoPadrao", atualizar, StringComparison.Ordinal);
        Assert.True(atualizar.IndexOf("MapearItensAsync(dto, categoria)", StringComparison.Ordinal) > editaTipo);
        Assert.Contains("transacao.Pago = dto.Pago", atualizar, StringComparison.Ordinal);
        Assert.DoesNotContain("TipoNaCriacao", atualizar, StringComparison.Ordinal);
        Assert.DoesNotContain("dto.Tipo == TipoTransacao.Despesa ? dto.Pago : true", atualizar, StringComparison.Ordinal);

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
        Assert.Equal(2, Contar(marcacao, "Label=\"Pago\""));
        Assert.DoesNotContain("TipoPadrao == TipoTransacao.Despesa", marcacao, StringComparison.Ordinal);
        Assert.DoesNotContain("_modelo.Pago = true", formulario, StringComparison.Ordinal);

        var sessao = ExtrairBloco(formulario, "Task AbrirSessaoAsync()");
        var aplicarNoCarregar = sessao.IndexOf("AplicarTipoDaCategoria()", StringComparison.Ordinal);
        var guardaNovo = sessao.IndexOf("if (!Id.HasValue)", StringComparison.Ordinal);
        Assert.True(guardaNovo >= 0);
        Assert.True(aplicarNoCarregar > guardaNovo);
        var sucesso = ExtrairChaves(sessao, sessao.IndexOf('{', sessao.IndexOf("transacao is not null", StringComparison.Ordinal)));
        Assert.DoesNotContain("AplicarTipoDaCategoria", sucesso, StringComparison.Ordinal);

        var troca = ExtrairBloco(formulario, "void OnCategoriaChanged(");
        Assert.Contains("FluxoLancamento.TipoNaEdicao(", troca, StringComparison.Ordinal);
        Assert.DoesNotContain("_modelo.Pago", troca, StringComparison.Ordinal);
        Assert.DoesNotContain("Recorrencia", troca, StringComparison.Ordinal);
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
