using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Data;

/// <summary>
/// O que o app grava sozinho na instalação: categorias padrão, o perfil "Usuário"
/// e os lançamentos, orçamentos e metas de demonstração.
/// Quem decide se isso já é dado do usuário é
/// <see cref="OrcaFacil.Core.Armazenamento.PoliticaSemeadura.HaDadoDoUsuarioAsync"/>:
/// categoria padrão não conta; perfil, lançamento, orçamento, meta e log contam.
/// </summary>
public static class CatalogoDemonstracao
{
    public const string NomePerfilPadrao = "Usuário";

    public const string AvatarPerfilPadrao = "👤";

    public static IReadOnlyList<CategoriaPadrao> Categorias { get; } =
    [
        new("Alimentação", "🛒", "#B2CFE6", TipoTransacao.Despesa, false),
        new("Moradia", "🏠", "#659ECD", TipoTransacao.Despesa, false),
        new("Transporte", "🚗", "#A2B94F", TipoTransacao.Despesa, false),
        new("Saúde", "🏥", "#6DA538", TipoTransacao.Despesa, false),
        new("Lazer", "🎉", "#A2B94F", TipoTransacao.Despesa, false),
        new("Educação", "🎓", "#A2B94F", TipoTransacao.Despesa, false),
        new("Outros", "📦", "#A2B94F", TipoTransacao.Despesa, false),
        new(CestaDespesaRegras.NomeCategoria, "🧺", "#6DA538", TipoTransacao.Despesa, true),
        new("Cartão de Crédito", "💳", "#B2CFE6", TipoTransacao.Despesa, true),
        new("Impostos e Taxas", "🧾", "#659ECD", TipoTransacao.Despesa, false),
        new("Investimentos e Previdência", "📈", "#6DA538", TipoTransacao.Despesa, false),
        new("Renda", "💰", "#6DA538", TipoTransacao.Receita, false)
    ];

    public static IReadOnlyList<LancamentoDemonstracao> Lancamentos { get; } =
    [
        new("Salário", 6500m, TipoTransacao.Receita, TipoRecorrencia.Mensal, "Renda", 4),
        new("Freelance", 850m, TipoTransacao.Receita, TipoRecorrencia.Unica, "Renda", 17),
        new("Supermercado", 620.40m, TipoTransacao.Despesa, TipoRecorrencia.Mensal, "Alimentação", 6),
        new("Feira", 95.30m, TipoTransacao.Despesa, TipoRecorrencia.Semanal, "Alimentação", 20),
        new("Condomínio", 580m, TipoTransacao.Despesa, TipoRecorrencia.Mensal, "Moradia", 9),
        new("Energia elétrica", 210.55m, TipoTransacao.Despesa, TipoRecorrencia.Mensal, "Moradia", 10),
        new("Internet", 130m, TipoTransacao.Despesa, TipoRecorrencia.Mensal, "Moradia", 11),
        new("Combustível", 340m, TipoTransacao.Despesa, TipoRecorrencia.Unica, "Transporte", 8),
        new("Parcela do carro", 780m, TipoTransacao.Despesa, TipoRecorrencia.Mensal, "Transporte", 5),
        new("Plano de saúde", 450m, TipoTransacao.Despesa, TipoRecorrencia.Mensal, "Saúde", 3),
        new("Farmácia", 68.90m, TipoTransacao.Despesa, TipoRecorrencia.Unica, "Saúde", 22),
        new("Cinema", 85m, TipoTransacao.Despesa, TipoRecorrencia.Unica, "Lazer", 15),
        new("Streaming", 55.90m, TipoTransacao.Despesa, TipoRecorrencia.Mensal, "Lazer", 2),
        new("Curso online", 199m, TipoTransacao.Despesa, TipoRecorrencia.Mensal, "Educação", 13),
        new("Fatura cartão", 1240m, TipoTransacao.Despesa, TipoRecorrencia.Mensal, "Cartão de Crédito", 18),
        new("DAS / Impostos", 320m, TipoTransacao.Despesa, TipoRecorrencia.Mensal, "Impostos e Taxas", 12),
        new("Aporte previdência privada", 400m, TipoTransacao.Despesa, TipoRecorrencia.Mensal, "Investimentos e Previdência", 7),
        new("Diversos", 60m, TipoTransacao.Despesa, TipoRecorrencia.Unica, "Outros", 24)
    ];

    public static IReadOnlyList<OrcamentoDemonstracao> Orcamentos { get; } =
    [
        new("Alimentação", 800m),
        new("Moradia", 1500m),
        new("Transporte", 1000m),
        new("Lazer", 300m),
        new("Saúde", 600m)
    ];

    public static IReadOnlyList<MetaDemonstracao> Metas { get; } =
    [
        new("Viagem de férias", 5000m, 1200m, "✈️", 6),
        new("Reserva de emergência", 10000m, 3000m, "🛟", 12)
    ];

    public static List<Categoria> CriarCategorias()
    {
        var lista = new List<Categoria>(Categorias.Count);
        foreach (var padrao in Categorias)
        {
            lista.Add(new Categoria
            {
                Nome = padrao.Nome,
                Icone = padrao.Icone,
                CorHex = padrao.CorHex,
                TipoPadrao = padrao.TipoPadrao,
                EhPadraoDoSistema = true,
                PermiteItensFatura = padrao.PermiteItensFatura
            });
        }

        return lista;
    }

    public static MembroFamilia CriarPerfil()
        => new()
        {
            Nome = NomePerfilPadrao,
            Avatar = AvatarPerfilPadrao,
            Ativo = true,
            Role = PerfilRole.Admin
        };

    /// <summary>
    /// Acrescenta a demonstração. O perfil e as categorias padrão já precisam estar gravados.
    /// O mês de referência é <paramref name="hoje"/>, como na primeira abertura do app.
    /// </summary>
    public static void AdicionarDemonstracao(AppDbContext ctx, MembroFamilia perfil, DateTime hoje)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(perfil);
        if (perfil.Id == 0)
            throw new ArgumentException("O perfil precisa estar gravado para receber a demonstração.", nameof(perfil));

        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var categoria in ctx.Categorias)
            ids[categoria.Nome] = categoria.Id;

        var mesAtual = new DateTime(hoje.Year, hoje.Month, 1);
        foreach (var mesRef in new[] { mesAtual.AddMonths(-2), mesAtual.AddMonths(-1), mesAtual })
        {
            foreach (var item in Lancamentos)
            {
                ctx.Transacoes.Add(new Transacao
                {
                    Descricao = item.Descricao,
                    Valor = item.Valor,
                    Data = mesRef.AddDays(item.Dia),
                    Tipo = item.Tipo,
                    Recorrencia = item.Recorrencia,
                    CategoriaId = Id(ids, item.Categoria),
                    MembroFamiliaId = perfil.Id
                });
            }
        }

        foreach (var item in Orcamentos)
        {
            ctx.Orcamentos.Add(new Orcamento
            {
                CategoriaId = Id(ids, item.Categoria),
                ValorLimite = item.ValorLimite,
                Mes = mesAtual.Month,
                Ano = mesAtual.Year
            });
        }

        foreach (var item in Metas)
        {
            ctx.Metas.Add(new Meta
            {
                Nome = item.Nome,
                ValorAlvo = item.ValorAlvo,
                ValorAtual = item.ValorAtual,
                Prazo = hoje.AddMonths(item.PrazoMeses),
                Icone = item.Icone,
                MembroFamiliaId = perfil.Id
            });
        }
    }

    private static int Id(Dictionary<string, int> ids, string nome)
        => ids.TryGetValue(nome, out var id)
            ? id
            : throw new InvalidOperationException("Categoria padrão ausente.");

    public readonly record struct CategoriaPadrao(
        string Nome,
        string Icone,
        string CorHex,
        TipoTransacao TipoPadrao,
        bool PermiteItensFatura);

    public readonly record struct LancamentoDemonstracao(
        string Descricao,
        decimal Valor,
        TipoTransacao Tipo,
        TipoRecorrencia Recorrencia,
        string Categoria,
        int Dia);

    public readonly record struct OrcamentoDemonstracao(string Categoria, decimal ValorLimite);

    public readonly record struct MetaDemonstracao(
        string Nome,
        decimal ValorAlvo,
        decimal ValorAtual,
        string Icone,
        int PrazoMeses);
}
