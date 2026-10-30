using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Exportacao;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Lancamentos;

public class CategoriaReceitaRecriadaTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CategoriaRepository _categorias;
    private readonly TransacaoRepository _transacoes;
    private readonly MembroFamiliaRepository _membros;
    private readonly CategoriaCadastroService _cadastro;

    public CategoriaReceitaRecriadaTests()
    {
        _categorias = new CategoriaRepository(_factory);
        _transacoes = new TransacaoRepository(_factory);
        _membros = new MembroFamiliaRepository(_factory);
        _cadastro = new CategoriaCadastroService(_categorias);
    }

    [Fact]
    public async Task RecebimentoDesativado_Recriar_GravaLancamentoNaLista()
    {
        var antiga = await PadraoRecebimentoAsync();
        var membro = await MembroAsync();
        var data = new DateTime(2026, 10, 26);
        await LancarAsync(antiga.Id, membro.Id, data, "Salário antigo");
        await _cadastro.RemoverAsync(antiga.Id);

        var desativada = await _categorias.GetByIdAsync(antiga.Id);
        Assert.NotNull(desativada);
        Assert.False(desativada.Ativo);
        Assert.DoesNotContain(await _categorias.GetAtivasAsync(), categoria => categoria.Id == antiga.Id);

        var recriada = await _cadastro.CriarAsync(Formulario("Recebimento", "💵", "#A2B94F"));

        Assert.Equal(antiga.Id, recriada.Id);
        Assert.True(recriada.Ativo);
        Assert.True(recriada.EhPadraoDoSistema);
        Assert.Equal(TipoTransacao.Receita, recriada.TipoPadrao);
        Assert.Equal("💵", recriada.Icone);
        Assert.Equal("Recebimento", recriada.Nome);
        Assert.Single(await _categorias.GetAllAsync(), categoria => categoria.Nome == "Recebimento");
        Assert.Contains(await _categorias.GetAtivasAsync(), categoria => categoria.Id == recriada.Id);

        await LancarAsync(recriada.Id, membro.Id, data, "Salário");

        var lista = await _transacoes.GetByPeriodoAsync(data, data.AddDays(1).AddTicks(-1), tipo: TipoTransacao.Receita);
        Assert.Equal(2, lista.Count);
        Assert.All(lista, lancamento =>
        {
            Assert.Equal(recriada.Id, lancamento.CategoriaId);
            Assert.Equal("Recebimento", lancamento.Categoria?.Nome);
        });
        Assert.Contains(lista, lancamento => lancamento.Descricao == "Salário");
    }

    [Fact]
    public async Task Recriar_ComOutraCaixa_ReativaAMesmaLinha()
    {
        var antiga = await PadraoRecebimentoAsync();
        await _cadastro.RemoverAsync(antiga.Id);

        var recriada = await _cadastro.CriarAsync(Formulario("RECEBIMENTO", "💵", "#A2B94F"));

        Assert.Equal(antiga.Id, recriada.Id);
        Assert.True(recriada.Ativo);
        Assert.Equal("RECEBIMENTO", recriada.Nome);
        Assert.Single(await _categorias.GetAllAsync());
    }

    [Fact]
    public async Task CategoriaEmUso_RecriarReativaEMantemLancamento()
    {
        var criada = await _cadastro.CriarAsync(Formulario("Freelance", "💰", "#6DA538"));
        var membro = await MembroAsync();
        var data = new DateTime(2026, 10, 20);
        await LancarAsync(criada.Id, membro.Id, data, "Freelance antigo");
        await _cadastro.RemoverAsync(criada.Id);

        var recriada = await _cadastro.CriarAsync(Formulario("Freelance", "💵", "#A2B94F"));

        Assert.Equal(criada.Id, recriada.Id);
        Assert.True(recriada.Ativo);
        var lista = await _transacoes.GetByPeriodoAsync(data, data.AddDays(1).AddTicks(-1));
        var lancamento = Assert.Single(lista);
        Assert.Equal(recriada.Id, lancamento.CategoriaId);
        Assert.Equal("Freelance antigo", lancamento.Descricao);
    }

    [Fact]
    public async Task CategoriaDoUsuarioSemUso_RemoverApaga_RecriarCriaOutraLinha()
    {
        var criada = await _cadastro.CriarAsync(Formulario("Bico", "💰", "#6DA538"));
        await _cadastro.RemoverAsync(criada.Id);

        Assert.Null(await _categorias.GetByIdAsync(criada.Id));

        var nova = await _cadastro.CriarAsync(Formulario("Bico", "💵", "#A2B94F"));
        Assert.NotEqual(criada.Id, nova.Id);
        Assert.True(nova.Ativo);
        Assert.Single(await _categorias.GetAllAsync());
    }

    [Fact]
    public async Task TipoDiferente_Recriar_CriaIdNovoEMantemLancamentoNaInativa()
    {
        var antiga = await _cadastro.CriarAsync(Formulario("Mercado", "🛒", "#9fb4c7", TipoTransacao.Despesa));
        var membro = await MembroAsync();
        var data = new DateTime(2026, 10, 20);
        await LancarAsync(antiga.Id, membro.Id, data, "Compra antiga", TipoTransacao.Despesa);
        await _cadastro.RemoverAsync(antiga.Id);

        var nova = await _cadastro.CriarAsync(Formulario("Mercado", "💵", "#A2B94F", TipoTransacao.Receita));

        Assert.NotEqual(antiga.Id, nova.Id);
        Assert.True(nova.Ativo);
        Assert.Equal(TipoTransacao.Receita, nova.TipoPadrao);
        Assert.Equal("💵", nova.Icone);

        var preservada = await _categorias.GetByIdAsync(antiga.Id);
        Assert.NotNull(preservada);
        Assert.False(preservada.Ativo);
        Assert.Equal(antiga.Id, preservada.Id);
        Assert.Equal("Mercado", preservada.Nome);
        Assert.Equal("🛒", preservada.Icone);
        Assert.Equal("#9fb4c7", preservada.CorHex);
        Assert.Equal(TipoTransacao.Despesa, preservada.TipoPadrao);

        var lista = await _transacoes.GetByPeriodoAsync(data, data.AddDays(1).AddTicks(-1));
        var lancamento = Assert.Single(lista);
        Assert.Equal(antiga.Id, lancamento.CategoriaId);
        Assert.Equal("Compra antiga", lancamento.Descricao);
        Assert.Equal(2, (await _categorias.GetAllAsync()).Count(categoria => categoria.Nome == "Mercado"));
    }

    [Fact]
    public async Task MesmoTipo_SemItensDeFatura_ReativaEDesligaPermiteItensFatura()
    {
        var antiga = await _cadastro.CriarAsync(Formulario("Cartão", "💳", "#28587b", TipoTransacao.Despesa, permiteItensFatura: true));
        var membro = await MembroAsync();
        var data = new DateTime(2026, 10, 21);
        await LancarAsync(antiga.Id, membro.Id, data, "Fatura sem itens", TipoTransacao.Despesa);
        await _cadastro.RemoverAsync(antiga.Id);

        var recriada = await _cadastro.CriarAsync(Formulario("Cartão", "💳", "#111111", TipoTransacao.Despesa, permiteItensFatura: false));

        Assert.Equal(antiga.Id, recriada.Id);
        Assert.True(recriada.Ativo);
        Assert.False(recriada.PermiteItensFatura);
        Assert.Equal("#111111", recriada.CorHex);
        var lista = await _transacoes.GetByPeriodoAsync(data, data.AddDays(1).AddTicks(-1));
        Assert.Equal(antiga.Id, Assert.Single(lista).CategoriaId);
    }

    [Fact]
    public async Task MesmoTipo_ComItensDeFatura_ReativaEMantemPermiteItensFatura()
    {
        var antiga = await _cadastro.CriarAsync(Formulario("Cartão", "💳", "#28587b", TipoTransacao.Despesa, permiteItensFatura: true));
        var membro = await MembroAsync();
        var data = new DateTime(2026, 10, 22);
        await _transacoes.AddAsync(new Transacao
        {
            Descricao = "Fatura",
            Valor = 40m,
            Data = data,
            Tipo = TipoTransacao.Despesa,
            CategoriaId = antiga.Id,
            MembroFamiliaId = membro.Id,
            Itens =
            [
                new ItemLancamento
                {
                    Descricao = "Mercado",
                    Valor = 40m,
                    CategoriaId = antiga.Id
                }
            ]
        });
        await _cadastro.RemoverAsync(antiga.Id);

        var recriada = await _cadastro.CriarAsync(Formulario("Cartão", "💳", "#111111", TipoTransacao.Despesa, permiteItensFatura: false));

        Assert.Equal(antiga.Id, recriada.Id);
        Assert.True(recriada.Ativo);
        Assert.True(recriada.PermiteItensFatura);
        var fatura = Assert.Single(await _transacoes.GetByPeriodoAsync(data, data.AddDays(1).AddTicks(-1)));
        Assert.Equal(antiga.Id, fatura.CategoriaId);
        var item = Assert.Single(fatura.Itens);
        Assert.Equal(antiga.Id, item.CategoriaId);
        Assert.Equal("Mercado", item.Descricao);
    }

    [Fact]
    public async Task NomeAtivoRepetido_NaoGravaEInforma()
    {
        await _cadastro.CriarAsync(Formulario("Recebimento", "💰", "#6DA538"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cadastro.CriarAsync(Formulario("recebimento", "💵", "#A2B94F")));

        Assert.Equal(CategoriaCadastroService.MensagemNomeRepetido, ex.Message);
        Assert.Single(await _categorias.GetAllAsync());
    }

    [Fact]
    public async Task RenomearParaNomeDeInativa_RecusaSemGravar()
    {
        var antiga = await PadraoRecebimentoAsync();
        await _cadastro.RemoverAsync(antiga.Id);
        var outra = await _cadastro.CriarAsync(Formulario("Extra", "💰", "#6DA538"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cadastro.AtualizarAsync(outra.Id, Formulario("Recebimento", "💵", "#A2B94F")));

        Assert.Equal(CategoriaCadastroService.MensagemNomeRepetido, ex.Message);
        var gravada = await _categorias.GetByIdAsync(outra.Id);
        Assert.Equal("Extra", gravada?.Nome);
        Assert.False((await _categorias.GetByIdAsync(antiga.Id))!.Ativo);
    }

    [Fact]
    public async Task LancamentoComCategoriaInexistente_FalhaDeBancoNaoVazaParaOUsuario()
    {
        var membro = await MembroAsync();
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _transacoes.AddAsync(new Transacao
        {
            Descricao = "Salário",
            Valor = 10m,
            Data = new DateTime(2026, 10, 26),
            Tipo = TipoTransacao.Receita,
            CategoriaId = 999,
            MembroFamiliaId = membro.Id
        }));

        var paraUsuario = FalhaDeGravacao.ParaOUsuario(ex, FalhaDeGravacao.MensagemLancamento);
        Assert.Equal(FalhaDeGravacao.MensagemLancamento, paraUsuario.Message);
        var detalhe = ex.InnerException?.Message ?? ex.Message;
        Assert.DoesNotContain(detalhe, paraUsuario.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("constraint", paraUsuario.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("DbUpdateException", VarreduraPlanilhaExportada.TipoDaFalha(ex));
        Assert.DoesNotContain(detalhe, VarreduraPlanilhaExportada.TipoDaFalha(ex), StringComparison.Ordinal);
        Assert.Empty(await _transacoes.GetByPeriodoAsync(DateTime.MinValue, DateTime.MaxValue));
    }

    [Fact]
    public void ParaOUsuario_NaoRepeteAMensagemDaExcecao()
    {
        var ex = new DbUpdateException("UNIQUE constraint failed: Categorias.Nome");

        var categoria = FalhaDeGravacao.ParaOUsuario(ex, FalhaDeGravacao.MensagemCategoria);
        var lancamento = FalhaDeGravacao.ParaOUsuario(ex, FalhaDeGravacao.MensagemLancamento);

        Assert.Equal(FalhaDeGravacao.MensagemCategoria, categoria.Message);
        Assert.Equal(FalhaDeGravacao.MensagemLancamento, lancamento.Message);
        Assert.DoesNotContain(ex.Message, categoria.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ex.Message, lancamento.Message, StringComparison.Ordinal);
        Assert.Equal("DbUpdateException", VarreduraPlanilhaExportada.TipoDaFalha(ex));
    }

    public void Dispose() => _factory.Dispose();

    private async Task<Categoria> PadraoRecebimentoAsync()
    {
        var categoria = new Categoria
        {
            Nome = "Recebimento",
            Icone = "💰",
            CorHex = "#6DA538",
            TipoPadrao = TipoTransacao.Receita,
            EhPadraoDoSistema = true,
            Ativo = true
        };
        await _categorias.AddAsync(categoria);
        return categoria;
    }

    private async Task<MembroFamilia> MembroAsync()
    {
        var membro = new MembroFamilia { Nome = "Usuário" };
        await _membros.AddAsync(membro);
        return membro;
    }

    private Task LancarAsync(int categoriaId, int membroId, DateTime data, string descricao, TipoTransacao tipo = TipoTransacao.Receita)
        => _transacoes.AddAsync(new Transacao
        {
            Descricao = descricao,
            Valor = 3200m,
            Data = data,
            Tipo = tipo,
            CategoriaId = categoriaId,
            MembroFamiliaId = membroId
        });

    private static CategoriaFormModel Formulario(
        string nome,
        string icone,
        string cor,
        TipoTransacao tipo = TipoTransacao.Receita,
        bool permiteItensFatura = false)
        => new()
        {
            Nome = nome,
            Icone = icone,
            CorHex = cor,
            TipoPadrao = tipo,
            PermiteItensFatura = permiteItensFatura
        };
}
