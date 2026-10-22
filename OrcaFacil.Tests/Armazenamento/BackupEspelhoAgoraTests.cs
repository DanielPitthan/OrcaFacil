using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Armazenamento.Backup;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Armazenamento;

public class BackupEspelhoAgoraTests
{
    private const string Senha = "NAO-LOGAR-SENHA-9f3a";
    private const string SenhaNova = "TrilhaAzul92";
    private const string UriPasta = "content://com.android.externalstorage.documents/tree/primary%3AOrca";

    [Fact]
    public void Provedor_GuardaSoNaInstancia_EToStringNaoTemASenha()
    {
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var outro = new ProvedorSenhaEspelhoMemoria();
        Assert.False(provedor.Informada);
        Assert.Null(provedor.Obter());

        var definida = provedor.Definir(Senha, Senha);

        Assert.True(definida.Aceita);
        Assert.Null(definida.Motivo);
        Assert.True(provedor.Informada);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Null(outro.Obter());
        AssertNaoVaza(provedor, Senha);
        AssertNaoVaza(definida, Senha);
        AssertNaoVazaTexto(provedor.ToString(), Senha);
        foreach (var campo in typeof(ProvedorSenhaEspelhoMemoria).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (campo.GetValue(null) is string texto)
                AssertNaoVazaTexto(texto, Senha);
        }

        provedor.Esquecer();
        Assert.False(provedor.Informada);
        Assert.Null(provedor.Obter());
        AssertNaoVaza(provedor, Senha);
    }

    [Fact]
    public void SenhaFracaOuDiferente_NaoSubstituiAQueJaEstava()
    {
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);

        var fraca = provedor.Definir("caracteres", "caracteres");
        var diferente = provedor.Definir(Senha, SenhaNova);

        Assert.False(fraca.Aceita);
        Assert.False(diferente.Aceita);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(RegraSenhaEspelho.Requisitos, fraca.Motivo);
        Assert.Equal("As senhas não coincidem. A senha desta sessão não mudou.", diferente.Motivo);
        Assert.DoesNotContain(Senha, diferente.Motivo, StringComparison.Ordinal);
        Assert.DoesNotContain(SenhaNova, diferente.Motivo, StringComparison.Ordinal);
        Assert.Equal("ResultadoDefinicaoSenha:recusada", fraca.ToString());
        Assert.DoesNotContain("caracteres", fraca.ToString(), StringComparison.Ordinal);
        AssertNaoVaza(diferente, Senha);
        AssertNaoVaza(diferente, SenhaNova);
    }

    [Fact]
    public void DefinicaoELeitura_PrecisamSerOMesmoObjeto()
    {
        var volume = new VolumeMemoria();
        var destino = new DestinoEspelhoPasta(new ProtocoloPublicacaoEspelho(volume));
        var leitura = new ProvedorSenhaEspelhoMemoria();
        var definicao = new ProvedorSenhaEspelhoMemoria();

        var excecao = Assert.Throws<InvalidOperationException>(() =>
            new BackupEspelhoAgora(volume, destino, leitura, definicao, VersaoSchemaAplicacao.DoApp));

        AssertNaoVaza(excecao, Senha);
    }

    [Fact]
    public async Task SenhaFraca_NaoConsultaAPasta_NaoPublica_NaoGravaPreferencia()
    {
        var preferencias = new PreferenciasEspiao();
        var memoria = new MemoriaPastaEspelho(preferencias);
        memoria.Gravar(UriPasta);
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 4, 4, 4 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(Path.Combine(Path.GetTempPath(), "ausente.db"), "caracteres", "caracteres");

        Assert.False(resultado.Sucesso);
        Assert.Equal(RegraSenhaEspelho.Requisitos, resultado.Mensagem);
        Assert.Equal("ResultadoBackupEspelho:falha", resultado.ToString());
        Assert.DoesNotContain("caracteres", resultado.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, volume.Consultas);
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.Null(provedor.Obter());
        AssertSoAUri(preferencias, "caracteres");
    }

    [Fact]
    public async Task SenhasDiferentes_NaoPublicam_ENaoTrocamASenhaMemorizada()
    {
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 3, 3 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync("orcafacil.db", Senha, SenhaNova);

        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemNaoCoincide, resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(0, volume.Consultas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
    }

    [Fact]
    public async Task SemSenha_NaoPublica()
    {
        var volume = new VolumeMemoria();
        var backup = Criar(volume, new ProvedorSenhaEspelhoMemoria());

        var resultado = await backup.ExecutarAsync("orcafacil.db", "", null);

        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemSemSenha, resultado.Mensagem);
        Assert.Equal(0, volume.Consultas);
        Assert.Equal(0, volume.CriacoesIniciadas);
    }

    [Fact]
    public async Task PastaNaoEscolhida_NaoPublica_NaoMemorizaSenha_NaoApagaOAnterior()
    {
        var volume = new VolumeMemoria { Acesso = CodigoFalhaPastaEspelho.PastaNaoEscolhida };
        var anterior = new byte[] { 8, 8, 8 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync("orcafacil.db", Senha, Senha);

        Assert.False(resultado.Sucesso);
        Assert.Equal(MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.PastaNaoEscolhida), resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        Assert.Null(provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.Empty(volume.Apagados);
    }

    [Fact]
    public async Task ConsultaQueEstoura_NaoPublica()
    {
        var volume = new VolumeMemoria { EstourarConsulta = true };
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync("orcafacil.db", Senha, Senha);

        Assert.False(resultado.Sucesso);
        Assert.Equal(MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.Indisponivel), resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        Assert.Null(provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
    }

    [Fact]
    public async Task Cancelado_NaoPublica()
    {
        var volume = new VolumeMemoria();
        var backup = Criar(volume, new ProvedorSenhaEspelhoMemoria());
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            backup.ExecutarAsync("orcafacil.db", Senha, Senha, cancelamento.Token));

        Assert.Equal(0, volume.CriacoesIniciadas);
    }

    [Fact]
    public async Task BancoAusente_ComSenhaJaMemorizada_NaoPublica()
    {
        using var area = new AreaTemp();
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 1, 1, 1 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(Path.Combine(area.Raiz, "ausente.db"), null, null);

        Assert.False(resultado.Sucesso);
        Assert.Equal(
            "Já existe um backup nesta pasta. A cópia anterior continua guardada na pasta de backup.",
            BackupEspelhoAgora.MensagemRecusaEspelhoExistente);
        Assert.DoesNotContain("Restaurar", BackupEspelhoAgora.MensagemRecusaEspelhoExistente, StringComparison.Ordinal);
        Assert.Equal(BackupEspelhoAgora.MensagemRecusaEspelhoExistente, resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
    }

    [Fact]
    public async Task Backup_DeSqliteReal_PublicaPeloProtocolo_SemVazarSenha()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var preferencias = new PreferenciasEspiao();
        var memoria = new MemoriaPastaEspelho(preferencias);
        memoria.Gravar(UriPasta);
        var volume = new VolumeMemoria();
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(area.Banco, Senha, Senha);

        Assert.True(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemGravado, resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
        var espelho = volume.Arquivos[volume.NomeArquivo];
        Assert.Equal(Encoding.ASCII.GetBytes("ORFESPEL"), espelho[..8]);
        AssertNaoContem(espelho, Senha);
        AssertNaoContem(espelho, "Marina");
        AssertNaoContemArquivo(area.Banco, Senha);
        var wal = area.Banco + "-wal";
        if (File.Exists(wal))
            AssertNaoContemArquivo(wal, Senha);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.DoesNotContain(volume.Arquivos.Keys, nome => nome.EndsWith(".tmp", StringComparison.Ordinal));
        AssertSoAUri(preferencias, Senha);

        var copia = Path.Combine(area.Raiz, "espelho.bin");
        await File.WriteAllBytesAsync(copia, espelho);
        using (var aberto = await LerEspelho.AbrirAsync(copia, Senha, VersaoSchemaAplicacao.DoApp))
        {
            await using var snapshot = AbrirSomenteLeitura(aberto.CaminhoSqlite);
            Assert.Equal("Marina", await EscalarTextoAsync(snapshot, "SELECT Nome FROM MembrosFamilia"));
        }

        Assert.Equal(espelho, volume.Arquivos[volume.NomeArquivo]);
        Assert.True(File.Exists(area.Banco));
        var criacoes = volume.CriacoesIniciadas;

        var deNovo = await backup.ExecutarAsync(Path.Combine(area.Raiz, "ausente.db"), "", "");

        Assert.False(deNovo.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemRecusaEspelhoExistente, deNovo.Mensagem);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(criacoes, volume.CriacoesIniciadas);
        Assert.Equal(espelho, volume.Arquivos[volume.NomeArquivo]);
    }

    [Fact]
    public async Task FalhaNoMeio_MantemOEspelhoAnterior_ENaoTrocaASenha()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var preferencias = new PreferenciasEspiao();
        new MemoriaPastaEspelho(preferencias).Gravar(UriPasta);
        var volume = new VolumeMemoria
        {
            FalhaAoCriar = CodigoFalhaPastaEspelho.SemEspaco,
            DeixarParcialAoFalharCriacao = true
        };
        var anterior = new byte[] { 7, 7, 7 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor, new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")));

        var resultado = await backup.ExecutarAsync(
            area.Banco,
            SenhaNova,
            SenhaNova,
            substituirEspelhoExistente: true);

        Assert.False(resultado.Sucesso);
        Assert.Contains("arquivo anterior foi mantido", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
        Assert.DoesNotContain("foto.jpg", volume.Apagados);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
        AssertNaoContemArquivo(area.Banco, SenhaNova);
        AssertSoAUri(preferencias, Senha);
        AssertSoAUri(preferencias, SenhaNova);
        foreach (var par in volume.Arquivos)
            AssertNaoContem(par.Value, SenhaNova);
    }

    [Fact]
    public void MensagemDeSucesso_EFixa_EToStringNaoLevaAFrase()
    {
        const string senha = "gravado na pa";
        Assert.True(RegraSenhaEspelho.AtendeForcaMinima(senha));
        Assert.Contains(senha, BackupEspelhoAgora.MensagemGravado, StringComparison.Ordinal);

        var resultado = ResultadoBackupEspelho.Ok(BackupEspelhoAgora.MensagemGravado);

        Assert.True(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemGravado, resultado.Mensagem);
        Assert.Equal("ResultadoBackupEspelho", resultado.ToString());
        Assert.DoesNotContain(senha, resultado.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Falha_UsaFraseFixa_EToStringNaoLevaSenha()
    {
        var resultado = ResultadoBackupEspelho.Falha(BackupEspelhoAgora.MensagemMantido);

        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemMantido, resultado.Mensagem);
        Assert.DoesNotContain(Senha, resultado.Mensagem, StringComparison.Ordinal);
        AssertNaoVaza(resultado, Senha);
    }

    [Fact]
    public void RotuloDaPasta_NaoLevaUri()
    {
        const string uri = "content://com.android.externalstorage.documents/tree/primary%3ASegredo";
        var casos = new[]
        {
            RotuloPastaEspelho.Descrever(new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.Nenhuma, true)),
            RotuloPastaEspelho.Descrever(new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.Nenhuma, false)),
            RotuloPastaEspelho.Descrever(new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.PastaNaoEscolhida, false)),
            RotuloPastaEspelho.Descrever(new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.PermissaoRevogada, false))
        };

        Assert.Equal("Escolhida", casos[0].Valor);
        Assert.Equal("Há um espelho nesta pasta.", casos[0].Detalhe);
        Assert.Equal("Ainda não há espelho nesta pasta.", casos[1].Detalhe);
        Assert.Equal("Não escolhida", casos[2].Valor);
        Assert.Equal("Indisponível", casos[3].Valor);
        foreach (var resumo in casos)
        {
            Assert.DoesNotContain(uri, resumo.Valor, StringComparison.Ordinal);
            Assert.DoesNotContain(uri, resumo.Detalhe, StringComparison.Ordinal);
            Assert.Equal("ResumoPastaEspelho", resumo.ToString());
            Assert.DoesNotContain("content:", resumo.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SenhaNaoEntraEmPreferencias_SecureStorage_Backup_NemNaTela()
    {
        var raiz = RaizRepositorio();
        var provedor = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core/Armazenamento/Espelho/ProvedorSenhaEspelhoMemoria.cs"));
        var backup = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core/Armazenamento/Espelho/BackupEspelhoAgora.cs"));
        var tela = File.ReadAllText(Path.Combine(raiz, "OrcaFacil/Components/Pages/Configuracoes/ArmazenamentoPanel.razor"));
        var registro = File.ReadAllText(Path.Combine(raiz, "OrcaFacil/Services/Espelho/RegistroServicosPastaEspelho.cs"));
        var maui = File.ReadAllText(Path.Combine(raiz, "OrcaFacil/MauiProgram.cs"));

        foreach (var fonte in new[] { provedor, backup, tela })
            AssertSemPersistencia(fonte);

        var registroExecutavel = SemComentarios(registro);
        Assert.Contains(
            "services.AddSingleton<IProvedorSenhaEspelho, ProvedorSenhaEspelhoMemoria>();",
            registroExecutavel,
            StringComparison.Ordinal);
        var comentario = """
            // esta linha não entra na conferência
            services.AddSingleton<IProvedorSenhaEspelho, ProvedorSenhaEspelhoMemoria>();
            """;
        var comentarioLimpo = SemComentarios(comentario);
        Assert.DoesNotContain("não entra", comentarioLimpo, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IProvedorSenhaEspelho, ProvedorSenhaEspelhoMemoria>", comentarioLimpo, StringComparison.Ordinal);
        Assert.DoesNotContain("ProvedorSenhaEspelhoMemoria", maui, StringComparison.Ordinal);
        Assert.Contains("RegraSenhaEspelho.Esquecimento", tela, StringComparison.Ordinal);
        Assert.Contains("Fazer backup agora", tela, StringComparison.Ordinal);
        Assert.Contains("Escolher pasta", tela, StringComparison.Ordinal);
        Assert.Contains("se perde ao fechar o app", tela, StringComparison.Ordinal);
        Assert.Contains("new-password", tela, StringComparison.Ordinal);
        Assert.Contains("data-lpignore", tela, StringComparison.Ordinal);
        Assert.Contains("data-1p-ignore", tela, StringComparison.Ordinal);
        Assert.Contains("BackupEspelhoAgora.MensagemConfirmacao", tela, StringComparison.Ordinal);
        Assert.DoesNotContain("será substituído", tela, StringComparison.Ordinal);
        Assert.Contains("IDisposable", tela, StringComparison.Ordinal);
        Assert.Contains("SenhaDaPastaEspelho.AoConfirmarEscolha", tela, StringComparison.Ordinal);
        Assert.Contains("EspelhoExistenteEm", tela, StringComparison.Ordinal);
        Assert.Contains("pt-BR", tela, StringComparison.Ordinal);
        Assert.DoesNotContain("Restaurar", tela, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.Message", tela, StringComparison.Ordinal);
        Assert.DoesNotContain("TextoSemSegredoEspelho", backup, StringComparison.Ordinal);
        Assert.DoesNotContain("TextoSemSegredoEspelho", provedor, StringComparison.Ordinal);
        foreach (var linha in tela.Split('\n'))
        {
            if (!linha.Contains("Snackbar.Add", StringComparison.Ordinal))
                continue;

            Assert.DoesNotContain("_senha", linha, StringComparison.Ordinal);
            Assert.DoesNotContain("_confirmacao", linha, StringComparison.Ordinal);
        }

        Assert.False(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.ArquivoSecureStorage));
        Assert.False(RegrasBackupAndroid.CopiadoPeloXml(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.ArquivoSecureStorage));
        Assert.DoesNotContain(
            RegrasBackupAndroid.Inclusoes,
            regra => regra.Caminho.Contains("senha", StringComparison.OrdinalIgnoreCase)
                || regra.Caminho.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TrocaDePasta_EsqueceASenha_MesmaUriMantem()
    {
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);

        Assert.False(SenhaDaPastaEspelho.AoConfirmarEscolha(provedor, UriPasta, UriPasta));
        Assert.Equal(Senha, provedor.Obter());
        Assert.False(SenhaDaPastaEspelho.AoConfirmarEscolha(provedor, null, null));
        Assert.Equal(Senha, provedor.Obter());

        Assert.True(SenhaDaPastaEspelho.AoConfirmarEscolha(provedor, UriPasta, UriPasta + "Outra"));
        Assert.Null(provedor.Obter());
        AssertNaoVaza(provedor, Senha);
    }

    [Fact]
    public void LogDeFalha_OmiteDetalheQueRepeteASenha()
    {
        var omisso = new LogEspiao();
        RegistroFalhaEspelho.Registrar(omisso, new IOException("disco " + Senha), Senha);

        Assert.Equal(RegistroFalhaEspelho.DetalheOmitido, Assert.Single(omisso.Mensagens));
        Assert.Null(Assert.Single(omisso.Excecoes));
        Assert.DoesNotContain(Senha, omisso.Mensagens[0], StringComparison.Ordinal);

        var aberto = new LogEspiao();
        RegistroFalhaEspelho.Registrar(aberto, new IOException("disco cheio"), Senha);

        Assert.Equal(RegistroFalhaEspelho.Detalhe, Assert.Single(aberto.Mensagens));
        Assert.IsType<IOException>(Assert.Single(aberto.Excecoes));
        Assert.DoesNotContain(Senha, aberto.Mensagens[0], StringComparison.Ordinal);
        Assert.DoesNotContain(Senha, aberto.Excecoes[0]!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BancoVazio_ComEspelho_Recusa_ENaoPublica()
    {
        using var area = new AreaTemp();
        await MigrarVazioAsync(area.Banco);
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 2, 2, 2 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(area.Banco, Senha, Senha);

        Assert.False(resultado.Sucesso);
        Assert.False(resultado.ExigeConfirmacao);
        Assert.Equal(BackupEspelhoAgora.MensagemRecusaEspelhoExistente, resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        Assert.Null(provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task SoDemonstracao_ComEspelho_Recusa_ENaoTrocaASenha()
    {
        using var area = new AreaTemp();
        await SemearDemonstracaoAsync(area.Banco);
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 5, 5 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova);

        Assert.False(resultado.ExigeConfirmacao);
        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemRecusaEspelhoExistente, resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
    }

    [Fact]
    public async Task MesDoOrcamentoAlterado_ComEspelho_PedeConfirmacao()
    {
        using var area = new AreaTemp();
        await SemearDemonstracaoComMesDeOrcamentoAlteradoAsync(area.Banco);
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 5, 9 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova);

        Assert.True(resultado.ExigeConfirmacao);
        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemConfirmacao, resultado.Mensagem);
        Assert.NotEqual(BackupEspelhoAgora.MensagemRecusaEspelhoExistente, resultado.Mensagem);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
    }

    [Fact]
    public async Task SoPerfilMinimo_ComEspelho_Recusa_ENaoTrocaASenha()
    {
        using var area = new AreaTemp();
        await InserirSoPerfilMinimoAsync(area.Banco);
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 7, 7 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova);

        Assert.False(resultado.ExigeConfirmacao);
        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemRecusaEspelhoExistente, resultado.Mensagem);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
    }

    [Fact]
    public async Task DemonstracaoComLancamentoReal_ComEspelho_PedeConfirmacao()
    {
        using var area = new AreaTemp();
        await SemearDemonstracaoComLancamentoRealAsync(area.Banco);
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 8, 1 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova);

        Assert.True(resultado.ExigeConfirmacao);
        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemConfirmacao, resultado.Mensagem);
        Assert.NotEqual(BackupEspelhoAgora.MensagemRecusaEspelhoExistente, resultado.Mensagem);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
    }

    [Fact]
    public async Task SoLog_ComEspelho_PedeConfirmacao_ENaoRecusa()
    {
        using var area = new AreaTemp();
        await InserirSoLogAsync(area.Banco);
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 6, 6 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova);

        Assert.True(resultado.ExigeConfirmacao);
        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemConfirmacao, resultado.Mensagem);
        Assert.NotEqual(BackupEspelhoAgora.MensagemRecusaEspelhoExistente, resultado.Mensagem);
        Assert.DoesNotContain("Restaurar", resultado.Mensagem, StringComparison.Ordinal);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
    }

    [Fact]
    public async Task DadoReal_ExigeConfirmacao_ECancelarNaoPublica()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 8, 8, 8 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor, new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")));

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova);

        Assert.True(resultado.ExigeConfirmacao);
        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.EspelhoExistenteEm);
        Assert.Equal(BackupEspelhoAgora.MensagemConfirmacao, resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task BancoIlegivel_ComEspelho_PedeConfirmacao_NaoRecusa()
    {
        using var area = new AreaTemp();
        await File.WriteAllBytesAsync(area.Banco, [1, 2, 3, 4, 5]);
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 3 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(area.Banco, null, null);

        Assert.True(resultado.ExigeConfirmacao);
        Assert.NotEqual(BackupEspelhoAgora.MensagemRecusaEspelhoExistente, resultado.Mensagem);
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(Senha, provedor.Obter());
    }

    [Fact]
    public async Task EspelhoLegivel_InformaAData_ENaoSubstituiSemConfirmacao()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria();
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var backup = Criar(volume, provedor, new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")));
        var antes = DateTimeOffset.UtcNow;

        var publicado = await backup.ExecutarAsync(area.Banco, Senha, Senha);

        Assert.True(publicado.Sucesso);
        var espelho = volume.Arquivos[volume.NomeArquivo].ToArray();
        var criacoes = volume.CriacoesIniciadas;

        var confirmacao = await backup.ExecutarAsync(area.Banco, null, null);

        Assert.True(confirmacao.ExigeConfirmacao);
        Assert.False(confirmacao.Sucesso);
        Assert.NotNull(confirmacao.EspelhoExistenteEm);
        Assert.InRange(confirmacao.EspelhoExistenteEm.Value, antes.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(criacoes, volume.CriacoesIniciadas);
        Assert.Equal(espelho, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
        AssertNaoVaza(confirmacao, Senha);
    }

    [Fact]
    public async Task SubstituirComDadoReal_Publica()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 4, 4 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor, new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")));

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova, substituirEspelhoExistente: true);

        Assert.True(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemGravado, resultado.Mensagem);
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.NotEqual(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(Encoding.ASCII.GetBytes("ORFESPEL"), volume.Arquivos[volume.NomeArquivo][..8]);
        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task PromocaoTrocouONomeEFalhou_Publica_AdotaSenhaNova_ENaoDizQueFalhou()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria { EfetuarPromocaoEFalhar = true };
        var anterior = new byte[] { 1, 1, 1 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var log = new LogEspiao();
        var backup = Criar(
            volume,
            provedor,
            new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")),
            log);

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova, substituirEspelhoExistente: true);

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.ExigeConfirmacao);
        Assert.Equal(BackupEspelhoAgora.MensagemGravadoComReserva, resultado.Mensagem);
        Assert.DoesNotContain("nada foi publicado", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Não foi possível", resultado.Mensagem, StringComparison.Ordinal);
        Assert.Equal("ResultadoBackupEspelho", resultado.ToString());
        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.NotEqual(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(anterior, volume.Arquivos[reserva]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.DoesNotContain("foto.jpg", volume.Apagados);
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
        foreach (var mensagem in log.Mensagens)
            AssertNaoVazaTexto(mensagem, SenhaNova);

        var copia = Path.Combine(area.Raiz, "espelho.bin");
        await File.WriteAllBytesAsync(copia, volume.Arquivos[volume.NomeArquivo]);
        using (var aberto = await LerEspelho.AbrirAsync(copia, SenhaNova, VersaoSchemaAplicacao.DoApp))
        {
            await using var snapshot = AbrirSomenteLeitura(aberto.CaminhoSqlite);
            Assert.Equal("Marina", await EscalarTextoAsync(snapshot, "SELECT Nome FROM MembrosFamilia"));
        }

        await Assert.ThrowsAsync<SenhaIncorretaException>(() =>
            LerEspelho.AbrirAsync(copia, Senha, VersaoSchemaAplicacao.DoApp));
    }

    [Fact]
    public async Task PromocaoEfetuada_FotoDoDefinitivoFalha_Publica_AdotaSenhaNova_ENaoDizQueManteve()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria
        {
            EfetuarPromocaoEFalhar = true,
            FalharCopiaDefinitivoAPartirDa = 2
        };
        var anterior = new byte[] { 1, 1, 1 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(
            volume,
            provedor,
            new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")));

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova, substituirEspelhoExistente: true);

        Assert.True(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemGravadoLeituraNaoConfirmada, resultado.Mensagem);
        Assert.NotEqual(BackupEspelhoAgora.MensagemMantido, resultado.Mensagem);
        Assert.DoesNotContain("mantido", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.NotEqual(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(anterior, volume.Arquivos[reserva]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));

        var copia = Path.Combine(area.Raiz, "espelho.bin");
        await File.WriteAllBytesAsync(copia, volume.Arquivos[volume.NomeArquivo]);
        using (var aberto = await LerEspelho.AbrirAsync(copia, SenhaNova, VersaoSchemaAplicacao.DoApp))
        {
            await using var snapshot = AbrirSomenteLeitura(aberto.CaminhoSqlite);
            Assert.Equal("Marina", await EscalarTextoAsync(snapshot, "SELECT Nome FROM MembrosFamilia"));
        }

        await Assert.ThrowsAsync<SenhaIncorretaException>(() =>
            LerEspelho.AbrirAsync(copia, Senha, VersaoSchemaAplicacao.DoApp));
    }

    [Fact]
    public async Task PassoSeguinteAoProtocoloLanca_MesmoComReleituraNula_Publica_AdotaSenhaNova_ENaoDizQueManteve()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria { FalharCopiaDefinitivoAPartirDa = 2 };
        var anterior = new byte[] { 1, 1, 1 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(
            volume,
            provedor,
            new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")),
            depoisDoProtocolo: (_, _) => throw new InvalidOperationException("passo seguinte"));

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova, substituirEspelhoExistente: true);

        Assert.True(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemGravadoLeituraNaoConfirmada, resultado.Mensagem);
        Assert.NotEqual(BackupEspelhoAgora.MensagemMantido, resultado.Mensagem);
        Assert.DoesNotContain("mantido", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.NotEqual(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));

        var copia = Path.Combine(area.Raiz, "espelho.bin");
        await File.WriteAllBytesAsync(copia, volume.Arquivos[volume.NomeArquivo]);
        using (var aberto = await LerEspelho.AbrirAsync(copia, SenhaNova, VersaoSchemaAplicacao.DoApp))
        {
            await using var snapshot = AbrirSomenteLeitura(aberto.CaminhoSqlite);
            Assert.Equal("Marina", await EscalarTextoAsync(snapshot, "SELECT Nome FROM MembrosFamilia"));
        }

        await Assert.ThrowsAsync<SenhaIncorretaException>(() =>
            LerEspelho.AbrirAsync(copia, Senha, VersaoSchemaAplicacao.DoApp));
    }

    [Fact]
    public void LogDoEspelho_GravaTipoEOmiteCaminhoEUri()
    {
        var caminho = Path.Combine(Path.GetTempPath(), "orcafacil-cache", "snap.db");
        var uri = "content://com.android.externalstorage.documents/tree/primary%3ABackup";
        var windows = @"C:\Users\x\AppData\Local\Temp\espelho.tmp";
        IOException excecao;
        try
        {
            throw new IOException($"Não foi possível ler '{caminho}' em {uri} nem {windows}.");
        }
        catch (IOException pega)
        {
            excecao = pega;
        }

        var texto = TextoLogEspelho.Montar("Falha ao publicar o espelho.", [], excecao);
        var informacao = TextoLogEspelho.Montar($"Cache em {caminho} via {uri}.", [windows], null);

        Assert.Contains("IOException", texto, StringComparison.Ordinal);
        Assert.Contains("Falha ao publicar o espelho.", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(caminho, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("snap.db", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(uri, texto, StringComparison.Ordinal);
        Assert.DoesNotContain(windows, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("content://", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("espelho.tmp", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(LogDoEspelho_GravaTipoEOmiteCaminhoEUri), texto, StringComparison.Ordinal);

        Assert.DoesNotContain(caminho, informacao, StringComparison.Ordinal);
        Assert.DoesNotContain(uri, informacao, StringComparison.Ordinal);
        Assert.DoesNotContain(windows, informacao, StringComparison.Ordinal);
        Assert.DoesNotContain("content://", informacao, StringComparison.Ordinal);
        Assert.Contains("Cache em", informacao, StringComparison.Ordinal);

        var fonte = File.ReadAllText(Path.Combine(RaizRepositorio(), "OrcaFacil/Services/Espelho/RegistroLogEspelho.cs"));
        Assert.Contains("TextoLogEspelho.Montar", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Log.Error(excecao", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Log.Information(mensagem", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParqueTrocouEFalhou_NaoAdotaSenhaNova_PorqueODefinitivoNaoEONOvo()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria { EfetuarParqueEFalhar = true };
        var anterior = new byte[] { 4, 4 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor, new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")));

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova, substituirEspelhoExistente: true);

        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemMantido, resultado.Mensagem);
        Assert.Equal(Senha, provedor.Obter());
        Assert.False(volume.Arquivos.ContainsKey(volume.NomeArquivo));
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(anterior, volume.Arquivos[reserva]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task IOExceptionComASenha_MostraFraseFixa_EOlogOmiteOSegredo()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria();
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var log = new LogEspiao();
        var backup = new BackupEspelhoAgora(
            volume,
            new DestinoQueFalha(Senha),
            provedor,
            provedor,
            VersaoSchemaAplicacao.DoApp,
            log,
            new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")));

        var resultado = await backup.ExecutarAsync(area.Banco, Senha, Senha);

        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemMantido, resultado.Mensagem);
        Assert.DoesNotContain(Senha, resultado.Mensagem, StringComparison.Ordinal);
        Assert.Null(provedor.Obter());
        Assert.False(volume.Arquivos.ContainsKey(volume.NomeArquivo));
        Assert.Equal(RegistroFalhaEspelho.DetalheOmitido, Assert.Single(log.Mensagens));
        Assert.Null(Assert.Single(log.Excecoes));
        Assert.DoesNotContain(Senha, log.Mensagens[0], StringComparison.Ordinal);
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task Cancela_DepoisDaPromocao_DefineSenhaNova_ERelancaOCE()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria
        {
            ExcecaoNaPromocao = new OperationCanceledException(),
            PromocaoRemoveTemporarioAntesDeFalhar = true
        };
        var anterior = new byte[] { 1, 1, 1 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor, new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova, substituirEspelhoExistente: true));

        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.Null(provedor.ObterAnterior());
        Assert.NotEqual(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        AssertMemoriaAbre(volume.Arquivos[volume.NomeArquivo], provedor, area.Raiz);
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task Cancela_DepoisDoMove_FotoNaoDecide_AdotaSenhaNova_ERetemAAntiga()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria
        {
            ExcecaoNaPromocao = new OperationCanceledException(),
            PromocaoRemoveTemporarioAntesDeFalhar = true,
            FalharCopiaDefinitivoAPartirDa = 2
        };
        var anterior = new byte[] { 1, 1, 1 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor, new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")));

        var excecao = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova, substituirEspelhoExistente: true));

        Assert.True(excecao.InnerException is PublicacaoEspelhoException { PromocaoEfetuada: true });
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.Equal(Senha, provedor.ObterAnterior());
        Assert.NotEqual(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
        Assert.Empty(volume.Apagados);
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(anterior, volume.Arquivos[reserva]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        AssertNaoVaza(excecao, Senha);
        AssertNaoVaza(excecao, SenhaNova);
        AssertNaoVaza(provedor, Senha);
        AssertNaoVaza(provedor, SenhaNova);
        AssertMemoriaAbre(volume.Arquivos[volume.NomeArquivo], provedor, area.Raiz);
        Assert.False(Abre(volume.Arquivos[volume.NomeArquivo], Senha, area.Raiz));
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task ExcecaoPosPromocao_ReleituraNula_AdotaSenhaNova_ERetemAAntiga()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria
        {
            ExcecaoNoDescarteReserva = new InvalidOperationException("descarte"),
            FalharCopiaDefinitivoAPartirDa = 2
        };
        var anterior = new byte[] { 1, 1, 1 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var log = new LogEspiao();
        var backup = Criar(
            volume,
            provedor,
            new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")),
            log);

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova, substituirEspelhoExistente: true);

        Assert.True(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemGravadoLeituraNaoConfirmada, resultado.Mensagem);
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.Equal(Senha, provedor.ObterAnterior());
        Assert.NotEqual(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(anterior, volume.Arquivos[reserva]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        AssertNaoVaza(provedor, Senha);
        AssertNaoVaza(provedor, SenhaNova);
        foreach (var mensagem in log.Mensagens)
        {
            AssertNaoVazaTexto(mensagem, Senha);
            AssertNaoVazaTexto(mensagem, SenhaNova);
        }

        AssertMemoriaAbre(volume.Arquivos[volume.NomeArquivo], provedor, area.Raiz);
        Assert.False(Abre(volume.Arquivos[volume.NomeArquivo], Senha, area.Raiz));
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task Homonimos_SenhaNova_MensagemComReserva_LeituraSeguinteIndisponivel()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria { HomonimosAposPromocao = true };
        var anterior = new byte[] { 2, 2 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor, new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")));

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova, substituirEspelhoExistente: true);

        Assert.True(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemGravadoComReserva, resultado.Mensagem);
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.Null(provedor.ObterAnterior());
        Assert.Empty(volume.Apagados);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(anterior, volume.Arquivos[reserva]);
        AssertMemoriaAbre(volume.Arquivos[volume.NomeArquivo], provedor, area.Raiz);

        var destino = Path.Combine(area.Raiz, "leitura-seguinte.bin");
        var leitura = await volume.CopiarParaLocalAsync(volume.NomeArquivo, destino, CancellationToken.None);
        Assert.Equal(CodigoFalhaPastaEspelho.Indisponivel, leitura.Codigo);
        Assert.Equal(MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.Indisponivel), leitura.Mensagem);
        Assert.False(File.Exists(destino));
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task HomonimosSemReserva_MensagemComReserva_NaoApagaNada()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var volume = new VolumeMemoria { HomonimosAposPromocao = true };
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor, new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache")));

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova);

        Assert.True(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemGravadoComReserva, resultado.Mensagem);
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.Null(provedor.ObterAnterior());
        Assert.Empty(volume.Apagados);
        Assert.DoesNotContain(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.DoesNotContain(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhTemporarioDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        AssertMemoriaAbre(volume.Arquivos[volume.NomeArquivo], provedor, area.Raiz);

        var destino = Path.Combine(area.Raiz, "leitura-homonimos.bin");
        var leitura = await volume.CopiarParaLocalAsync(volume.NomeArquivo, destino, CancellationToken.None);
        Assert.Equal(CodigoFalhaPastaEspelho.Indisponivel, leitura.Codigo);
        Assert.False(File.Exists(destino));
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task Matriz_SenhaEmMemoria_DecifraODefinitivo()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var cache = new AreaTemporariaEspelhoEmDisco(Path.Combine(area.Raiz, "cache"));

        var publicado = await Rodar(area, cache, volume => { });
        Assert.Equal(BackupEspelhoAgora.MensagemGravado, publicado.Resultado.Mensagem);
        Assert.Null(publicado.Provedor.ObterAnterior());
        AssertMemoriaAbre(publicado.Volume.Arquivos[publicado.Volume.NomeArquivo], publicado.Provedor, area.Raiz);
        Assert.False(Abre(publicado.Volume.Arquivos[publicado.Volume.NomeArquivo], Senha, area.Raiz));

        var semConfirmar = await Rodar(area, cache, volume =>
        {
            volume.ExcecaoNoDescarteReserva = new InvalidOperationException("descarte");
            volume.FalharCopiaDefinitivoAPartirDa = 2;
        });
        Assert.Equal(BackupEspelhoAgora.MensagemGravadoLeituraNaoConfirmada, semConfirmar.Resultado.Mensagem);
        Assert.Equal(Senha, semConfirmar.Provedor.ObterAnterior());
        AssertMemoriaAbre(semConfirmar.Volume.Arquivos[semConfirmar.Volume.NomeArquivo], semConfirmar.Provedor, area.Raiz);

        var antigo = Path.Combine(area.Raiz, "antigo.espelho");
        CofreEspelho.Gravar(antigo, [9, 8, 7], Senha, 1, "matriz", DateTimeOffset.UnixEpoch, FormatoEspelho.IteracoesMinimas);
        var bytesAntigos = File.ReadAllBytes(antigo);
        Assert.False(Abre(bytesAntigos, semConfirmar.Provedor.Obter(), area.Raiz));
        Assert.True(Abre(bytesAntigos, semConfirmar.Provedor.ObterAnterior(), area.Raiz));
        Assert.Equal(SenhaNova, semConfirmar.Provedor.Obter());
        Assert.Equal(Senha, semConfirmar.Provedor.ObterAnterior());

        var homonimos = await Rodar(area, cache, volume => volume.HomonimosAposPromocao = true);
        Assert.Equal(BackupEspelhoAgora.MensagemGravadoComReserva, homonimos.Resultado.Mensagem);
        AssertMemoriaAbre(homonimos.Volume.Arquivos[homonimos.Volume.NomeArquivo], homonimos.Provedor, area.Raiz);
        var bloqueio = await homonimos.Volume.CopiarParaLocalAsync(
            homonimos.Volume.NomeArquivo,
            Path.Combine(area.Raiz, "bloqueio.bin"),
            CancellationToken.None);
        Assert.Equal(CodigoFalhaPastaEspelho.Indisponivel, bloqueio.Codigo);

        var mantido = await Rodar(area, cache, volume => volume.EfetuarParqueEFalhar = true);
        Assert.Equal(BackupEspelhoAgora.MensagemMantido, mantido.Resultado.Mensagem);
        Assert.Equal(Senha, mantido.Provedor.Obter());
        Assert.Null(mantido.Provedor.ObterAnterior());
        Assert.False(mantido.Volume.Arquivos.ContainsKey(mantido.Volume.NomeArquivo));
        Assert.Contains("mantido", mantido.Resultado.Mensagem, StringComparison.OrdinalIgnoreCase);

        var cts = new CancellationTokenSource();
        var cancelado = new VolumeMemoria();
        var espelhoAnterior = new byte[] { 4, 4, 4 };
        cancelado.Arquivos[cancelado.NomeArquivo] = espelhoAnterior;
        cancelado.DepoisDoParque = () => cts.Cancel();
        var senhaCancelada = new ProvedorSenhaEspelhoMemoria();
        Assert.True(senhaCancelada.Definir(Senha, Senha).Aceita);
        var backupCancelado = Criar(cancelado, senhaCancelada, cache);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            backupCancelado.ExecutarAsync(area.Banco, SenhaNova, SenhaNova, cts.Token, substituirEspelhoExistente: true));
        Assert.Equal(Senha, senhaCancelada.Obter());
        Assert.Null(senhaCancelada.ObterAnterior());
        Assert.False(cancelado.Arquivos.ContainsKey(cancelado.NomeArquivo));
        var reservaCancelada = Assert.Single(
            cancelado.Arquivos,
            par => NomeTemporarioEspelho.EhReservaDesteApp(par.Key, cancelado.NomeArquivo));
        Assert.Equal(espelhoAnterior, reservaCancelada.Value);
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
    }

    private async Task<CenarioSenha> Rodar(
        AreaTemp area,
        IAreaTemporariaEspelho cache,
        Action<VolumeMemoria> preparar)
    {
        var volume = new VolumeMemoria();
        volume.Arquivos[volume.NomeArquivo] = [1, 1, 1];
        preparar(volume);
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor, cache);
        ResultadoBackupEspelho resultado;
        try
        {
            resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova, substituirEspelhoExistente: true);
        }
        catch (Exception ex)
        {
            AssertNaoVaza(ex, Senha);
            AssertNaoVaza(ex, SenhaNova);
            throw;
        }

        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        AssertNaoVaza(provedor, Senha);
        AssertNaoVaza(provedor, SenhaNova);
        return new CenarioSenha(volume, provedor, resultado);
    }

    private static void AssertMemoriaAbre(byte[] arquivo, ProvedorSenhaEspelhoMemoria provedor, string pasta)
    {
        var atual = provedor.Obter();
        Assert.False(string.IsNullOrEmpty(atual));
        if (Abre(arquivo, atual, pasta))
            return;

        var anterior = provedor.ObterAnterior();
        Assert.False(string.IsNullOrEmpty(anterior));
        Assert.True(Abre(arquivo, anterior, pasta));
    }

    private static bool Abre(byte[] arquivo, string? senha, string pasta)
    {
        if (string.IsNullOrEmpty(senha))
            return false;

        var caminho = Path.Combine(pasta, "abre-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(caminho, arquivo);
        try
        {
            var lido = CofreEspelho.Ler(caminho, senha);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(lido.Sqlite);
            return true;
        }
        catch (SenhaIncorretaException)
        {
            return false;
        }
    }

    private readonly record struct CenarioSenha(
        VolumeMemoria Volume,
        ProvedorSenhaEspelhoMemoria Provedor,
        ResultadoBackupEspelho Resultado);

    private static BackupEspelhoAgora Criar(
        VolumeMemoria volume,
        ProvedorSenhaEspelhoMemoria provedor,
        IAreaTemporariaEspelho? area = null,
        IRegistroLog? log = null,
        Func<ResultadoOperacaoPasta, CancellationToken, Task>? depoisDoProtocolo = null)
    {
        var destino = new DestinoEspelhoPasta(new ProtocoloPublicacaoEspelho(volume))
        {
            DepoisDoProtocolo = depoisDoProtocolo
        };
        return new(
            volume,
            destino,
            provedor,
            provedor,
            VersaoSchemaAplicacao.DoApp,
            log,
            area);
    }

    private static string SemComentarios(string fonte)
    {
        var texto = new StringBuilder(fonte.Length);
        for (var i = 0; i < fonte.Length; i++)
        {
            var c = fonte[i];
            if (c == '"')
            {
                texto.Append(c);
                for (i++; i < fonte.Length; i++)
                {
                    texto.Append(fonte[i]);
                    if (fonte[i] == '\\' && i + 1 < fonte.Length)
                    {
                        texto.Append(fonte[++i]);
                        continue;
                    }

                    if (fonte[i] == '"')
                        break;
                }

                continue;
            }

            if (c == '/' && i + 1 < fonte.Length && fonte[i + 1] == '/')
            {
                while (i < fonte.Length && fonte[i] != '\n')
                    i++;

                if (i < fonte.Length)
                    texto.Append('\n');
                continue;
            }

            if (c == '/' && i + 1 < fonte.Length && fonte[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < fonte.Length && !(fonte[i] == '*' && fonte[i + 1] == '/'))
                    i++;

                if (i + 1 < fonte.Length)
                    i++;
                continue;
            }

            texto.Append(c);
        }

        return texto.ToString();
    }

    private static void AssertSoAUri(PreferenciasEspiao preferencias, string senha)
    {
        Assert.Equal(UriPasta, preferencias.ValorOuNulo(UriEscolhaPasta.ChavePreferencia));
        Assert.Single(preferencias.Gravacoes);
        foreach (var (chave, valor) in preferencias.Gravacoes)
        {
            AssertNaoVazaTexto(chave, senha);
            AssertNaoVazaTexto(valor, senha);
        }
    }

    private static void AssertSemPersistencia(string fonte)
    {
        Assert.DoesNotContain("SecureStorage.", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("IPreferenciasSimples", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Preferences.", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Maui.Storage", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("ILogger", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Log.", fonte, StringComparison.Ordinal);
    }

    private static void AssertNaoVaza(object valor, string senha)
    {
        AssertNaoVazaTexto(valor.ToString(), senha);
        foreach (var propriedade in valor.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (propriedade.GetValue(valor) is string texto)
                AssertNaoVazaTexto(texto, senha);
        }
    }

    private static void AssertNaoVazaTexto(string? texto, string senha)
    {
        Assert.NotNull(texto);
        Assert.DoesNotContain(senha, texto, StringComparison.Ordinal);
    }

    private static void AssertNaoContem(byte[] arquivo, string texto)
        => Assert.True(arquivo.AsSpan().IndexOf(Encoding.UTF8.GetBytes(texto)) < 0);

    private static void AssertNaoContemArquivo(string caminho, string texto)
        => AssertNaoContem(File.ReadAllBytes(caminho), texto);

    private static string RaizRepositorio()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.Core", "OrcaFacil.Core.csproj")))
                return diretorio.FullName;

            diretorio = diretorio.Parent;
        }

        throw new InvalidOperationException("Não achei a raiz do repositório.");
    }

    private static async Task<SqliteConnection> CriarBancoAbertoAsync(string caminho, string nome)
    {
        var conexao = await AbrirAsync(caminho);
        await ExecutarAsync(conexao, "PRAGMA journal_mode = WAL;");
        await ExecutarAsync(conexao, "PRAGMA wal_autocheckpoint = 0;");
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conexao).Options;
        await using (var ctx = new AppDbContext(opcoes))
            await ctx.Database.MigrateAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
            VALUES ($nome, 'M', 0, 1, '2026-03-01 00:00:00')
            """;
        comando.Parameters.AddWithValue("$nome", nome);
        Assert.Equal(1, await comando.ExecuteNonQueryAsync());
        return conexao;
    }

    private static async Task<SqliteConnection> AbrirAsync(string caminho)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        var conexao = new SqliteConnection(builder.ConnectionString);
        await conexao.OpenAsync();
        return conexao;
    }

    private static SqliteConnection AbrirSomenteLeitura(string caminho)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        var conexao = new SqliteConnection(builder.ConnectionString);
        conexao.Open();
        return conexao;
    }

    private static async Task ExecutarAsync(SqliteConnection conexao, string sql)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<string> EscalarTextoAsync(SqliteConnection conexao, string sql)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        var valor = await comando.ExecuteScalarAsync();
        return valor is null or DBNull ? "" : Convert.ToString(valor) ?? "";
    }

    private sealed class AreaTemp : IDisposable
    {
        public AreaTemp()
        {
            Raiz = Directory.CreateTempSubdirectory("orcafacil-backup-").FullName;
            Banco = Path.Combine(Raiz, "orcafacil.db");
        }

        public string Raiz { get; }

        public string Banco { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Raiz))
                    Directory.Delete(Raiz, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class PreferenciasEspiao : IPreferenciasSimples
    {
        private readonly Dictionary<string, string> _valores = new(StringComparer.Ordinal);

        public List<(string Chave, string Valor)> Gravacoes { get; } = [];

        public bool Contem(string chave) => _valores.ContainsKey(chave);

        public string Obter(string chave, string padrao) =>
            _valores.TryGetValue(chave, out var valor) ? valor : padrao;

        public void Definir(string chave, string valor)
        {
            Gravacoes.Add((chave, valor));
            _valores[chave] = valor;
        }

        public void Limpar() => _valores.Clear();

        public string? ValorOuNulo(string chave) =>
            _valores.TryGetValue(chave, out var valor) ? valor : null;
    }

    private sealed class LogEspiao : IRegistroLog
    {
        public List<string> Mensagens { get; } = [];

        public List<Exception?> Excecoes { get; } = [];

        public void Informacao(string mensagem, object?[] argumentos) => Mensagens.Add(mensagem);

        public void Erro(Exception? excecao, string mensagem, object?[] argumentos)
        {
            Excecoes.Add(excecao);
            Mensagens.Add(mensagem);
        }
    }

    private sealed class DestinoQueFalha : IDestinoEspelho
    {
        private readonly string _texto;

        public DestinoQueFalha(string texto) => _texto = texto;

        public Task PublicarAsync(string arquivoOrigem, CancellationToken cancellationToken = default)
            => throw new IOException("falha " + _texto);
    }

    private static async Task MigrarVazioAsync(string caminho)
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={caminho};Pooling=False")
            .Options;
        await using var ctx = new AppDbContext(opcoes);
        await ctx.Database.MigrateAsync();
    }

    private static async Task SemearDemonstracaoAsync(string caminho)
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={caminho};Pooling=False")
            .Options;
        await using var ctx = new AppDbContext(opcoes);
        await ctx.Database.MigrateAsync();
        ctx.Categorias.AddRange(CatalogoDemonstracao.CriarCategorias());
        var perfil = CatalogoDemonstracao.CriarPerfil();
        ctx.MembrosFamilia.Add(perfil);
        await ctx.SaveChangesAsync();
        CatalogoDemonstracao.AdicionarDemonstracao(ctx, perfil, new DateTime(2026, 10, 19));
        await ctx.SaveChangesAsync();
    }

    private static async Task SemearDemonstracaoComMesDeOrcamentoAlteradoAsync(string caminho)
    {
        await SemearDemonstracaoAsync(caminho);
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={caminho};Pooling=False")
            .Options;
        await using var ctx = new AppDbContext(opcoes);
        var orcamento = await ctx.Orcamentos.FirstAsync(item => item.Categoria!.Nome == "Alimentação");
        orcamento.Mes = orcamento.Mes == 1 ? 12 : orcamento.Mes - 1;
        await ctx.SaveChangesAsync();
    }

    private static async Task InserirSoPerfilMinimoAsync(string caminho)
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={caminho};Pooling=False")
            .Options;
        await using var ctx = new AppDbContext(opcoes);
        await ctx.Database.MigrateAsync();
        ctx.MembrosFamilia.Add(CatalogoDemonstracao.CriarPerfil());
        await ctx.SaveChangesAsync();
    }

    private static async Task SemearDemonstracaoComLancamentoRealAsync(string caminho)
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={caminho};Pooling=False")
            .Options;
        await using var ctx = new AppDbContext(opcoes);
        await ctx.Database.MigrateAsync();
        ctx.Categorias.AddRange(CatalogoDemonstracao.CriarCategorias());
        var perfil = CatalogoDemonstracao.CriarPerfil();
        ctx.MembrosFamilia.Add(perfil);
        await ctx.SaveChangesAsync();
        CatalogoDemonstracao.AdicionarDemonstracao(ctx, perfil, new DateTime(2026, 10, 19));
        var alimentacao = ctx.Categorias.Single(categoria => categoria.Nome == "Alimentação");
        ctx.Transacoes.Add(new Transacao
        {
            Descricao = "Padaria",
            Valor = 12.50m,
            Data = new DateTime(2026, 10, 2),
            Tipo = TipoTransacao.Despesa,
            Recorrencia = TipoRecorrencia.Unica,
            CategoriaId = alimentacao.Id,
            MembroFamiliaId = perfil.Id
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task InserirSoLogAsync(string caminho)
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={caminho};Pooling=False")
            .Options;
        await using var ctx = new AppDbContext(opcoes);
        await ctx.Database.MigrateAsync();
        ctx.LogsEvento.Add(new LogEvento { Tipo = TipoLogEvento.Sistema, Mensagem = "abriu" });
        await ctx.SaveChangesAsync();
    }
}
