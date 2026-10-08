using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Data;

/// <summary>
/// Popula categorias padrão, o perfil único e dados de demonstração (fictícios) no primeiro uso do app.
/// Os valores aqui NÃO representam dados financeiros reais de nenhum usuário.
/// </summary>
public static class SeedData
{
    public static async Task PopularAsync(AppDbContext ctx)
    {
        var perfil = await PopularEssencialAsync(ctx);
        await PopularDemonstracaoAsync(ctx, perfil);
    }

    /// <summary>Mínimo para o app funcionar: categorias padrão e o perfil do usuário.</summary>
    public static async Task<MembroFamilia> PopularEssencialAsync(AppDbContext ctx)
    {
        ctx.Categorias.AddRange(CriarCategoriasPadrao());

        var perfil = new MembroFamilia { Nome = "Usuário", Avatar = "👤", Ativo = true };
        ctx.MembrosFamilia.Add(perfil);

        await ctx.SaveChangesAsync();
        return perfil;
    }

    public static async Task PopularDemonstracaoAsync(AppDbContext ctx, MembroFamilia perfil)
    {
        var categorias = ctx.Categorias.ToList();
        Categoria Cat(string nome) => categorias.First(c => c.Nome == nome);

        var hoje = DateTime.Today;
        var mesAtual = new DateTime(hoje.Year, hoje.Month, 1);
        var mesAnterior = mesAtual.AddMonths(-1);
        var mesRetrasado = mesAtual.AddMonths(-2);

        var transacoes = new List<Transacao>();

        foreach (var mesRef in new[] { mesRetrasado, mesAnterior, mesAtual })
        {
            transacoes.AddRange(new[]
            {
                new Transacao { Descricao = "Salário", Valor = 6500m, Data = mesRef.AddDays(4), Tipo = TipoTransacao.Receita, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Renda").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Freelance", Valor = 850m, Data = mesRef.AddDays(17), Tipo = TipoTransacao.Receita, Recorrencia = TipoRecorrencia.Unica, CategoriaId = Cat("Renda").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Supermercado", Valor = 620.40m, Data = mesRef.AddDays(6), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Alimentação").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Feira", Valor = 95.30m, Data = mesRef.AddDays(20), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Semanal, CategoriaId = Cat("Alimentação").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Condomínio", Valor = 580m, Data = mesRef.AddDays(9), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Moradia").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Energia elétrica", Valor = 210.55m, Data = mesRef.AddDays(10), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Moradia").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Internet", Valor = 130m, Data = mesRef.AddDays(11), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Moradia").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Combustível", Valor = 340m, Data = mesRef.AddDays(8), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Unica, CategoriaId = Cat("Transporte").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Parcela do carro", Valor = 780m, Data = mesRef.AddDays(5), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Transporte").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Plano de saúde", Valor = 450m, Data = mesRef.AddDays(3), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Saúde").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Farmácia", Valor = 68.90m, Data = mesRef.AddDays(22), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Unica, CategoriaId = Cat("Saúde").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Cinema", Valor = 85m, Data = mesRef.AddDays(15), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Unica, CategoriaId = Cat("Lazer").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Streaming", Valor = 55.90m, Data = mesRef.AddDays(2), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Lazer").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Curso online", Valor = 199m, Data = mesRef.AddDays(13), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Educação").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Fatura cartão", Valor = 1240m, Data = mesRef.AddDays(18), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Cartão de Crédito").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "DAS / Impostos", Valor = 320m, Data = mesRef.AddDays(12), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Impostos e Taxas").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Aporte previdência privada", Valor = 400m, Data = mesRef.AddDays(7), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = Cat("Investimentos e Previdência").Id, MembroFamiliaId = perfil.Id },
                new Transacao { Descricao = "Diversos", Valor = 60m, Data = mesRef.AddDays(24), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Unica, CategoriaId = Cat("Outros").Id, MembroFamiliaId = perfil.Id },
            });
        }

        ctx.Transacoes.AddRange(transacoes);
        await ctx.SaveChangesAsync();

        var orcamentos = new List<Orcamento>
        {
            new() { CategoriaId = Cat("Alimentação").Id, ValorLimite = 800m, Mes = mesAtual.Month, Ano = mesAtual.Year },
            new() { CategoriaId = Cat("Moradia").Id, ValorLimite = 1500m, Mes = mesAtual.Month, Ano = mesAtual.Year },
            new() { CategoriaId = Cat("Transporte").Id, ValorLimite = 1000m, Mes = mesAtual.Month, Ano = mesAtual.Year },
            new() { CategoriaId = Cat("Lazer").Id, ValorLimite = 300m, Mes = mesAtual.Month, Ano = mesAtual.Year },
            new() { CategoriaId = Cat("Saúde").Id, ValorLimite = 600m, Mes = mesAtual.Month, Ano = mesAtual.Year },
        };
        ctx.Orcamentos.AddRange(orcamentos);
        await ctx.SaveChangesAsync();

        var metas = new List<Meta>
        {
            new() { Nome = "Viagem de férias", ValorAlvo = 5000m, ValorAtual = 1200m, Prazo = hoje.AddMonths(6), Icone = "✈️", MembroFamiliaId = perfil.Id },
            new() { Nome = "Reserva de emergência", ValorAlvo = 10000m, ValorAtual = 3000m, Prazo = hoje.AddMonths(12), Icone = "🛟", MembroFamiliaId = perfil.Id },
        };
        ctx.Metas.AddRange(metas);
        await ctx.SaveChangesAsync();
    }

    private static List<Categoria> CriarCategoriasPadrao() => new()
    {
        new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#B2CFE6", TipoPadrao = TipoTransacao.Despesa, EhPadraoDoSistema = true },
        new Categoria { Nome = "Moradia", Icone = "🏠", CorHex = "#659ECD", TipoPadrao = TipoTransacao.Despesa, EhPadraoDoSistema = true },
        new Categoria { Nome = "Transporte", Icone = "🚗", CorHex = "#A2B94F", TipoPadrao = TipoTransacao.Despesa, EhPadraoDoSistema = true },
        new Categoria { Nome = "Saúde", Icone = "🏥", CorHex = "#6DA538", TipoPadrao = TipoTransacao.Despesa, EhPadraoDoSistema = true },
        new Categoria { Nome = "Lazer", Icone = "🎉", CorHex = "#A2B94F", TipoPadrao = TipoTransacao.Despesa, EhPadraoDoSistema = true },
        new Categoria { Nome = "Educação", Icone = "🎓", CorHex = "#A2B94F", TipoPadrao = TipoTransacao.Despesa, EhPadraoDoSistema = true },
        new Categoria { Nome = "Outros", Icone = "📦", CorHex = "#A2B94F", TipoPadrao = TipoTransacao.Despesa, EhPadraoDoSistema = true },
        new Categoria { Nome = "Cartão de Crédito", Icone = "💳", CorHex = "#B2CFE6", TipoPadrao = TipoTransacao.Despesa, EhPadraoDoSistema = true },
        new Categoria { Nome = "Impostos e Taxas", Icone = "🧾", CorHex = "#659ECD", TipoPadrao = TipoTransacao.Despesa, EhPadraoDoSistema = true },
        new Categoria { Nome = "Investimentos e Previdência", Icone = "📈", CorHex = "#6DA538", TipoPadrao = TipoTransacao.Despesa, EhPadraoDoSistema = true },
        new Categoria { Nome = "Renda", Icone = "💰", CorHex = "#6DA538", TipoPadrao = TipoTransacao.Receita, EhPadraoDoSistema = true },
    };
}
