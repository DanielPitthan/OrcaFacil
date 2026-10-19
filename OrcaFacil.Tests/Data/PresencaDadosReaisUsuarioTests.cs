using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Data;

public class PresencaDadosReaisUsuarioTests
{
    [Fact]
    public async Task ArquivoAusente_NaoTemDadoReal()
    {
        var caminho = Path.Combine(Path.GetTempPath(), "orcafacil-ausente-" + Guid.NewGuid().ToString("N") + ".db");

        Assert.Equal(LeituraDadosReais.SemDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(caminho));
    }

    [Fact]
    public async Task ArquivoIlegivel_FicaIndeterminado()
    {
        var caminho = Path.Combine(Path.GetTempPath(), "orcafacil-lixo-" + Guid.NewGuid().ToString("N") + ".db");
        await File.WriteAllBytesAsync(caminho, [1, 2, 3, 4]);
        try
        {
            Assert.Equal(LeituraDadosReais.Indeterminado, await PresencaDadosReaisUsuario.ClassificarAsync(caminho));
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public async Task BancoVazio_ECategoriaPadrao_NaoSaoDado_LogConta()
    {
        await using var banco = await Banco.AbrirAsync();
        Assert.Equal(LeituraDadosReais.SemDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(banco.Contexto));

        banco.Contexto.Categorias.AddRange(CatalogoDemonstracao.CriarCategorias());
        await banco.Contexto.SaveChangesAsync();
        Assert.Equal(LeituraDadosReais.SemDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(banco.Contexto));
        Assert.False(await PoliticaSemeadura.HaDadoDoUsuarioAsync(banco.Contexto));

        banco.Contexto.LogsEvento.Add(new LogEvento { Tipo = TipoLogEvento.Sistema, Mensagem = "abriu" });
        await banco.Contexto.SaveChangesAsync();
        Assert.Equal(LeituraDadosReais.ComDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(banco.Contexto));
        Assert.True(await PoliticaSemeadura.HaDadoDoUsuarioAsync(banco.Contexto));
    }

    [Fact]
    public async Task AberturaEBackup_ClassificamIgual_Vazio_Demonstracao_Log_EDadoReal()
    {
        await using (var vazio = await Banco.AbrirAsync())
            await ConferirIgualAsync(vazio, false);

        await using (var demonstracao = await Banco.AbrirAsync())
        {
            var perfil = await demonstracao.EssencialAsync();
            CatalogoDemonstracao.AdicionarDemonstracao(demonstracao.Contexto, perfil, new DateTime(2026, 10, 19));
            await demonstracao.Contexto.SaveChangesAsync();
            await ConferirIgualAsync(demonstracao, true);
        }

        await using (var soLog = await Banco.AbrirAsync())
        {
            soLog.Contexto.LogsEvento.Add(new LogEvento { Tipo = TipoLogEvento.Sistema, Mensagem = "abriu" });
            await soLog.Contexto.SaveChangesAsync();
            await ConferirIgualAsync(soLog, true);
        }

        await using (var real = await Banco.AbrirAsync())
        {
            real.Contexto.MembrosFamilia.Add(new MembroFamilia { Nome = "Marina", Avatar = "M", Ativo = true });
            await real.Contexto.SaveChangesAsync();
            await ConferirIgualAsync(real, true);
        }
    }

    [Fact]
    public async Task LancamentoNoFormatoDaDemonstracao_ContaComoDado()
    {
        await using var banco = await Banco.AbrirAsync();
        var perfil = await banco.EssencialAsync();
        var renda = banco.Contexto.Categorias.Single(c => c.Nome == "Renda");
        banco.Contexto.Transacoes.Add(new Transacao
        {
            Descricao = "Salário",
            Valor = 6500m,
            Data = new DateTime(2026, 10, 5),
            Tipo = TipoTransacao.Receita,
            Recorrencia = TipoRecorrencia.Mensal,
            CategoriaId = renda.Id,
            MembroFamiliaId = perfil.Id
        });
        await banco.Contexto.SaveChangesAsync();

        Assert.Equal(LeituraDadosReais.ComDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(banco.Contexto));
    }

    [Fact]
    public async Task LancamentoForaDoCatalogo_OrcamentoRepetido_OuQuartaCopia_SaoDadoReal()
    {
        await using var padaria = await Banco.AbrirAsync();
        var perfil = await padaria.EssencialAsync();
        CatalogoDemonstracao.AdicionarDemonstracao(padaria.Contexto, perfil, new DateTime(2026, 10, 19));
        var alimentacao = padaria.Contexto.Categorias.Single(c => c.Nome == "Alimentação");
        padaria.Contexto.Transacoes.Add(new Transacao
        {
            Descricao = "Padaria",
            Valor = 12.50m,
            Data = new DateTime(2026, 10, 2),
            Tipo = TipoTransacao.Despesa,
            Recorrencia = TipoRecorrencia.Unica,
            CategoriaId = alimentacao.Id,
            MembroFamiliaId = perfil.Id
        });
        await padaria.Contexto.SaveChangesAsync();
        Assert.Equal(LeituraDadosReais.ComDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(padaria.Contexto));

        await using var repetido = await Banco.AbrirAsync();
        await repetido.EssencialAsync();
        CatalogoDemonstracao.AdicionarDemonstracao(
            repetido.Contexto,
            repetido.Contexto.MembrosFamilia.Single(),
            new DateTime(2026, 10, 19));
        await repetido.Contexto.SaveChangesAsync();
        var origem = repetido.Contexto.Orcamentos.First(o => o.Categoria!.Nome == "Alimentação");
        repetido.Contexto.Orcamentos.Add(new Orcamento
        {
            CategoriaId = origem.CategoriaId,
            ValorLimite = origem.ValorLimite,
            Mes = origem.Mes == 1 ? 12 : origem.Mes - 1,
            Ano = origem.Mes == 1 ? origem.Ano - 1 : origem.Ano
        });
        await repetido.Contexto.SaveChangesAsync();
        Assert.Equal(LeituraDadosReais.ComDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(repetido.Contexto));

        await using var quarta = await Banco.AbrirAsync();
        var pessoa = await quarta.EssencialAsync();
        CatalogoDemonstracao.AdicionarDemonstracao(quarta.Contexto, pessoa, new DateTime(2026, 10, 19));
        await quarta.Contexto.SaveChangesAsync();
        var renda = quarta.Contexto.Categorias.Single(c => c.Nome == "Renda");
        quarta.Contexto.Transacoes.Add(new Transacao
        {
            Descricao = "Salário",
            Valor = 6500m,
            Data = new DateTime(2026, 7, 5),
            Tipo = TipoTransacao.Receita,
            Recorrencia = TipoRecorrencia.Mensal,
            CategoriaId = renda.Id,
            MembroFamiliaId = pessoa.Id
        });
        await quarta.Contexto.SaveChangesAsync();
        Assert.Equal(LeituraDadosReais.ComDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(quarta.Contexto));
    }

    [Theory]
    [InlineData("nome")]
    [InlineData("apelido")]
    [InlineData("foto")]
    [InlineData("papel")]
    [InlineData("inativo")]
    [InlineData("outro")]
    public async Task PerfilQueNaoESoOPadrao_EDadoReal(string ajuste)
    {
        await using var banco = await Banco.AbrirAsync();
        var perfil = await banco.EssencialAsync();
        switch (ajuste)
        {
            case "nome":
                perfil.Nome = "Marina";
                break;
            case "apelido":
                perfil.Apelido = "Ma";
                break;
            case "foto":
                perfil.FotoPath = "foto.jpg";
                break;
            case "papel":
                perfil.Role = PerfilRole.Visualizador;
                break;
            case "inativo":
                perfil.Ativo = false;
                break;
            default:
                banco.Contexto.MembrosFamilia.Add(new MembroFamilia { Nome = "Filho", Avatar = "👦", Ativo = true });
                break;
        }

        await banco.Contexto.SaveChangesAsync();
        Assert.Equal(LeituraDadosReais.ComDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(banco.Contexto));
    }

    [Fact]
    public async Task CategoriaPropria_Cartao_OuFechamento_SaoDadoReal()
    {
        await using var categoria = await Banco.AbrirAsync();
        await categoria.EssencialAsync();
        categoria.Contexto.Categorias.Add(new Categoria
        {
            Nome = "Pets",
            Icone = "🐾",
            CorHex = "#112233",
            TipoPadrao = TipoTransacao.Despesa,
            EhPadraoDoSistema = false
        });
        await categoria.Contexto.SaveChangesAsync();
        Assert.Equal(LeituraDadosReais.ComDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(categoria.Contexto));

        await using var cartao = await Banco.AbrirAsync();
        await cartao.EssencialAsync();
        cartao.Contexto.Cartoes.Add(new Cartao
        {
            Nome = "Nubank",
            Bandeira = "Visa",
            UltimosDigitos = "1234",
            Tipo = TipoCartao.Credito
        });
        await cartao.Contexto.SaveChangesAsync();
        Assert.Equal(LeituraDadosReais.ComDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(cartao.Contexto));

        await using var fechado = await Banco.AbrirAsync();
        await fechado.EssencialAsync();
        fechado.Contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 10, Ano = 2026, Fechado = true });
        await fechado.Contexto.SaveChangesAsync();
        Assert.Equal(LeituraDadosReais.ComDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(fechado.Contexto));
    }

    private static async Task ConferirIgualAsync(Banco banco, bool dado)
    {
        var abertura = await PoliticaSemeadura.HaDadoDoUsuarioAsync(banco.Contexto);
        var backup = await PresencaDadosReaisUsuario.ClassificarAsync(banco.Contexto);
        Assert.Equal(dado, abertura);
        Assert.Equal(dado ? LeituraDadosReais.ComDadosReais : LeituraDadosReais.SemDadosReais, backup);
    }

    private sealed class Banco : IAsyncDisposable
    {
        private Banco(string caminho, AppDbContext contexto)
        {
            Caminho = caminho;
            Contexto = contexto;
        }

        public string Caminho { get; }

        public AppDbContext Contexto { get; }

        public static async Task<Banco> AbrirAsync()
        {
            var caminho = Path.Combine(Path.GetTempPath(), "orcafacil-reais-" + Guid.NewGuid().ToString("N") + ".db");
            var opcoes = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={caminho};Pooling=False")
                .Options;
            var ctx = new AppDbContext(opcoes);
            await ctx.Database.MigrateAsync();
            return new Banco(caminho, ctx);
        }

        public async Task<MembroFamilia> EssencialAsync()
        {
            Contexto.Categorias.AddRange(CatalogoDemonstracao.CriarCategorias());
            var perfil = CatalogoDemonstracao.CriarPerfil();
            Contexto.MembrosFamilia.Add(perfil);
            await Contexto.SaveChangesAsync();
            return perfil;
        }

        public async ValueTask DisposeAsync()
        {
            await Contexto.DisposeAsync();
            foreach (var sufixo in new[] { "", "-wal", "-shm" })
            {
                var arquivo = Caminho + sufixo;
                if (File.Exists(arquivo))
                    File.Delete(arquivo);
            }
        }
    }
}
