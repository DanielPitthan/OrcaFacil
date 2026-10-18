using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Tests.Tema;

namespace OrcaFacil.Tests.Armazenamento;

public class NomeTemporarioEspelhoTests
{
    private const string Final = FormatoEspelho.NomeArquivoSugerido;

    [Fact]
    public void NomeDefinitivo_EhODefinidoNoFormato()
    {
        Assert.Equal("orcafacil.espelho", Final);
    }

    [Fact]
    public void Temporario_LevaONomeFinalOGuidEOtmp()
    {
        var id = Guid.Parse("0123456789abcdef0123456789abcdef");
        var nome = NomeTemporarioEspelho.Criar(Final, id);

        Assert.Equal("orcafacil.espelho.0123456789abcdef0123456789abcdef.tmp", nome);
        Assert.True(NomeTemporarioEspelho.EhTemporarioDesteApp(nome, Final));
        Assert.False(NomeTemporarioEspelho.EhReservaDesteApp(nome, Final));
        Assert.False(NomeTemporarioEspelho.EhTemporarioDesteApp(Final, Final));
    }

    [Fact]
    public void Reserva_NaoPareceTemporarioNemNomeFinal()
    {
        var id = Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var nome = NomeTemporarioEspelho.CriarReserva(Final, id);

        Assert.Equal("orcafacil.espelho.reserva.aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", nome);
        Assert.True(NomeTemporarioEspelho.EhReservaDesteApp(nome, Final));
        Assert.False(NomeTemporarioEspelho.EhTemporarioDesteApp(nome, Final));
        Assert.False(string.Equals(nome, Final, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("foto.jpg")]
    [InlineData("orcafacil.espelho")]
    [InlineData("orcafacil.espelho.tmp")]
    [InlineData("orcafacil.espelho.reserva")]
    [InlineData(".orcafacil.espelho.0123456789abcdef0123456789abcdef.tmp")]
    [InlineData("orcafacil.espelho.0123456789ABCDEF0123456789ABCDEF.tmp")]
    public void ArquivoAlheio_NaoETemporarioNemReserva(string nome)
    {
        Assert.False(NomeTemporarioEspelho.EhTemporarioDesteApp(nome, Final));
        Assert.False(NomeTemporarioEspelho.EhReservaDesteApp(nome, Final));
    }

    [Fact]
    public void GuidVazio_ENomeComCaminho_SaoRecusados()
    {
        Assert.Throws<ArgumentException>(() => NomeTemporarioEspelho.Criar(Final, Guid.Empty));
        Assert.Throws<ArgumentException>(() => NomeTemporarioEspelho.Criar("pasta/espelho", Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => NomeTemporarioEspelho.CriarReserva(@"pasta\espelho", Guid.NewGuid()));
    }
}

public class RegrasPastaEspelhoTests
{
    private const string Final = FormatoEspelho.NomeArquivoSugerido;

    [Fact]
    public void SoCriaEApagaTemporario_EReservaSoComOFinalNoLugar()
    {
        var id = Guid.NewGuid();
        var temp = NomeTemporarioEspelho.Criar(Final, id);
        var reserva = NomeTemporarioEspelho.CriarReserva(Final, id);

        Assert.Null(RegrasPastaEspelho.ImpedirCriacao(Final, temp));
        Assert.Equal(CodigoFalhaPastaEspelho.NomeRecusado, RegrasPastaEspelho.ImpedirCriacao(Final, Final));
        Assert.Equal(CodigoFalhaPastaEspelho.NomeRecusado, RegrasPastaEspelho.ImpedirCriacao(Final, "foto.jpg"));
        Assert.Equal(CodigoFalhaPastaEspelho.NomeRecusado, RegrasPastaEspelho.ImpedirCriacao(Final, reserva));

        Assert.Null(RegrasPastaEspelho.ImpedirDescarteTemporario(Final, temp));
        Assert.Equal(CodigoFalhaPastaEspelho.NomeRecusado, RegrasPastaEspelho.ImpedirDescarteTemporario(Final, Final));
        Assert.Equal(CodigoFalhaPastaEspelho.NomeRecusado, RegrasPastaEspelho.ImpedirDescarteTemporario(Final, reserva));
        Assert.Equal(CodigoFalhaPastaEspelho.NomeRecusado, RegrasPastaEspelho.ImpedirDescarteTemporario(Final, "foto.jpg"));

        Assert.Equal(CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome, RegrasPastaEspelho.ImpedirDescarteReserva(Final, reserva, arquivoFinalPresente: false));
        Assert.Null(RegrasPastaEspelho.ImpedirDescarteReserva(Final, reserva, arquivoFinalPresente: true));
        Assert.Equal(CodigoFalhaPastaEspelho.NomeRecusado, RegrasPastaEspelho.ImpedirDescarteReserva(Final, Final, true));
        Assert.Equal(CodigoFalhaPastaEspelho.NomeRecusado, RegrasPastaEspelho.ImpedirDescarteReserva(Final, temp, true));
        Assert.Equal(CodigoFalhaPastaEspelho.NomeRecusado, RegrasPastaEspelho.ImpedirDescarteReserva(Final, "foto.jpg", true));
    }

    [Fact]
    public void Renome_SoEntreNomesDesteApp()
    {
        var temp = NomeTemporarioEspelho.Criar(Final, Guid.NewGuid());
        var reserva = NomeTemporarioEspelho.CriarReserva(Final, Guid.NewGuid());

        Assert.True(RegrasPastaEspelho.PodeRenomear(Final, temp, Final));
        Assert.True(RegrasPastaEspelho.PodeRenomear(Final, Final, reserva));
        Assert.True(RegrasPastaEspelho.PodeRenomear(Final, reserva, Final));
        Assert.False(RegrasPastaEspelho.PodeRenomear(Final, "foto.jpg", Final));
        Assert.False(RegrasPastaEspelho.PodeRenomear(Final, Final, "foto.jpg"));
        Assert.False(RegrasPastaEspelho.PodeRenomear(Final, Final, Final));
    }
}

public class ClassificadorFalhaPastaEspelhoTests
{
    private const string UriCompleta = "content://com.android.externalstorage.documents/tree/primary%3ADocuments%2FOrcaFacil";

    [Fact]
    public void PermissaoRevogada_PastaApagada_ESemEspaco_SaoDistintos()
    {
        Assert.Equal(
            CodigoFalhaPastaEspelho.PermissaoRevogada,
            ClassificadorFalhaPastaEspelho.Classificar(new SinalFalhaPasta("SecurityException", "Permission Denial: " + UriCompleta, true, null)));

        Assert.Equal(
            CodigoFalhaPastaEspelho.SemEspaco,
            ClassificadorFalhaPastaEspelho.Classificar(new SinalFalhaPasta("IOException", "No space left on device (ENOSPC)", true, true)));

        Assert.Equal(
            CodigoFalhaPastaEspelho.ArquivoAusente,
            ClassificadorFalhaPastaEspelho.Classificar(new SinalFalhaPasta("FileNotFoundException", "No such file or directory", true, null)));
        Assert.DoesNotContain(
            "apagada",
            MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.ArquivoAusente),
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            CodigoFalhaPastaEspelho.PastaApagada,
            ClassificadorFalhaPastaEspelho.Classificar(new SinalFalhaPasta("StaleDataException", "ENOENT", true, null)));

        Assert.Equal(
            CodigoFalhaPastaEspelho.PastaApagada,
            ClassificadorFalhaPastaEspelho.Classificar(new SinalFalhaPasta("IllegalArgumentException", null, true, false)));
    }

    [Fact]
    public void PermissaoAusente_GanhaDeOutrosSinais()
    {
        var codigo = ClassificadorFalhaPastaEspelho.Classificar(
            new SinalFalhaPasta("IOException", "ENOSPC", PermissaoAindaPersistida: false, DocumentoEncontrado: false));

        Assert.Equal(CodigoFalhaPastaEspelho.PermissaoRevogada, codigo);
    }

    [Fact]
    public void TextoClassificavel_RemoveAUriCompleta()
    {
        var texto = TextoClassificavelPasta.Montar("SecurityException", "open " + UriCompleta + " failed");

        Assert.DoesNotContain(UriCompleta, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("content://", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SecurityException", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void ExcecaoComUriECaminho_NaoVazaNaMensagem()
    {
        var excecao = new IOException("falha em " + UriCompleta + " e em /storage/emulated/0/Orca");
        var codigo = ClassificadorFalhaPastaEspelho.Classificar(SinalFalhaPasta.De(excecao));
        var mensagem = MensagensPastaEspelho.Para(codigo);

        Assert.DoesNotContain(UriCompleta, mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("/storage/", mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("content:", mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TodaFalha_TemMensagemClara_SemUri()
    {
        foreach (var codigo in Enum.GetValues<CodigoFalhaPastaEspelho>())
        {
            var mensagem = MensagensPastaEspelho.Para(codigo);
            if (codigo == CodigoFalhaPastaEspelho.Nenhuma)
            {
                Assert.Equal("", mensagem);
                continue;
            }

            Assert.False(string.IsNullOrWhiteSpace(mensagem));
            Assert.DoesNotContain("content:", mensagem, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/storage/", mensagem, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(CodigoFalhaPastaEspelho.PermissaoRevogada, "revogada")]
    [InlineData(CodigoFalhaPastaEspelho.PastaApagada, "apagada")]
    [InlineData(CodigoFalhaPastaEspelho.SemEspaco, "espaço")]
    [InlineData(CodigoFalhaPastaEspelho.ProvedorSemTroca, "não permite substituir")]
    public void MensagensDosCasosDoSaf_CitamOProblema(CodigoFalhaPastaEspelho codigo, string trecho)
    {
        Assert.Contains(trecho, MensagensPastaEspelho.Para(codigo), StringComparison.OrdinalIgnoreCase);
    }
}

public class MapeamentoLeituraPastaEspelhoTests
{
    [Fact]
    public void SoPastaNaoEscolhida_OuPastaVazia_PareceAusente()
    {
        Assert.False(MapeamentoLeituraPastaEspelho.ExisteParaOCoordenador(
            new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.PastaNaoEscolhida, false)));
        Assert.False(MapeamentoLeituraPastaEspelho.ExisteParaOCoordenador(
            new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.Nenhuma, false)));
        Assert.True(MapeamentoLeituraPastaEspelho.ExisteParaOCoordenador(
            new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.Nenhuma, true)));
        Assert.True(MapeamentoLeituraPastaEspelho.ExisteParaOCoordenador(
            new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.Nenhuma, false, residuoDesteApp: true)));

        foreach (var codigo in Enum.GetValues<CodigoFalhaPastaEspelho>())
        {
            if (codigo is CodigoFalhaPastaEspelho.Nenhuma
                or CodigoFalhaPastaEspelho.PastaNaoEscolhida
                or CodigoFalhaPastaEspelho.PublicadoComResiduo)
                continue;

            Assert.True(
                MapeamentoLeituraPastaEspelho.ExisteParaOCoordenador(new DiagnosticoPastaEspelho(codigo, false)),
                codigo.ToString());
            var bloqueio = MapeamentoLeituraPastaEspelho.Bloqueio(new DiagnosticoPastaEspelho(codigo, false));
            Assert.NotNull(bloqueio);
            Assert.False(bloqueio!.Legivel);
            Assert.Equal(MensagensPastaEspelho.Para(codigo), bloqueio.Motivo);
        }
    }

    [Fact]
    public void AcessoLivre_NaoEBloqueio()
    {
        Assert.Null(MapeamentoLeituraPastaEspelho.Bloqueio(
            new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.Nenhuma, true)));
    }
}

public class MemoriaEEscolhaPastaTests
{
    private const string UriCompleta = "content://com.android.externalstorage.documents/tree/primary%3ADocuments%2FOrcaFacil";

    [Fact]
    public void Gravar_SoAceitaContent_ENaoApareceNoLog()
    {
        var memoria = new MemoriaPastaEspelho(new PreferenciasEmMemoria());

        Assert.False(memoria.PossuiEscolha);
        Assert.Equal("pasta do espelho: não escolhida", memoria.DescricaoParaLog());
        Assert.Throws<ArgumentException>(() => memoria.Gravar("file:///storage/emulated/0/Orca"));
        Assert.Throws<ArgumentException>(() => memoria.Gravar(""));
        Assert.False(memoria.PossuiEscolha);

        memoria.Gravar(UriCompleta);

        Assert.True(memoria.PossuiEscolha);
        Assert.Equal(UriCompleta, memoria.LerUri());
        Assert.Equal("pasta do espelho: escolhida", memoria.DescricaoParaLog());
        Assert.DoesNotContain(UriCompleta, memoria.DescricaoParaLog(), StringComparison.Ordinal);
        Assert.DoesNotContain(UriCompleta, memoria.ToString(), StringComparison.Ordinal);
        Assert.Equal(UriEscolhaPasta.ChavePreferencia, "Espelho.Pasta.Uri");
    }

    [Fact]
    public void Esquecer_TiraAUri_SemSerUmaUriNova()
    {
        var memoria = new MemoriaPastaEspelho(new PreferenciasEmMemoria());
        memoria.Gravar(UriCompleta);

        memoria.Esquecer();

        Assert.False(memoria.PossuiEscolha);
        Assert.Null(memoria.LerUri());
        Assert.DoesNotContain(UriCompleta, memoria.DescricaoParaLog(), StringComparison.Ordinal);
    }

    [Fact]
    public void CancelarOuNaoPersistir_MantemAEscolhaAnterior()
    {
        var memoria = new MemoriaPastaEspelho(new PreferenciasEmMemoria());
        memoria.Gravar(UriCompleta);

        Assert.Equal(
            CodigoFalhaPastaEspelho.Cancelado,
            AplicacaoEscolhaPasta.Aplicar(memoria, false, true, true, true, "content://outra"));
        Assert.Equal(UriCompleta, memoria.LerUri());

        Assert.Equal(
            CodigoFalhaPastaEspelho.SemPermissaoDeEscrita,
            AplicacaoEscolhaPasta.Aplicar(memoria, true, true, false, true, "content://outra"));
        Assert.Equal(UriCompleta, memoria.LerUri());

        Assert.Equal(
            CodigoFalhaPastaEspelho.PermissaoRevogada,
            AplicacaoEscolhaPasta.Aplicar(memoria, true, true, true, false, "content://outra"));
        Assert.Equal(UriCompleta, memoria.LerUri());

        Assert.Equal(
            CodigoFalhaPastaEspelho.Indisponivel,
            AplicacaoEscolhaPasta.Aplicar(memoria, true, true, true, true, "file:///tmp/espelho"));
        Assert.Equal(UriCompleta, memoria.LerUri());
    }

    [Fact]
    public void ConfirmacaoComPermissao_GravaANovaUri()
    {
        var memoria = new MemoriaPastaEspelho(new PreferenciasEmMemoria());
        const string nova = "content://com.android.externalstorage.documents/tree/primary%3ADownload";

        var codigo = AplicacaoEscolhaPasta.Aplicar(memoria, true, true, true, true, nova);

        Assert.Equal(CodigoFalhaPastaEspelho.Nenhuma, codigo);
        Assert.Equal(nova, memoria.LerUri());
    }
}

public class AreaTemporariaEspelhoEmDiscoTests
{
    [Fact]
    public void Descartar_NaoApagaArquivoForaDaRaiz()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "orcafacil-area-" + Guid.NewGuid().ToString("N"));
        var fora = Path.Combine(Path.GetTempPath(), "orcafacil-usuario-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fora);
        var arquivoUsuario = Path.Combine(fora, "orcafacil.espelho");
        File.WriteAllBytes(arquivoUsuario, [1, 2, 3]);
        try
        {
            var area = new AreaTemporariaEspelhoEmDisco(raiz);
            var criado = area.CriarArquivo();
            Assert.StartsWith(Path.GetFullPath(raiz) + Path.DirectorySeparatorChar, Path.GetFullPath(criado), StringComparison.Ordinal);

            area.Descartar(arquivoUsuario);
            area.Descartar(raiz);

            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(arquivoUsuario));
            Assert.True(File.Exists(criado));

            area.Descartar(criado);
            Assert.False(File.Exists(criado));
        }
        finally
        {
            if (Directory.Exists(raiz))
                Directory.Delete(raiz, recursive: true);
            if (Directory.Exists(fora))
                Directory.Delete(fora, recursive: true);
        }
    }
}
