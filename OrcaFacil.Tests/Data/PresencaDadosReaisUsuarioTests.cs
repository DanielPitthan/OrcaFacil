using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Partida;
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
    public async Task Mesa_TresEstados_AberturaSemeiaSoNoVazio_BackupRecusaVazioESoDemonstracao()
    {
        await ConferirMesaAsync(SituacaoConteudoBanco.Vazio, _ => Task.CompletedTask);
        await ConferirMesaAsync(SituacaoConteudoBanco.Vazio, async banco =>
        {
            banco.Contexto.Categorias.AddRange(CatalogoDemonstracao.CriarCategorias());
            await banco.Contexto.SaveChangesAsync();
        });
        await ConferirMesaAsync(SituacaoConteudoBanco.SoDemonstracao, async banco =>
        {
            banco.Contexto.MembrosFamilia.Add(CatalogoDemonstracao.CriarPerfil());
            await banco.Contexto.SaveChangesAsync();
        });
        await ConferirMesaAsync(SituacaoConteudoBanco.SoDemonstracao, async banco =>
        {
            var perfil = await banco.EssencialAsync();
            CatalogoDemonstracao.AdicionarDemonstracao(banco.Contexto, perfil, new DateTime(2026, 10, 19));
            await banco.Contexto.SaveChangesAsync();
        });
        await ConferirMesaAsync(SituacaoConteudoBanco.DadoReal, async banco =>
        {
            banco.Contexto.LogsEvento.Add(new LogEvento { Tipo = TipoLogEvento.Sistema, Mensagem = "abriu" });
            await banco.Contexto.SaveChangesAsync();
        });
        await ConferirMesaAsync(SituacaoConteudoBanco.DadoReal, async banco =>
        {
            var perfil = await banco.EssencialAsync();
            CatalogoDemonstracao.AdicionarDemonstracao(banco.Contexto, perfil, new DateTime(2026, 10, 19));
            var alimentacao = banco.Contexto.Categorias.Single(categoria => categoria.Nome == "Alimentação");
            banco.Contexto.Transacoes.Add(new Transacao
            {
                Descricao = "Padaria",
                Valor = 12.50m,
                Data = new DateTime(2026, 10, 2),
                Tipo = TipoTransacao.Despesa,
                Recorrencia = TipoRecorrencia.Unica,
                CategoriaId = alimentacao.Id,
                MembroFamiliaId = perfil.Id
            });
            await banco.Contexto.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Abertura_NaoSemeia_Demonstracao_PerfilMinimo_NemLog()
    {
        await AssertNaoSemeiaAsync(async banco =>
        {
            var perfil = await banco.EssencialAsync();
            CatalogoDemonstracao.AdicionarDemonstracao(banco.Contexto, perfil, new DateTime(2026, 10, 19));
            await banco.Contexto.SaveChangesAsync();
        });
        await AssertNaoSemeiaAsync(async banco =>
        {
            banco.Contexto.MembrosFamilia.Add(CatalogoDemonstracao.CriarPerfil());
            await banco.Contexto.SaveChangesAsync();
        });
        await AssertNaoSemeiaAsync(async banco =>
        {
            banco.Contexto.LogsEvento.Add(new LogEvento { Tipo = TipoLogEvento.Sistema, Mensagem = "abriu" });
            await banco.Contexto.SaveChangesAsync();
        }, criaPerfilMinimo: true);
    }

    [Fact]
    public async Task LancamentoNoFormatoDaDemonstracao_ESoDemonstracao()
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

        Assert.Equal(SituacaoConteudoBanco.SoDemonstracao, await PoliticaSemeadura.ClassificarConteudoAsync(banco.Contexto));
        Assert.True(await PoliticaSemeadura.HaDadoDoUsuarioAsync(banco.Contexto));
        Assert.Equal(LeituraDadosReais.SemDadosReais, await PresencaDadosReaisUsuario.ClassificarAsync(banco.Contexto));
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

    private static async Task ConferirMesaAsync(SituacaoConteudoBanco estado, Func<Banco, Task> preparar)
    {
        await using var banco = await Banco.AbrirAsync();
        await preparar(banco);

        Assert.Equal(estado, await PoliticaSemeadura.ClassificarConteudoAsync(banco.Contexto));
        Assert.Equal(
            estado != SituacaoConteudoBanco.Vazio,
            await PoliticaSemeadura.HaDadoDoUsuarioAsync(banco.Contexto));
        Assert.Equal(
            estado == SituacaoConteudoBanco.DadoReal ? LeituraDadosReais.ComDadosReais : LeituraDadosReais.SemDadosReais,
            await PresencaDadosReaisUsuario.ClassificarAsync(banco.Contexto));
    }

    private static async Task AssertNaoSemeiaAsync(Func<Banco, Task> preparar, bool criaPerfilMinimo = false)
    {
        await using var banco = await Banco.AbrirAsync();
        await preparar(banco);
        await banco.FecharContextoAsync();

        var chamadas = 0;
        var log = new LogMudo();
        var semeadura = new SemeaduraDadosApp(
            new CaminhoBancoVivo(banco.Caminho),
            log,
            (_, _) =>
            {
                chamadas++;
                return Task.CompletedTask;
            });
        await semeadura.SemearAsync();

        Assert.Equal(0, chamadas);
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={banco.Caminho};Pooling=False")
            .Options;
        await using var leitura = new AppDbContext(opcoes);
        var nomes = await leitura.MembrosFamilia.Select(membro => membro.Nome).ToListAsync();
        if (criaPerfilMinimo)
        {
            Assert.Contains(PoliticaSemeadura.MensagemPerfilMinimo, log.Mensagens);
            Assert.Equal(PoliticaSemeadura.NomePerfilMinimo, Assert.Single(nomes));
            Assert.False(await leitura.Transacoes.AnyAsync());
        }
        else
        {
            Assert.Equal(CatalogoDemonstracao.NomePerfilPadrao, Assert.Single(nomes));
        }
    }

    private sealed class Banco : IAsyncDisposable
    {
        private Banco(string caminho, AppDbContext contexto)
        {
            Caminho = caminho;
            Contexto = contexto;
        }

        private bool _contextoAberto = true;

        public string Caminho { get; }

        public AppDbContext Contexto { get; }

        public async Task FecharContextoAsync()
        {
            if (!_contextoAberto)
                return;

            await Contexto.DisposeAsync();
            _contextoAberto = false;
        }

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
            await FecharContextoAsync();
            foreach (var sufixo in new[] { "", "-wal", "-shm" })
            {
                var arquivo = Caminho + sufixo;
                if (File.Exists(arquivo))
                    File.Delete(arquivo);
            }
        }
    }

    private sealed class LogMudo : IRegistroLog
    {
        public List<string> Mensagens { get; } = [];

        public void Informacao(string mensagem, object?[] argumentos) => Mensagens.Add(mensagem);

        public void Erro(Exception? excecao, string mensagem, object?[] argumentos) => Mensagens.Add(mensagem);
    }
}
