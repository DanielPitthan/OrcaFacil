using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Core.Lancamentos;

/// <summary>
/// Abertura de um lançamento e escolhas do cartão que não podem vazar para o próximo.
/// A tela de cartão é só a categoria "Cartão de Crédito" com itens de fatura.
/// Sem a categoria de cartão, o lançamento fica na tela simples, sem cartão nem recorrência herdada.
/// O tipo gravado é o tipo padrão da categoria.
/// </summary>
public static class FluxoLancamento
{
    public readonly record struct CategoriaDeLancamento(int Id, string Nome, bool PermiteItensFatura, TipoTransacao Tipo);

    /// <summary>O formulário não escolhe receita ou despesa. Vale o tipo da categoria.</summary>
    public static TipoTransacao TipoGravado(TipoTransacao tipoPadrao) => tipoPadrao;

    public static bool EhLancamentoDeCartao(TipoTransacao tipo, string? nomeCategoria, bool permiteItensFatura)
        => tipo == TipoTransacao.Despesa
           && permiteItensFatura
           && string.Equals(nomeCategoria, CartaoRepository.NomeCategoriaCartaoCredito, StringComparison.OrdinalIgnoreCase);

    public static bool MostraEscolhaDeTipo(TipoTransacao tipo, string? nomeCategoria, bool permiteItensFatura)
        => !EhLancamentoDeCartao(tipo, nomeCategoria, permiteItensFatura);

    /// <summary>Zera tipo, cartão, recorrência e itens. O próximo lançamento não herda a fatura anterior.</summary>
    public static void IniciarNovo(TransacaoFormModel modelo, DateTime hoje)
    {
        modelo.Id = null;
        modelo.Descricao = string.Empty;
        modelo.Valor = 0;
        modelo.Data = hoje.Date;
        modelo.Tipo = TipoTransacao.Despesa;
        modelo.Recorrencia = TipoRecorrencia.Unica;
        modelo.Pago = false;
        modelo.Observacao = null;
        modelo.CategoriaId = 0;
        modelo.CartaoId = null;
        modelo.Itens = [];
    }

    /// <summary>Primeira categoria do tipo que não abre direto a fatura do cartão.</summary>
    public static int CategoriaInicial(IReadOnlyList<CategoriaDeLancamento> categorias, TipoTransacao tipo)
    {
        var doTipo = categorias.Where(c => c.Tipo == tipo).ToList();
        if (doTipo.Count == 0)
            return 0;

        foreach (var categoria in doTipo)
        {
            if (!EhLancamentoDeCartao(tipo, categoria.Nome, categoria.PermiteItensFatura))
                return categoria.Id;
        }

        return doTipo[0].Id;
    }

    public static void EscolherCategoria(TransacaoFormModel modelo, int categoriaId, bool ehCartao)
    {
        modelo.CategoriaId = categoriaId;
        if (ehCartao)
            return;

        modelo.CartaoId = null;
        modelo.Itens = [];
    }

    /// <summary>Troca o cartão e preserva a recorrência que a pessoa já escolheu.</summary>
    public static void EscolherCartao(TransacaoFormModel modelo, int cartaoId)
        => modelo.CartaoId = cartaoId;

    /// <summary>Troca a recorrência e preserva o cartão já escolhido.</summary>
    public static void EscolherRecorrencia(TransacaoFormModel modelo, TipoRecorrencia recorrencia)
        => modelo.Recorrencia = recorrencia;

    /// <summary>
    /// Mantém o cartão escolhido quando ele ainda existe. Sem escolha, usa o primeiro da lista.
    /// Não altera a recorrência: ela não é derivada do cartão.
    /// </summary>
    public static int? CartaoInicial(int? atual, IReadOnlyList<int> idsDisponiveis)
    {
        if (atual is int id && idsDisponiveis.Contains(id))
            return id;

        return idsDisponiveis.Count == 0 ? null : idsDisponiveis[0];
    }
}
