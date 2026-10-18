using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Armazenamento;

public class ProtocoloPublicacaoEspelhoTests
{
    [Fact]
    public async Task PastaVazia_PublicaNoNomeDefinitivo_SemDeixarTemporario()
    {
        var volume = new VolumeMemoria();
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([4, 5, 6]));

        Assert.True(resultado.Sucesso);
        Assert.Equal(new byte[] { 4, 5, 6 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(FormatoEspelho.NomeArquivoSugerido, volume.NomeArquivo);
        Assert.Empty(volume.Apagados);
        Assert.DoesNotContain(volume.Arquivos.Keys, nome => nome.EndsWith(".tmp", StringComparison.Ordinal));
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task ComEspelhoAnterior_SubstituiESoApagaAReserva()
    {
        var volume = new VolumeMemoria();
        volume.Arquivos[volume.NomeArquivo] = [1, 1, 1];
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([2, 2]));

        Assert.True(resultado.Sucesso);
        Assert.Equal(new byte[] { 2, 2 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.Single(volume.Apagados);
        Assert.True(NomeTemporarioEspelho.EhReservaDesteApp(volume.Apagados[0], volume.NomeArquivo));
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
        Assert.DoesNotContain("foto.jpg", volume.Apagados);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task SemEspaco_MantemOAnterior_ERemoveOParcial()
    {
        var volume = new VolumeMemoria();
        volume.Arquivos[volume.NomeArquivo] = [7, 7];
        volume.FalhaAoCriar = CodigoFalhaPastaEspelho.SemEspaco;
        volume.DeixarParcialAoFalharCriacao = true;
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([3]));

        Assert.Equal(CodigoFalhaPastaEspelho.SemEspaco, resultado.Codigo);
        Assert.Contains("arquivo anterior foi mantido", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new byte[] { 7, 7 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.Single(volume.Apagados);
        Assert.True(NomeTemporarioEspelho.EhTemporarioDesteApp(volume.Apagados[0], volume.NomeArquivo));
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Theory]
    [InlineData(CodigoFalhaPastaEspelho.PermissaoRevogada)]
    [InlineData(CodigoFalhaPastaEspelho.PastaApagada)]
    [InlineData(CodigoFalhaPastaEspelho.PastaNaoEscolhida)]
    public async Task SemAcesso_NaoCriaNemApaga(CodigoFalhaPastaEspelho codigo)
    {
        var volume = new VolumeMemoria { Acesso = codigo };
        volume.Arquivos[volume.NomeArquivo] = [8];
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([1]));

        Assert.Equal(codigo, resultado.Codigo);
        Assert.Equal(new byte[] { 8 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.Empty(volume.Apagados);
        Assert.Equal(2, volume.Arquivos.Count);
    }

    [Fact]
    public async Task ProvedorSemRename_MantemOAnteriorNoNomeDefinitivo()
    {
        var volume = new VolumeMemoria { FalharParque = true };
        volume.Arquivos[volume.NomeArquivo] = [5, 5];
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([6]));

        Assert.Equal(CodigoFalhaPastaEspelho.ProvedorSemTroca, resultado.Codigo);
        Assert.Contains("armazenamento do aparelho", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new byte[] { 5, 5 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.DoesNotContain(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.All(volume.Apagados, nome => Assert.True(NomeTemporarioEspelho.EhTemporarioDesteApp(nome, volume.NomeArquivo)));
    }

    [Fact]
    public async Task PromocaoFalha_DevolveAReservaAoNomeDefinitivo()
    {
        var volume = new VolumeMemoria { FalharPromocao = true };
        volume.Arquivos[volume.NomeArquivo] = [4, 4, 4];
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([1, 2]));

        Assert.Equal(CodigoFalhaPastaEspelho.ProvedorSemTroca, resultado.Codigo);
        Assert.Equal(new byte[] { 4, 4, 4 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.DoesNotContain(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
    }

    [Fact]
    public async Task VoltaDaReservaFalha_PreservaOAnteriorForaDoNome_ENaoApaga()
    {
        var volume = new VolumeMemoria { FalharPromocao = true, FalharVolta = true };
        volume.Arquivos[volume.NomeArquivo] = [3, 3];
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([9, 9]));

        Assert.Equal(CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome, resultado.Codigo);
        Assert.Contains("não foi apagado", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.False(volume.Arquivos.ContainsKey(volume.NomeArquivo));
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 3, 3 }, volume.Arquivos[reserva]);
        Assert.DoesNotContain(reserva, volume.Apagados);
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task TrocaNaoConfirmada_NaoApagaOEspelhoAnterior()
    {
        var volume = new VolumeMemoria { TrocaNaoConfirmadaNoParque = true };
        volume.Arquivos[volume.NomeArquivo] = [2, 2, 2];
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([1]));

        Assert.Equal(CodigoFalhaPastaEspelho.TrocaNaoConfirmada, resultado.Codigo);
        Assert.Equal(new byte[] { 2, 2, 2 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
    }

    [Fact]
    public async Task ReservaNaoSai_SeOFinalNaoEstiverConfirmado()
    {
        var volume = new VolumeMemoria { FalharDescarteReserva = true };
        volume.Arquivos[volume.NomeArquivo] = [1];
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([8, 8]));

        Assert.Equal(CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome, resultado.Codigo);
        Assert.Equal(new byte[] { 8, 8 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.Contains(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.DoesNotContain(volume.Apagados, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
    }

    [Fact]
    public async Task ParqueJaRenomeadoEFalha_NaoApaga_EReservaFicaVisivel()
    {
        var volume = new VolumeMemoria { EfetuarParqueEFalhar = true };
        volume.Arquivos[volume.NomeArquivo] = [4, 4];
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([8]));

        Assert.Equal(CodigoFalhaPastaEspelho.TrocaNaoConfirmada, resultado.Codigo);
        Assert.False(resultado.Sucesso);
        Assert.Empty(volume.Apagados);
        Assert.False(volume.Arquivos.ContainsKey(volume.NomeArquivo));
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 4, 4 }, volume.Arquivos[reserva]);
        var temporario = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhTemporarioDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 8 }, volume.Arquivos[temporario]);
        Assert.True(MapeamentoLeituraPastaEspelho.ExisteParaOCoordenador(volume.Consultar()));
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task PromocaoJaRenomeadaEFalha_MantemDefinitivoNovoEReserva()
    {
        var volume = new VolumeMemoria { EfetuarPromocaoEFalhar = true };
        volume.Arquivos[volume.NomeArquivo] = [1, 1];
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([6, 6]));

        Assert.Equal(CodigoFalhaPastaEspelho.TrocaNaoConfirmada, resultado.Codigo);
        Assert.Empty(volume.Apagados);
        Assert.Equal(new byte[] { 6, 6 }, volume.Arquivos[volume.NomeArquivo]);
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 1, 1 }, volume.Arquivos[reserva]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task CancelarDepoisDoParque_NaoApagaNada()
    {
        var volume = new VolumeMemoria();
        volume.Arquivos[volume.NomeArquivo] = [2, 2];
        var cts = new CancellationTokenSource();
        volume.DepoisDoParque = () => cts.Cancel();
        var protocolo = Protocolo(volume);

        await Assert.ThrowsAsync<OperationCanceledException>(() => protocolo.PublicarAsync(Arquivo([3]), cts.Token));

        Assert.Empty(volume.Apagados);
        Assert.False(volume.Arquivos.ContainsKey(volume.NomeArquivo));
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 2, 2 }, volume.Arquivos[reserva]);
        Assert.Contains(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhTemporarioDesteApp(nome, volume.NomeArquivo));
        Assert.True(MapeamentoLeituraPastaEspelho.ExisteParaOCoordenador(volume.Consultar()));
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task Publicacoes_NaoConcorrem()
    {
        var volume = new VolumeMemoria();
        var segurar = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        volume.SegurarCriacao = segurar;
        var protocolo = Protocolo(volume);

        var primeira = protocolo.PublicarAsync(Arquivo([1, 2]));
        await volume.CriacaoEntrou.Task;

        var segunda = protocolo.PublicarAsync(Arquivo([3, 4]));
        await Task.Yield();
        Assert.Equal(1, volume.CriacoesIniciadas);
        Assert.Equal(1, volume.Consultas);
        Assert.False(segunda.IsCompleted);

        segurar.TrySetResult(true);
        await primeira;
        await segunda;

        Assert.Equal(2, volume.CriacoesIniciadas);
        Assert.Equal(new byte[] { 3, 4 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task RenameViraCopia_NaoApagaNada_EDefinitivoTemOsBytesNovos()
    {
        var volume = new VolumeMemoria { PromocaoViraCopia = true };
        volume.Arquivos[volume.NomeArquivo] = [1, 1, 1];
        var destino = new DestinoEspelhoPasta(Protocolo(volume));

        var resultado = await destino.PublicarComResultadoAsync(Arquivo([7, 7, 7]));

        Assert.Equal(CodigoFalhaPastaEspelho.PublicadoComResiduo, resultado.Codigo);
        Assert.True(resultado.Sucesso);
        Assert.Contains("não foi apagada", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new byte[] { 7, 7, 7 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.Empty(volume.Apagados);
        var temporario = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhTemporarioDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 7, 7, 7 }, volume.Arquivos[temporario]);
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 1, 1, 1 }, volume.Arquivos[reserva]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task PastaVazia_RenameViraCopia_MantemOTemporarioEGravaODefinitivo()
    {
        var volume = new VolumeMemoria { PromocaoViraCopia = true };
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([4, 5]));

        Assert.Equal(CodigoFalhaPastaEspelho.PublicadoComResiduo, resultado.Codigo);
        Assert.True(resultado.Sucesso);
        Assert.Equal(new byte[] { 4, 5 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.Empty(volume.Apagados);
        var temporario = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhTemporarioDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 4, 5 }, volume.Arquivos[temporario]);
        Assert.DoesNotContain(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task ParqueViraCopia_NaoApagaOrigemNemTemporario()
    {
        var volume = new VolumeMemoria { ParqueViraCopia = true };
        volume.Arquivos[volume.NomeArquivo] = [5, 5];
        var protocolo = Protocolo(volume);

        var resultado = await protocolo.PublicarAsync(Arquivo([6]));

        Assert.Equal(CodigoFalhaPastaEspelho.ProvedorSemTroca, resultado.Codigo);
        Assert.False(resultado.Sucesso);
        Assert.Equal(new byte[] { 5, 5 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.Empty(volume.Apagados);
        var temporario = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhTemporarioDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 6 }, volume.Arquivos[temporario]);
        var reserva = Assert.Single(volume.Arquivos.Keys, nome => NomeTemporarioEspelho.EhReservaDesteApp(nome, volume.NomeArquivo));
        Assert.Equal(new byte[] { 5, 5 }, volume.Arquivos[reserva]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task Destino_FalhaViraIOException_SemUri()
    {
        const string uri = "content://com.android.externalstorage.documents/tree/primary%3ASegredo";
        var volume = new VolumeMemoria { ExcecaoAoCriar = new IOException("não gravou " + uri + " em /storage/emulated/0/Orca") };
        var destino = new DestinoEspelhoPasta(Protocolo(volume));

        var excecao = await Assert.ThrowsAsync<IOException>(() => destino.PublicarAsync(Arquivo([1])));

        Assert.DoesNotContain(uri, excecao.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("/storage/", excecao.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(uri, destino.ToString(), StringComparison.Ordinal);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Fact]
    public async Task Volume_RecusaApagarNomeAlheio()
    {
        var volume = new VolumeMemoria();
        var resultado = await volume.DescartarTemporarioAsync("foto.jpg", CancellationToken.None);
        var reserva = await volume.DescartarReservaAposTrocaAsync(volume.NomeArquivo, CancellationToken.None);

        Assert.Equal(CodigoFalhaPastaEspelho.NomeRecusado, resultado.Codigo);
        Assert.Equal(CodigoFalhaPastaEspelho.NomeRecusado, reserva.Codigo);
        Assert.Empty(volume.Apagados);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    private static ProtocoloPublicacaoEspelho Protocolo(VolumeMemoria volume)
        => new(volume, () => Guid.NewGuid());

    private static string Arquivo(byte[] bytes)
    {
        var caminho = Path.Combine(Path.GetTempPath(), "orcafacil-pub-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(caminho, bytes);
        return caminho;
    }
}

internal sealed class VolumeMemoria : IVolumePastaEspelho
{
        public Dictionary<string, byte[]> Arquivos { get; } = new(StringComparer.Ordinal)
        {
            ["foto.jpg"] = [9]
        };

        public List<string> Apagados { get; } = [];

        public CodigoFalhaPastaEspelho Acesso { get; set; } = CodigoFalhaPastaEspelho.Nenhuma;

        public CodigoFalhaPastaEspelho? FalhaAoCriar { get; set; }

        public bool DeixarParcialAoFalharCriacao { get; set; }

        public bool FalharParque { get; set; }

        public bool EfetuarParqueEFalhar { get; set; }

        public bool ParqueViraCopia { get; set; }

        public bool PromocaoViraCopia { get; set; }

        public bool FalharPromocao { get; set; }

        public bool EfetuarPromocaoEFalhar { get; set; }

        public Action? DepoisDoParque { get; set; }

        public TaskCompletionSource<bool>? SegurarCriacao { get; set; }

        public TaskCompletionSource<bool> CriacaoEntrou { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CriacoesIniciadas { get; private set; }

        public int Consultas { get; private set; }

        public bool FalharVolta { get; set; }

        public bool FalharDescarteReserva { get; set; }

        public bool TrocaNaoConfirmadaNoParque { get; set; }

        public Exception? ExcecaoAoCriar { get; set; }

        public bool EstourarConsulta { get; set; }

        public string NomeArquivo => FormatoEspelho.NomeArquivoSugerido;

        public DiagnosticoPastaEspelho Consultar()
        {
            Consultas++;
            if (EstourarConsulta)
                throw new InvalidOperationException("consulta da pasta");

            if (Acesso != CodigoFalhaPastaEspelho.Nenhuma)
                return new DiagnosticoPastaEspelho(Acesso, false);

            var final = Arquivos.ContainsKey(NomeArquivo);
            string? reserva = null;
            var residuo = false;
            foreach (var nome in Arquivos.Keys)
            {
                if (NomeTemporarioEspelho.EhReservaDesteApp(nome, NomeArquivo))
                {
                    residuo = true;
                    reserva ??= nome;
                }
                else if (NomeTemporarioEspelho.EhTemporarioDesteApp(nome, NomeArquivo))
                {
                    residuo = true;
                }
            }

            return new DiagnosticoPastaEspelho(
                CodigoFalhaPastaEspelho.Nenhuma,
                final,
                residuo,
                final ? null : reserva);
        }

        public async Task<ResultadoOperacaoPasta> CriarTemporarioAsync(
            string nomeTemporario,
            string arquivoOrigemLocal,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recusa = RegrasPastaEspelho.ImpedirCriacao(NomeArquivo, nomeTemporario);
            if (recusa is CodigoFalhaPastaEspelho codigo)
                return ResultadoOperacaoPasta.Falha(codigo);

            CriacoesIniciadas++;
            CriacaoEntrou.TrySetResult(true);
            if (SegurarCriacao is not null)
                await SegurarCriacao.Task.WaitAsync(cancellationToken);

            if (ExcecaoAoCriar is not null)
                throw ExcecaoAoCriar;

            if (FalhaAoCriar is CodigoFalhaPastaEspelho falha)
            {
                if (DeixarParcialAoFalharCriacao)
                    Arquivos[nomeTemporario] = [0];

                return ResultadoOperacaoPasta.Falha(falha);
            }

            Arquivos[nomeTemporario] = File.ReadAllBytes(arquivoOrigemLocal);
            return ResultadoOperacaoPasta.Ok();
        }

        public Task<ResultadoRenomeacaoVolume> RenomearAsync(
            string nomeAtual,
            string nomeNovo,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!RegrasPastaEspelho.PodeRenomear(NomeArquivo, nomeAtual, nomeNovo))
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.NomeRecusado));

            if (Arquivos.ContainsKey(nomeNovo))
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca, destinoJaExistia: true));

            var parque = nomeAtual == NomeArquivo && NomeTemporarioEspelho.EhReservaDesteApp(nomeNovo, NomeArquivo);
            var promocao = NomeTemporarioEspelho.EhTemporarioDesteApp(nomeAtual, NomeArquivo) && nomeNovo == NomeArquivo;
            var volta = NomeTemporarioEspelho.EhReservaDesteApp(nomeAtual, NomeArquivo) && nomeNovo == NomeArquivo;

            if (parque && TrocaNaoConfirmadaNoParque)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.TrocaNaoConfirmada));
            if (parque && FalharParque)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca));
            if (parque && EfetuarParqueEFalhar)
                return MoverEFalhar(nomeAtual, nomeNovo);
            if ((parque && ParqueViraCopia) || (promocao && PromocaoViraCopia))
                return CopiarSemExcluirOrigem(nomeAtual, nomeNovo);
            if (promocao && FalharPromocao)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca));
            if (promocao && EfetuarPromocaoEFalhar)
                return MoverEFalhar(nomeAtual, nomeNovo);
            if (volta && FalharVolta)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca));

            if (!Arquivos.TryGetValue(nomeAtual, out var bytes))
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ArquivoAusente));

            Arquivos.Remove(nomeAtual);
            Arquivos[nomeNovo] = bytes;
            if (parque)
                DepoisDoParque?.Invoke();

            return Task.FromResult(ResultadoRenomeacaoVolume.Ok());
        }

        private Task<ResultadoRenomeacaoVolume> MoverEFalhar(string nomeAtual, string nomeNovo)
        {
            if (!Arquivos.TryGetValue(nomeAtual, out var bytes))
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ArquivoAusente));

            Arquivos.Remove(nomeAtual);
            Arquivos[nomeNovo] = bytes.ToArray();
            return Task.FromResult(ResultadoRenomeacaoVolume.FalhaAposEfetuar(CodigoFalhaPastaEspelho.TrocaNaoConfirmada));
        }

        private Task<ResultadoRenomeacaoVolume> CopiarSemExcluirOrigem(string nomeAtual, string nomeNovo)
        {
            if (!Arquivos.TryGetValue(nomeAtual, out var bytes))
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ArquivoAusente));

            Arquivos[nomeNovo] = bytes.ToArray();
            return Task.FromResult(ResultadoRenomeacaoVolume.CopiaSemExcluirOrigem());
        }

        public Task<ResultadoOperacaoPasta> DescartarTemporarioAsync(
            string nomeTemporario,
            CancellationToken cancellationToken)
        {
            var recusa = RegrasPastaEspelho.ImpedirDescarteTemporario(NomeArquivo, nomeTemporario);
            if (recusa is CodigoFalhaPastaEspelho codigo)
                return Task.FromResult(ResultadoOperacaoPasta.Falha(codigo));

            if (Arquivos.Remove(nomeTemporario))
                Apagados.Add(nomeTemporario);

            return Task.FromResult(ResultadoOperacaoPasta.Ok());
        }

        public Task<ResultadoOperacaoPasta> DescartarReservaAposTrocaAsync(
            string nomeReserva,
            CancellationToken cancellationToken)
        {
            var presente = Arquivos.ContainsKey(NomeArquivo);
            var recusa = RegrasPastaEspelho.ImpedirDescarteReserva(NomeArquivo, nomeReserva, presente);
            if (recusa is CodigoFalhaPastaEspelho codigo)
                return Task.FromResult(ResultadoOperacaoPasta.Falha(codigo));

            if (FalharDescarteReserva)
                return Task.FromResult(ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome));

            if (Arquivos.Remove(nomeReserva))
                Apagados.Add(nomeReserva);

            return Task.FromResult(ResultadoOperacaoPasta.Ok());
        }

        public Task<ResultadoOperacaoPasta> CopiarParaLocalAsync(
            string nome,
            string caminhoLocal,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var podeLer = string.Equals(nome, NomeArquivo, StringComparison.Ordinal)
                || NomeTemporarioEspelho.EhReservaDesteApp(nome, NomeArquivo);
            if (!podeLer)
                return Task.FromResult(ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.NomeRecusado));

            if (!Arquivos.TryGetValue(nome, out var bytes))
                return Task.FromResult(ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ArquivoAusente));

            File.WriteAllBytes(caminhoLocal, bytes);
            return Task.FromResult(ResultadoOperacaoPasta.Ok());
        }
}

public class EspelhoNaPastaUsuarioTests
{
    [Fact]
    public async Task PermissaoRevogada_OuPastaApagada_NaoPareceAusente_ENaoLe()
    {
        var leitor = new LeitorFalso();
        var revogada = new VolumeMemoria { Acesso = CodigoFalhaPastaEspelho.PermissaoRevogada };
        var espelho = Novo(revogada, leitor);

        Assert.True(espelho.Existe());
        var leitura = await espelho.LerAsync();
        Assert.False(leitura.Legivel);
        Assert.Contains("revogada", leitura.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, leitor.Chamadas);

        var apagada = new VolumeMemoria { Acesso = CodigoFalhaPastaEspelho.PastaApagada };
        var leituraApagada = await Novo(apagada, leitor).LerAsync();
        Assert.True(Novo(apagada, leitor).Existe());
        Assert.Contains("apagada", leituraApagada.Motivo, StringComparison.OrdinalIgnoreCase);

        var semEspaco = new VolumeMemoria { Acesso = CodigoFalhaPastaEspelho.SemEspaco };
        Assert.True(Novo(semEspaco, leitor).Existe());
        Assert.Contains("espaço", (await Novo(semEspaco, leitor).LerAsync()).Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(CodigoFalhaPastaEspelho.PermissaoRevogada)]
    [InlineData(CodigoFalhaPastaEspelho.PastaApagada)]
    [InlineData(CodigoFalhaPastaEspelho.SemEspaco)]
    [InlineData(CodigoFalhaPastaEspelho.Indisponivel)]
    public async Task Coordenador_SemBancoEPastaInacessivel_NaoSemeiaNemApaga(CodigoFalhaPastaEspelho codigo)
    {
        var volume = new VolumeMemoria { Acesso = codigo };
        var semeadura = new SemeaduraContada();
        var migracao = new MigracaoQueNaoRoda();
        var limpeza = new LimpezaContada();
        var preservacao = new PreservacaoContada();
        var restauracao = new RestauracaoContada();
        var perfil = new PerfilQueNaoRoda();
        var coordenador = new CoordenadorArmazenamento(
            new SondaAusente(),
            new EspelhoNaPastaUsuario(volume, new LeitorFalso(), new AreaFalsa()),
            new VersaoFixa(),
            restauracao,
            migracao,
            semeadura,
            limpeza,
            new OpcoesPreparoBanco { VersaoAplicativo = "1.0-teste" },
            preservacao,
            perfil);

        var resultado = await coordenador.PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(SituacaoBancoVivo.Ausente, resultado.Estado!.BancoVivo);
        Assert.Equal(SituacaoEspelho.Corrompido, resultado.Estado.Espelho);
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(0, migracao.Chamadas);
        Assert.Equal(0, restauracao.Chamadas);
        Assert.Equal(0, preservacao.Chamadas);
        Assert.Equal(0, perfil.Chamadas);
        Assert.Equal(0, limpeza.RemocoesBanco);
        Assert.Equal(0, limpeza.RemocoesEspelho);
        Assert.Empty(volume.Apagados);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PastaSoComReservaOuTemporario_NaoSemeiaNemApaga(bool reserva)
    {
        var volume = new VolumeMemoria();
        var nome = reserva
            ? NomeTemporarioEspelho.CriarReserva(volume.NomeArquivo, Guid.NewGuid())
            : NomeTemporarioEspelho.Criar(volume.NomeArquivo, Guid.NewGuid());
        volume.Arquivos[nome] = [5, 5, 5];
        var semeadura = new SemeaduraContada();
        var migracao = new MigracaoQueNaoRoda();
        var limpeza = new LimpezaContada();
        var preservacao = new PreservacaoContada();
        var restauracao = new RestauracaoContada();
        var perfil = new PerfilQueNaoRoda();
        var leitor = new LeitorFalso();
        var espelho = new EspelhoNaPastaUsuario(volume, leitor, new AreaFalsa());
        var coordenador = new CoordenadorArmazenamento(
            new SondaAusente(),
            espelho,
            new VersaoFixa(),
            restauracao,
            migracao,
            semeadura,
            limpeza,
            new OpcoesPreparoBanco { VersaoAplicativo = "1.0-teste" },
            preservacao,
            perfil);

        Assert.True(espelho.Existe());
        var resultado = await coordenador.PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(SituacaoBancoVivo.Ausente, resultado.Estado!.BancoVivo);
        Assert.Equal(SituacaoEspelho.Corrompido, resultado.Estado.Espelho);
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(0, migracao.Chamadas);
        Assert.Equal(0, restauracao.Chamadas);
        Assert.Equal(0, preservacao.Chamadas);
        Assert.Equal(0, perfil.Chamadas);
        Assert.Equal(0, limpeza.RemocoesBanco);
        Assert.Equal(0, limpeza.RemocoesEspelho);
        Assert.Empty(volume.Apagados);
        Assert.Equal(new byte[] { 5, 5, 5 }, volume.Arquivos[nome]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        if (reserva)
            Assert.Equal(1, leitor.Chamadas);
    }

    [Fact]
    public void PastaNaoEscolhida_OuVazia_EAusente()
    {
        Assert.False(Novo(new VolumeMemoria { Acesso = CodigoFalhaPastaEspelho.PastaNaoEscolhida }, new LeitorFalso()).Existe());
        Assert.False(Novo(new VolumeMemoria(), new LeitorFalso()).Existe());
    }

    [Fact]
    public void ConsultaQueEstoura_NaoPareceAusente()
    {
        var volume = new VolumeMemoria { EstourarConsulta = true };
        Assert.True(Novo(volume, new LeitorFalso()).Existe());
    }

    [Fact]
    public async Task ArquivoPresente_DelegaALeituraLocal_EApagaSoOCache()
    {
        var volume = new VolumeMemoria();
        volume.Arquivos[volume.NomeArquivo] = [4, 5];
        var leitor = new LeitorFalso { Resposta = LeituraEspelho.Ok(3) };
        var area = new AreaFalsa();
        var espelho = new EspelhoNaPastaUsuario(volume, leitor, area);

        Assert.True(espelho.Existe());
        var leitura = await espelho.LerAsync();

        Assert.True(leitura.Legivel);
        Assert.Equal(3, leitura.VersaoSchema);
        Assert.Equal(1, leitor.Chamadas);
        Assert.Equal(new byte[] { 4, 5 }, await File.ReadAllBytesAsync(leitor.Caminho!));
        Assert.Contains(leitor.Caminho, area.Descartados);
        Assert.False(volume.Arquivos.Keys.Except([volume.NomeArquivo, "foto.jpg"]).Any());
        Assert.Empty(volume.Apagados);
    }

    private static EspelhoNaPastaUsuario Novo(VolumeMemoria volume, LeitorFalso leitor)
        => new(volume, leitor, new AreaFalsa());

    private sealed class SondaAusente : ISondaBancoVivo
    {
        public bool Existe() => false;

        public Task<RelatorioIntegridadeBanco> VerificarAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("não devia sondar o banco");
    }

    private sealed class VersaoFixa : IVersaoSchemaApp
    {
        public int Atual => 1;
    }

    private sealed class RestauracaoContada : IRestauracaoEspelho
    {
        public int Chamadas { get; private set; }

        public Task<ResultadoRestauracao> RestaurarAsync(CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(ResultadoRestauracao.Falha("não devia restaurar"));
        }
    }

    private sealed class MigracaoQueNaoRoda : IMigracaoBancoVivo
    {
        public int Chamadas { get; private set; }

        public Task<ResultadoPreparoBanco> MigrarEVerificarAsync(
            OpcoesPreparoBanco opcoes,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(opcoes);
            Chamadas++;
            throw new InvalidOperationException("não devia migrar");
        }
    }

    private sealed class PerfilQueNaoRoda : IPresencaPerfil
    {
        public int Chamadas { get; private set; }

        public Task<bool> HaMembroAsync(CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(false);
        }
    }

    private sealed class PreservacaoContada : IPreservacaoBancoVivo
    {
        public int Chamadas { get; private set; }

        public Task<ResultadoPreservacao> PreservarAsync(CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(ResultadoPreservacao.Falha("não devia preservar"));
        }
    }

    private sealed class SemeaduraContada : ISemeaduraDados
    {
        public int Chamadas { get; private set; }

        public Task SemearAsync(CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.CompletedTask;
        }
    }

    private sealed class LimpezaContada : ILimpezaTotalDados
    {
        public int RemocoesBanco { get; private set; }

        public int RemocoesEspelho { get; private set; }

        public Task RemoverBancoVivoAsync(CancellationToken cancellationToken = default)
        {
            RemocoesBanco++;
            return Task.CompletedTask;
        }

        public Task RemoverEspelhoAsync(CancellationToken cancellationToken = default)
        {
            RemocoesEspelho++;
            return Task.CompletedTask;
        }
    }

    private sealed class LeitorFalso : ILeitorEspelhoLocal
    {
        public int Chamadas { get; private set; }

        public string? Caminho { get; private set; }

        public LeituraEspelho Resposta { get; set; } = LeituraEspelho.Ilegivel("não devia ler");

        public Task<LeituraEspelho> LerAsync(string caminhoLocal, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            Caminho = caminhoLocal;
            return Task.FromResult(Resposta);
        }
    }
}

public class PlanoBEspelhoTests
{
    [Fact]
    public async Task Exportar_SemArquivoOuSemPermissao_NaoCompartilha()
    {
        var volume = new VolumeMemoria();
        var share = new ShareFalso();
        var plano = Plano(volume, share, new SeletorArquivoFalso());

        var ausente = await plano.ExportarAsync();
        volume.Acesso = CodigoFalhaPastaEspelho.PermissaoRevogada;
        var revogada = await plano.ExportarAsync();

        Assert.Equal(CodigoFalhaPastaEspelho.ArquivoAusente, ausente.Codigo);
        Assert.Equal(CodigoFalhaPastaEspelho.PermissaoRevogada, revogada.Codigo);
        Assert.Equal(0, share.Chamadas);
        Assert.Empty(volume.Apagados);
    }

    [Fact]
    public async Task Exportar_EntregaOCache_ENaoApagaOEspelho()
    {
        var volume = new VolumeMemoria();
        volume.Arquivos[volume.NomeArquivo] = [1, 2, 3];
        var share = new ShareFalso();
        var area = new AreaFalsa();
        var plano = new PlanoBEspelho(volume, new DestinoEspelhoPasta(new ProtocoloPublicacaoEspelho(volume)), area, share, new SeletorArquivoFalso());

        var resultado = await plano.ExportarAsync();

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, share.Chamadas);
        Assert.Equal(PlanoBEspelho.TituloCompartilhamento, share.Titulo);
        Assert.DoesNotContain(share.Caminho, area.Descartados);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(share.Caminho!));
        Assert.Empty(volume.Apagados);
    }

    [Fact]
    public async Task ImportarCancelada_NaoPublica()
    {
        var volume = new VolumeMemoria();
        volume.Arquivos[volume.NomeArquivo] = [6];
        var seletor = new SeletorArquivoFalso { Codigo = CodigoFalhaPastaEspelho.Cancelado };
        var plano = Plano(volume, new ShareFalso(), seletor);

        var resultado = await plano.ImportarAsync();

        Assert.Equal(CodigoFalhaPastaEspelho.Cancelado, resultado.Codigo);
        Assert.Equal(new byte[] { 6 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.Empty(volume.Apagados);
    }

    [Fact]
    public async Task Importar_PublicaPeloMesmoProtocolo_EDescartaSoOCache()
    {
        var volume = new VolumeMemoria();
        volume.Arquivos[volume.NomeArquivo] = [6];
        var area = new AreaFalsa();
        var origem = area.CriarArquivo();
        await File.WriteAllBytesAsync(origem, [7, 7, 7]);
        var seletor = new SeletorArquivoFalso { Caminho = origem };
        var plano = new PlanoBEspelho(
            volume,
            new DestinoEspelhoPasta(new ProtocoloPublicacaoEspelho(volume)),
            area,
            new ShareFalso(),
            seletor);

        var resultado = await plano.ImportarAsync();

        Assert.True(resultado.Sucesso);
        Assert.Equal(new byte[] { 7, 7, 7 }, volume.Arquivos[volume.NomeArquivo]);
        Assert.Contains(origem, area.Descartados);
        Assert.DoesNotContain("foto.jpg", volume.Apagados);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
    }

    private static PlanoBEspelho Plano(VolumeMemoria volume, ShareFalso share, SeletorArquivoFalso seletor)
        => new(volume, new DestinoEspelhoPasta(new ProtocoloPublicacaoEspelho(volume)), new AreaFalsa(), share, seletor);

    private sealed class ShareFalso : ICompartilhadorArquivoLocal
    {
        public int Chamadas { get; private set; }

        public string? Titulo { get; private set; }

        public string? Caminho { get; private set; }

        public Task<ResultadoOperacaoPasta> CompartilharAsync(
            string caminhoLocal,
            string titulo,
            CancellationToken cancellationToken = default)
        {
            Chamadas++;
            Titulo = titulo;
            Caminho = caminhoLocal;
            return Task.FromResult(ResultadoOperacaoPasta.Ok());
        }
    }

    private sealed class SeletorArquivoFalso : ISeletorArquivoLocal
    {
        public CodigoFalhaPastaEspelho Codigo { get; set; } = CodigoFalhaPastaEspelho.Nenhuma;

        public string? Caminho { get; set; }

        public Task<ResultadoSelecaoArquivo> EscolherAsync(CancellationToken cancellationToken = default)
        {
            if (Codigo != CodigoFalhaPastaEspelho.Nenhuma)
                return Task.FromResult(ResultadoSelecaoArquivo.Falha(Codigo));

            return Task.FromResult(ResultadoSelecaoArquivo.Ok(Caminho!));
        }
    }
}

internal sealed class AreaFalsa : IAreaTemporariaEspelho
{
    public List<string> Descartados { get; } = [];

    public string CriarArquivo()
    {
        var caminho = Path.Combine(Path.GetTempPath(), "orcafacil-cache-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(caminho, []);
        return caminho;
    }

    public void Descartar(string caminho) => Descartados.Add(caminho);
}
