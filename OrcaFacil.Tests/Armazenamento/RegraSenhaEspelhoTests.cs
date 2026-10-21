using OrcaFacil.Core.Armazenamento.Espelho;

namespace OrcaFacil.Tests.Armazenamento;

public class RegraSenhaEspelhoTests
{
    [Theory]
    [InlineData("TrilhaAzul92")]
    [InlineData("Costa#Azul1")]
    [InlineData("frase comprida de backup")]
    [InlineData("somenteletras")]
    [InlineData("NAO-LOGAR-SENHA-9f3a")]
    public void SenhaComLetraENumero_AtendeOMinimo(string senha)
    {
        Assert.True(RegraSenhaEspelho.AtendeForcaMinima(senha));
        RegraSenhaEspelho.Exigir(senha);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curta1")]
    [InlineData("Orca1234")]
    [InlineData("somentelet")]
    [InlineData("12345678")]
    [InlineData("   a1   ")]
    [InlineData("TrilhaAzul")]
    [InlineData("trilhaazul9")]
    [InlineData("password1234")]
    [InlineData("Senha123456")]
    [InlineData("orcafacil1234")]
    [InlineData("aaaaaaaaaaaa")]
    [InlineData("123456789012")]
    [InlineData("Password1234!")]
    public void SenhaFraca_NaoAtende(string? senha)
    {
        Assert.False(RegraSenhaEspelho.AtendeForcaMinima(senha));
        if (senha is not null)
        {
            var excecao = Assert.Throws<ArgumentException>(() => RegraSenhaEspelho.Exigir(senha));
            var visivel = senha.Trim();
            if (visivel.Length >= 3)
                Assert.DoesNotContain(visivel, excecao.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Esquecimento_DocumentaSnapshotNovoSemApagarOAntigo()
    {
        Assert.Equal(10, RegraSenhaEspelho.TamanhoMinimo);
        Assert.Equal(12, RegraSenhaEspelho.TamanhoSemSimbolo);
        Assert.Contains("12", RegraSenhaEspelho.Requisitos, StringComparison.Ordinal);
        Assert.Contains("maiúscula", RegraSenhaEspelho.Requisitos, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("símbolo", RegraSenhaEspelho.Requisitos, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Esquecer a senha impede restaurar", RegraSenhaEspelho.Esquecimento, StringComparison.Ordinal);
        Assert.Contains("novo snapshot", RegraSenhaEspelho.Esquecimento, StringComparison.Ordinal);
        Assert.Contains("senha nova", RegraSenhaEspelho.Esquecimento, StringComparison.Ordinal);
        Assert.Contains("não é apagado automaticamente", RegraSenhaEspelho.Esquecimento, StringComparison.Ordinal);
    }

    [Fact]
    public void ParametrosDeSeguranca_FicamNoFormato()
    {
        Assert.Equal(1, FormatoEspelho.VersaoAtual);
        Assert.Equal(600_000, FormatoEspelho.IteracoesPadrao);
        Assert.Equal(600_000, FormatoEspelho.IteracoesMinimas);
        Assert.True(FormatoEspelho.IteracoesMaximas >= FormatoEspelho.IteracoesPadrao);
        Assert.Equal(32, FormatoEspelho.TamanhoChave);
        Assert.Equal(16, FormatoEspelho.TamanhoTag);
        Assert.Equal(16, FormatoEspelho.TamanhoSalt);
        Assert.Equal(12, FormatoEspelho.TamanhoNonce);
        Assert.Equal(32, FormatoEspelho.TamanhoHash);
        Assert.Equal("ORFESPEL", System.Text.Encoding.ASCII.GetString(FormatoEspelho.Magic));
        Assert.Equal("orcafacil.espelho", FormatoEspelho.NomeArquivoSugerido);
    }

    [Fact]
    public void VersaoDoApp_UsaOHistoricoConhecido()
    {
        var versao = VersaoSchemaAplicacao.DoApp;

        Assert.Equal(versao.Migrations.Count, versao.Atual);
        Assert.Equal("20261021101911_AddApuracaoIrMensal", versao.UltimaMigration);
        Assert.Equal(0, versao.Calcular([]));
        Assert.Equal(versao.Atual, versao.Calcular(versao.Migrations.ToList()));
        Assert.Equal(versao.Atual - 1, versao.Calcular(versao.Migrations.Take(versao.Atual - 1).ToList()));

        var comFuro = versao.Migrations.Where(id => id != versao.Migrations[3]).ToList();
        Assert.Null(versao.Calcular(comFuro));
        Assert.NotEqual(versao.Atual, versao.Calcular(comFuro));
        Assert.Null(versao.Calcular(["20990101000000_Futura"]));
        Assert.Null(versao.Calcular([.. versao.Migrations, "20990101000000_FuturaA", "20990201000000_FuturaB"]));
        Assert.Null(versao.Calcular([versao.Migrations[0], versao.Migrations[0]]));
    }

    [Fact]
    public void CodigoDoEspelho_NaoRegistraSenhaChaveNemCaminho()
    {
        var raiz = EncontrarRaiz();
        var pasta = Path.Combine(raiz, "OrcaFacil.Core", "Armazenamento", "Espelho");
        var fontes = Directory.GetFiles(pasta, "*.cs");
        Assert.NotEmpty(fontes);

        foreach (var arquivo in fontes)
        {
            var fonte = File.ReadAllText(arquivo);
            Assert.DoesNotContain("ILogger", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("Console.", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("Debug.Write", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("Trace.", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("LogInformation", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("LogError", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("LogWarning", fonte, StringComparison.Ordinal);
        }
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil.Core", "Armazenamento", "CoordenadorArmazenamento.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}

public class DestinoEspelhoArquivoTests
{
    [Fact]
    public async Task PublicacaoConcluida_SubstituiOEspelho()
    {
        using var area = new AreaTemp();
        var destino = new DestinoEspelhoArquivo(area.Espelho);
        var primeiro = Path.Combine(area.Raiz, "a.bin");
        var segundo = Path.Combine(area.Raiz, "b.bin");
        await File.WriteAllBytesAsync(primeiro, [1, 2, 3]);
        await File.WriteAllBytesAsync(segundo, [4, 5, 6, 7]);

        await destino.PublicarAsync(primeiro);
        await destino.PublicarAsync(segundo);

        Assert.Equal(new byte[] { 4, 5, 6, 7 }, await File.ReadAllBytesAsync(area.Espelho));
        Assert.Empty(Temporarios(area));
        Assert.DoesNotContain(area.Raiz, destino.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FalhaDuranteACopia_MantemOEspelhoAnterior()
    {
        using var area = new AreaTemp();
        var destino = new DestinoEspelhoArquivo(area.Espelho);
        var anterior = new byte[] { 9, 8, 7, 6 };
        await File.WriteAllBytesAsync(area.Espelho, anterior);

        var excecao = await Assert.ThrowsAsync<IOException>(() => destino.PublicarAsync(Path.Combine(area.Raiz, "ausente.bin")));

        Assert.Equal(anterior, await File.ReadAllBytesAsync(area.Espelho));
        Assert.Empty(Temporarios(area));
        Assert.DoesNotContain(area.Raiz, excecao.ToString(), StringComparison.Ordinal);
        Assert.Contains("arquivo anterior foi mantido", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> Temporarios(AreaTemp area)
        => Directory.GetFiles(Path.GetDirectoryName(area.Espelho)!, "*.tmp", SearchOption.TopDirectoryOnly);

    private sealed class AreaTemp : IDisposable
    {
        public AreaTemp()
        {
            Raiz = Path.Combine(Path.GetTempPath(), "orcafacil-destino-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Raiz, "espelho"));
        }

        public string Raiz { get; }

        public string Espelho => Path.Combine(Raiz, "espelho", FormatoEspelho.NomeArquivoSugerido);

        public void Dispose()
        {
            if (Directory.Exists(Raiz))
                Directory.Delete(Raiz, recursive: true);
        }
    }
}
