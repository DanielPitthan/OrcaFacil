using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Tests.Lancamentos;

public class FluxoLancamentoTests
{
    private static readonly FluxoLancamento.CategoriaDeLancamento[] Categorias =
    [
        new(1, "Cartão de Crédito", true, TipoTransacao.Despesa),
        new(2, "Moradia", false, TipoTransacao.Despesa),
        new(4, "Saúde", false, TipoTransacao.Despesa),
        new(10, "Energia", false, TipoTransacao.Despesa),
        new(8, "Renda", false, TipoTransacao.Receita)
    ];

    [Fact]
    public void EscolherCartao_NaoPrendeARecorrenciaEmUnica()
    {
        var modelo = new TransacaoFormModel { Recorrencia = TipoRecorrencia.Mensal };

        FluxoLancamento.EscolherCartao(modelo, 11);
        Assert.Equal(11, modelo.CartaoId);
        Assert.Equal(TipoRecorrencia.Mensal, modelo.Recorrencia);

        FluxoLancamento.EscolherCartao(modelo, 22);
        Assert.Equal(22, modelo.CartaoId);
        Assert.Equal(TipoRecorrencia.Mensal, modelo.Recorrencia);

        FluxoLancamento.EscolherRecorrencia(modelo, TipoRecorrencia.Semanal);
        Assert.Equal(22, modelo.CartaoId);
        Assert.Equal(TipoRecorrencia.Semanal, modelo.Recorrencia);

        FluxoLancamento.EscolherRecorrencia(modelo, TipoRecorrencia.Anual);
        Assert.Equal(22, modelo.CartaoId);
        Assert.Equal(TipoRecorrencia.Anual, modelo.Recorrencia);
    }

    [Fact]
    public void DoisCartoes_ItauEBradesco_SaoEscolhasDistintas()
    {
        const int itau = 11;
        const int bradesco = 22;
        var disponiveis = new List<int> { itau, bradesco };
        var modelo = new TransacaoFormModel
        {
            Recorrencia = TipoRecorrencia.Mensal,
            CartaoId = FluxoLancamento.CartaoInicial(null, disponiveis)
        };

        Assert.Equal(itau, modelo.CartaoId);

        FluxoLancamento.EscolherCartao(modelo, bradesco);
        modelo.CartaoId = FluxoLancamento.CartaoInicial(modelo.CartaoId, disponiveis);
        Assert.Equal(bradesco, modelo.CartaoId);
        Assert.NotEqual(itau, modelo.CartaoId);

        FluxoLancamento.EscolherCartao(modelo, itau);
        modelo.CartaoId = FluxoLancamento.CartaoInicial(modelo.CartaoId, disponiveis);
        Assert.Equal(itau, modelo.CartaoId);
        Assert.Equal(TipoRecorrencia.Mensal, modelo.Recorrencia);
    }

    [Theory]
    [InlineData("Energia", 10)]
    [InlineData("Saúde", 4)]
    [InlineData("Moradia", 2)]
    public void DepoisDoCartao_CadaTipoVoltaParaAEscolhaSemDadosDoCartao(string nome, int categoriaId)
    {
        var modelo = LancamentoDeCartao();
        var hoje = new DateTime(2026, 10, 19);

        FluxoLancamento.IniciarNovo(modelo, hoje);

        Assert.Null(modelo.Id);
        Assert.Equal(0m, modelo.Valor);
        Assert.Equal(hoje, modelo.Data);
        Assert.Null(modelo.CartaoId);
        Assert.Equal(0, modelo.CategoriaId);
        Assert.Equal(TipoTransacao.Despesa, modelo.Tipo);
        Assert.Equal(TipoRecorrencia.Unica, modelo.Recorrencia);
        Assert.Empty(modelo.Itens);
        Assert.Equal(string.Empty, modelo.Descricao);
        Assert.True(FluxoLancamento.MostraEscolhaDeTipo(modelo.Tipo, nomeCategoria: null, permiteItensFatura: false));
        Assert.NotEqual(1, FluxoLancamento.CategoriaInicial(Categorias, modelo.Tipo));

        var categoria = Categorias.Single(c => c.Id == categoriaId);
        Assert.Equal(nome, categoria.Nome);
        FluxoLancamento.EscolherCategoria(modelo, categoria.Id, ehCartao: false);

        Assert.Equal(categoriaId, modelo.CategoriaId);
        Assert.Null(modelo.CartaoId);
        Assert.Empty(modelo.Itens);
        Assert.Equal(TipoRecorrencia.Unica, modelo.Recorrencia);
        Assert.True(FluxoLancamento.MostraEscolhaDeTipo(modelo.Tipo, categoria.Nome, categoria.PermiteItensFatura));
        Assert.False(FluxoLancamento.EhLancamentoDeCartao(modelo.Tipo, categoria.Nome, categoria.PermiteItensFatura));
    }

    [Fact]
    public void CategoriaInicial_NaoAbreAFaturaQuandoOCartaoESoPrimeiroDaLista()
    {
        var id = FluxoLancamento.CategoriaInicial(Categorias, TipoTransacao.Despesa);
        var categoria = Categorias.Single(c => c.Id == id);

        Assert.Equal("Moradia", categoria.Nome);
        Assert.True(FluxoLancamento.MostraEscolhaDeTipo(TipoTransacao.Despesa, categoria.Nome, categoria.PermiteItensFatura));
    }

    private static TransacaoFormModel LancamentoDeCartao() => new()
    {
        Id = 42,
        Descricao = "Fatura Itaú",
        Valor = 180m,
        Data = new DateTime(2026, 10, 1),
        Tipo = TipoTransacao.Despesa,
        Recorrencia = TipoRecorrencia.Mensal,
        Pago = true,
        Observacao = "fecha dia 10",
        CategoriaId = 1,
        CartaoId = 11,
        Itens =
        [
            new ItemLancamentoFormModel
            {
                Descricao = CartaoRepository.NomeCategoriaCartaoCredito,
                Valor = 80m,
                CategoriaId = 10
            }
        ]
    };
}
