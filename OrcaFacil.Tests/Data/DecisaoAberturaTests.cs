using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Data;

public class DecisaoAberturaTests
{
    [Fact]
    public void Tela_SoMostraOAplicativoQuandoPronta_ESoIniciaQuandoPendente()
    {
        Assert.True(DecisaoAbertura.MostrarAplicativo(FaseInicializacao.Pronta));
        Assert.False(DecisaoAbertura.MostrarAplicativo(FaseInicializacao.Pendente));
        Assert.False(DecisaoAbertura.MostrarAplicativo(FaseInicializacao.EmAndamento));
        Assert.False(DecisaoAbertura.MostrarAplicativo(FaseInicializacao.Falhou));

        Assert.True(DecisaoAbertura.DeveIniciar(FaseInicializacao.Pendente));
        Assert.False(DecisaoAbertura.DeveIniciar(FaseInicializacao.EmAndamento));
        Assert.False(DecisaoAbertura.DeveIniciar(FaseInicializacao.Pronta));
        Assert.False(DecisaoAbertura.DeveIniciar(FaseInicializacao.Falhou));

        Assert.True(DecisaoAbertura.MostrarFalha(FaseInicializacao.Falhou));
        Assert.False(DecisaoAbertura.MostrarFalha(FaseInicializacao.Pendente));
        Assert.False(DecisaoAbertura.MostrarFalha(FaseInicializacao.EmAndamento));
        Assert.False(DecisaoAbertura.MostrarFalha(FaseInicializacao.Pronta));
    }

    [Fact]
    public void Seed_SoRodaComBancoIntegroESemMembro()
    {
        var integro = new ResultadoPreparoBanco(null, new RelatorioIntegridadeBanco());
        Assert.True(PoliticaSeed.DevePopular(integro, haMembro: false));
        Assert.False(PoliticaSeed.DevePopular(integro, haMembro: true));

        var incompleto = new ResultadoPreparoBanco(
            null,
            new RelatorioIntegridadeBanco { TabelasFaltando = ["Cartoes"] });
        Assert.False(PoliticaSeed.DevePopular(incompleto, haMembro: false));
    }

    [Fact]
    public async Task FalhaDeBanco_ImpedeAbertura_EResumoNaoRepeteAMensagemClara()
    {
        SqliteException sqlite;
        try
        {
            await using var conexao = new SqliteConnection("Data Source=:memory:");
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = "SELECT * FROM tabela_que_nao_existe";
            await comando.ExecuteScalarAsync();
            throw new InvalidOperationException("o comando deveria falhar");
        }
        catch (SqliteException ex)
        {
            sqlite = ex;
        }

        Assert.True(FalhasInicializacao.ImpedeAbertura(sqlite));
        Assert.True(FalhasInicializacao.ImpedeAbertura(new InvalidOperationException("plugin", sqlite)));
        Assert.True(FalhasInicializacao.ImpedeAbertura(new DbUpdateException("não gravou")));
        Assert.False(FalhasInicializacao.ImpedeAbertura(new InvalidOperationException("plugin de notificação")));

        var motivo = "A migration AddCartoes parou no meio: a coluna Transacoes.CartaoId existe e a tabela Cartoes não existe.";
        var resultado = new ResultadoPreparoBanco(
            new InvalidOperationException(motivo),
            new RelatorioIntegridadeBanco { TabelasFaltando = ["Cartoes"] },
            aviso: motivo);
        var excecao = new IntegridadeBancoException(resultado);
        Assert.Equal(motivo, excecao.Message);
        Assert.Equal(motivo, FalhasInicializacao.Resumir(excecao));
        Assert.DoesNotContain("duplicate column", FalhasInicializacao.Resumir(excecao));

        var longa = new string('a', 600);
        var resumo = FalhasInicializacao.Resumir(new InvalidOperationException(longa));
        Assert.Equal(501, resumo.Length);
        Assert.EndsWith("…", resumo);

        var cadeia = new InvalidOperationException("a", new InvalidOperationException("b", new InvalidOperationException("c", new InvalidOperationException("d"))));
        Assert.Equal("a — b — c", FalhasInicializacao.Resumir(cadeia));
    }
}
