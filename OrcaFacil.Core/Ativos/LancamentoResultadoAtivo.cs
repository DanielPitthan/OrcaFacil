using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Ativos;

/// <summary>
/// Forma única do lançamento de renda variável: categoria de sistema, pago, sem itens.
/// Quem chama informa o valor. A venda calcula o resultado; o provento usa o líquido.
/// </summary>
public static class LancamentoResultadoAtivo
{
    public const string CategoriaGanho = "Ganho realizado em ativos";
    public const string CategoriaPerda = "Perda realizada em ativos";
    public const string CategoriaProventos = "Proventos";

    public static Task<Categoria> ObterOuCriarCategoriaAsync(
        AppDbContext context,
        TipoTransacao tipo,
        CancellationToken cancellationToken)
    {
        var receita = tipo == TipoTransacao.Receita;
        return ObterOuCriarAsync(
            context,
            receita ? CategoriaGanho : CategoriaPerda,
            tipo,
            receita ? "📈" : "📉",
            receita ? "#6DA538" : "#659ECD",
            cancellationToken);
    }

    public static Task<Categoria> ObterOuCriarCategoriaProventosAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
        => ObterOuCriarAsync(
            context, CategoriaProventos, TipoTransacao.Receita, "💵", "#6DA538", cancellationToken);

    private static async Task<Categoria> ObterOuCriarAsync(
        AppDbContext context,
        string nome,
        TipoTransacao tipo,
        string icone,
        string corHex,
        CancellationToken cancellationToken)
    {
        var existente = context.Categorias.Local.FirstOrDefault(c => string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase))
            ?? await context.Categorias.FirstOrDefaultAsync(c => c.Nome == nome, cancellationToken);
        if (existente is not null)
            return existente;

        var nova = new Categoria
        {
            Nome = nome,
            Icone = icone,
            CorHex = corHex,
            TipoPadrao = tipo,
            EhPadraoDoSistema = true,
            PermiteItensFatura = false,
            Ativo = true
        };
        context.Categorias.Add(nova);
        return nova;
    }

    public static Transacao Criar(
        int membroFamiliaId,
        decimal valor,
        DateTime data,
        TipoTransacao tipo,
        Categoria categoria,
        string descricao,
        string observacao) => new()
    {
        Descricao = descricao,
        Valor = valor,
        Data = data.Date,
        Tipo = tipo,
        Recorrencia = TipoRecorrencia.Unica,
        Pago = true,
        Observacao = observacao,
        MembroFamiliaId = membroFamiliaId,
        Categoria = categoria
    };

    public static void Aplicar(
        Transacao lancamento,
        decimal valor,
        DateTime data,
        TipoTransacao tipo,
        Categoria categoria,
        string descricao,
        string observacao)
    {
        lancamento.Descricao = descricao;
        lancamento.Valor = valor;
        lancamento.Data = data.Date;
        lancamento.Tipo = tipo;
        lancamento.Recorrencia = TipoRecorrencia.Unica;
        lancamento.Pago = true;
        lancamento.Observacao = observacao;
        if (categoria.Id == 0)
            lancamento.Categoria = categoria;
        else
            lancamento.CategoriaId = categoria.Id;
        lancamento.AtualizadoEm = DateTime.UtcNow;
    }

    public static bool Igual(
        Transacao lancamento,
        decimal valor,
        DateTime data,
        TipoTransacao tipo,
        Categoria categoria,
        string descricao,
        string observacao)
    {
        var categoriaIgual = categoria.Id == 0
            ? ReferenceEquals(lancamento.Categoria, categoria)
            : lancamento.CategoriaId == categoria.Id;

        return lancamento.Valor == valor
            && lancamento.Tipo == tipo
            && lancamento.Data.Date == data.Date
            && lancamento.Descricao == descricao
            && categoriaIgual
            && lancamento.Pago
            && lancamento.Recorrencia == TipoRecorrencia.Unica
            && lancamento.Observacao == observacao;
    }
}
