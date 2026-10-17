using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Armazenamento;

public class CoordenadorArmazenamentoTests
{
    [Fact]
    public async Task Preparar_CobreAMatriz_SemApagar_ESoSemeiaNaInstalacaoNova()
    {
        foreach (var banco in Enum.GetValues<SituacaoBancoVivo>())
        {
            foreach (var espelho in Enum.GetValues<SituacaoEspelho>())
            {
                foreach (var schema in Enum.GetValues<ComparacaoSchemaEspelho>())
                {
                    var estado = new EstadoObservadoArmazenamento(banco, espelho, schema);
                    if (!estado.EhCoerente)
                        continue;

                    var ambiente = AmbienteFalso.Observando(estado);
                    var resultado = await ambiente.Criar().PrepararAsync();
                    var instalacaoNova = banco == SituacaoBancoVivo.Ausente && espelho == SituacaoEspelho.Ausente;

                    Assert.Equal(estado, resultado.Estado);
                    Assert.Equal(MatrizDecisaoArmazenamento.Decidir(estado).Acao, resultado.Acao);
                    Assert.Equal(resultado.Acao, resultado.AcaoPlanejada);
                    Assert.Null(resultado.Falha);
                    Assert.Equal(0, ambiente.RemocoesBanco);
                    Assert.Equal(0, ambiente.RemocoesEspelho);
                    Assert.DoesNotContain("remover-banco", ambiente.Chamadas);
                    Assert.DoesNotContain("remover-espelho", ambiente.Chamadas);

                    if (instalacaoNova)
                    {
                        Assert.Equal(AcaoArmazenamento.Semear, resultado.Acao);
                        Assert.Equal(1, ambiente.Semeaduras);
                    }
                    else
                    {
                        Assert.NotEqual(AcaoArmazenamento.Semear, resultado.Acao);
                        Assert.Equal(0, ambiente.Semeaduras);
                        Assert.DoesNotContain("semear", ambiente.Chamadas);
                    }

                    Assert.Equal(resultado.Acao == AcaoArmazenamento.RestaurarDoEspelho ? 1 : 0, ambiente.Restauracoes);
                    Assert.Equal(resultado.Acao == AcaoArmazenamento.MigrarBancoVivo ? 1 : 0, ambiente.Migracoes);
                    Assert.Equal(banco == SituacaoBancoVivo.Ausente ? 0 : 1, ambiente.Verificacoes);
                    Assert.Equal(espelho == SituacaoEspelho.Ausente ? 0 : 1, ambiente.LeiturasEspelho);
                    var bancoDeveExistir = ambiente.BancoExistia
                        || resultado.Acao is AcaoArmazenamento.RestaurarDoEspelho or AcaoArmazenamento.Semear;
                    Assert.Equal(bancoDeveExistir, ambiente.BancoExiste);
                    Assert.Equal(espelho != SituacaoEspelho.Ausente, ambiente.EspelhoExiste);
                }
            }
        }
    }

    [Fact]
    public async Task RestauracaoQueDevolveFalha_NaoSemeia_ENaoApagaBancoVivoNemEspelho()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Ausente,
            SituacaoEspelho.Legivel,
            ComparacaoSchemaEspelho.Igual));
        ambiente.RestauracaoSucesso = false;

        var resultado = await ambiente.Criar().PrepararAsync();

        AssertFalhaDeRestauracao(ambiente, resultado);
        Assert.Equal(
            ["versao", "banco.existe", "espelho.existe", "espelho.ler", "restaurar"],
            ambiente.Chamadas);
        Assert.Contains("apag", resultado.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("seme", resultado.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestauracaoQueLanca_NaoSemeia_ENaoApagaBancoVivoNemEspelho()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Inconsistente,
            SituacaoEspelho.Legivel,
            ComparacaoSchemaEspelho.EspelhoMaisAntigo));
        ambiente.RestauracaoLanca = true;

        var resultado = await ambiente.Criar().PrepararAsync();

        AssertFalhaDeRestauracao(ambiente, resultado);
        Assert.True(ambiente.BancoExiste);
        Assert.DoesNotContain("semear", ambiente.Chamadas);
        Assert.DoesNotContain("remover-banco", ambiente.Chamadas);
        Assert.DoesNotContain("remover-espelho", ambiente.Chamadas);
        Assert.DoesNotContain("migrar", ambiente.Chamadas);
    }

    [Fact]
    public async Task RestauracaoBemSucedida_NaoApagaOEspelho()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Ausente,
            SituacaoEspelho.Legivel,
            ComparacaoSchemaEspelho.EspelhoMaisAntigo));

        var resultado = await ambiente.Criar().PrepararAsync();

        Assert.Equal(AcaoArmazenamento.RestaurarDoEspelho, resultado.Acao);
        Assert.True(ambiente.EspelhoExiste);
        Assert.True(ambiente.BancoExiste);
        Assert.Equal(0, ambiente.RemocoesBanco);
        Assert.Equal(0, ambiente.RemocoesEspelho);
        Assert.Equal(0, ambiente.Semeaduras);
    }

    [Fact]
    public async Task MigracaoQueFalha_NaoSemeia_NaoApaga_ENaoRestauraPorCima()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Desatualizado,
            SituacaoEspelho.Legivel,
            ComparacaoSchemaEspelho.Igual));
        ambiente.ResultadoMigracao = new ResultadoPreparoBanco(
            new InvalidOperationException("coluna já existe"),
            new RelatorioIntegridadeBanco { MigrationsAusentesNoHistorico = ["20261016182243_AddEhEstornoItemFatura"] });

        var resultado = await ambiente.Criar().PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(AcaoArmazenamento.MigrarBancoVivo, resultado.AcaoPlanejada);
        Assert.Equal(0, ambiente.Semeaduras);
        Assert.Equal(0, ambiente.Restauracoes);
        Assert.Equal(0, ambiente.RemocoesBanco);
        Assert.Equal(0, ambiente.RemocoesEspelho);
        Assert.True(ambiente.BancoExiste);
        Assert.True(ambiente.EspelhoExiste);
        Assert.IsType<InvalidOperationException>(resultado.Falha);
        Assert.Contains("apag", resultado.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("seme", resultado.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigracaoQueLanca_NaoSemeia_ENaoApaga()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Desatualizado,
            SituacaoEspelho.Ausente,
            ComparacaoSchemaEspelho.SemEspelho));
        ambiente.MigracaoLanca = true;

        var resultado = await ambiente.Criar().PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(AcaoArmazenamento.MigrarBancoVivo, resultado.AcaoPlanejada);
        Assert.Equal(0, ambiente.Semeaduras);
        Assert.Equal(0, ambiente.RemocoesBanco);
        Assert.Equal(0, ambiente.RemocoesEspelho);
        Assert.True(ambiente.BancoExiste);
        Assert.Equal(
            ["versao", "banco.existe", "banco.verificar", "espelho.existe", "migrar"],
            ambiente.Chamadas);
    }

    [Theory]
    [InlineData(SituacaoBancoVivo.Ausente, SituacaoEspelho.Corrompido, ComparacaoSchemaEspelho.NaoAplicavel)]
    [InlineData(SituacaoBancoVivo.Inconsistente, SituacaoEspelho.Ausente, ComparacaoSchemaEspelho.SemEspelho)]
    [InlineData(SituacaoBancoVivo.Inconsistente, SituacaoEspelho.Corrompido, ComparacaoSchemaEspelho.NaoAplicavel)]
    public async Task Recuperacao_NaoSemeia_ENaoApaga(
        SituacaoBancoVivo banco,
        SituacaoEspelho espelho,
        ComparacaoSchemaEspelho schema)
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(banco, espelho, schema));
        var bancoAntes = ambiente.BancoExiste;
        var espelhoAntes = ambiente.EspelhoExiste;

        var resultado = await ambiente.Criar().PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(0, ambiente.Semeaduras);
        Assert.Equal(0, ambiente.Restauracoes);
        Assert.Equal(0, ambiente.Migracoes);
        Assert.Equal(0, ambiente.RemocoesBanco);
        Assert.Equal(0, ambiente.RemocoesEspelho);
        Assert.Equal(bancoAntes, ambiente.BancoExiste);
        Assert.Equal(espelhoAntes, ambiente.EspelhoExiste);
    }

    [Fact]
    public async Task EspelhoMaisNovo_Para_SemRestaurar_SemSemear_SemApagar()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Ausente,
            SituacaoEspelho.Legivel,
            ComparacaoSchemaEspelho.EspelhoMaisNovo));

        var resultado = await ambiente.Criar().PrepararAsync();

        Assert.Equal(AcaoArmazenamento.PedirAtualizacaoDoApp, resultado.Acao);
        Assert.Equal(0, ambiente.Semeaduras);
        Assert.Equal(0, ambiente.Restauracoes);
        Assert.Equal(0, ambiente.Migracoes);
        Assert.Equal(0, ambiente.RemocoesBanco);
        Assert.Equal(0, ambiente.RemocoesEspelho);
        Assert.True(ambiente.EspelhoExiste);
        Assert.False(ambiente.BancoExiste);
    }

    [Fact]
    public async Task BancoMaisNovoQueOApp_NaoRestauraEspelhoAntigoPorCima()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.SchemaMaisNovoQueOApp,
            SituacaoEspelho.Legivel,
            ComparacaoSchemaEspelho.EspelhoMaisAntigo));

        var resultado = await ambiente.Criar().PrepararAsync();

        Assert.Equal(AcaoArmazenamento.PedirAtualizacaoDoApp, resultado.Acao);
        Assert.Equal(0, ambiente.Restauracoes);
        Assert.Equal(0, ambiente.Migracoes);
        Assert.Equal(0, ambiente.Semeaduras);
        Assert.Equal(0, ambiente.RemocoesBanco);
        Assert.True(ambiente.BancoExiste);
        Assert.True(ambiente.EspelhoExiste);
    }

    [Fact]
    public async Task SemeaduraQueLanca_NaoApaga()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Ausente,
            SituacaoEspelho.Ausente,
            ComparacaoSchemaEspelho.SemEspelho));
        ambiente.SemeaduraLanca = true;

        var resultado = await ambiente.Criar().PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(AcaoArmazenamento.Semear, resultado.AcaoPlanejada);
        Assert.Equal(1, ambiente.Semeaduras);
        Assert.Equal(0, ambiente.RemocoesBanco);
        Assert.Equal(0, ambiente.RemocoesEspelho);
        Assert.Equal(0, ambiente.Restauracoes);
    }

    [Fact]
    public async Task VersaoNegativa_NaoSemeia_ENaoApaga()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Ausente,
            SituacaoEspelho.Ausente,
            ComparacaoSchemaEspelho.SemEspelho));
        ambiente.VersaoApp = -1;

        var resultado = await ambiente.Criar().PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Null(resultado.Estado);
        Assert.Equal(0, ambiente.Semeaduras);
        Assert.Equal(0, ambiente.RemocoesBanco);
        Assert.Equal(0, ambiente.RemocoesEspelho);
        Assert.Equal(["versao"], ambiente.Chamadas);
    }

    [Fact]
    public async Task FalhaAoVerificarBanco_ComEspelhoLegivel_Restaura_SemApagarAntes()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Inconsistente,
            SituacaoEspelho.Legivel,
            ComparacaoSchemaEspelho.Igual));
        ambiente.FalhaAoVerificar = new IOException("banco ilegível");

        var resultado = await ambiente.Criar().PrepararAsync();

        Assert.Equal(AcaoArmazenamento.RestaurarDoEspelho, resultado.Acao);
        Assert.Equal(SituacaoBancoVivo.Inconsistente, resultado.Estado!.BancoVivo);
        Assert.Equal(
            ["versao", "banco.existe", "banco.verificar", "espelho.existe", "espelho.ler", "restaurar"],
            ambiente.Chamadas);
        Assert.Equal(0, ambiente.RemocoesBanco);
        Assert.Equal(0, ambiente.RemocoesEspelho);
        Assert.Equal(0, ambiente.Semeaduras);
    }

    [Fact]
    public async Task LimparTudo_PedidoExplicito_RemoveBancoVivoEEspelho_ENaoSemeia()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Integro,
            SituacaoEspelho.Legivel,
            ComparacaoSchemaEspelho.Igual));
        var coordenador = ambiente.Criar();

        var resultado = await coordenador.LimparTudoAsync(PedidoLimparTudo.Explicito);

        Assert.True(resultado.BancoVivoRemovido);
        Assert.True(resultado.EspelhoRemovido);
        Assert.False(ambiente.BancoExiste);
        Assert.False(ambiente.EspelhoExiste);
        Assert.Equal(1, ambiente.RemocoesBanco);
        Assert.Equal(1, ambiente.RemocoesEspelho);
        Assert.Equal(0, ambiente.Semeaduras);
        Assert.Equal(["remover-banco", "remover-espelho"], ambiente.Chamadas);
    }

    [Fact]
    public async Task LimparTudo_SemPedido_NaoRemoveNada()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Integro,
            SituacaoEspelho.Legivel,
            ComparacaoSchemaEspelho.Igual));

        await Assert.ThrowsAsync<ArgumentNullException>(() => ambiente.Criar().LimparTudoAsync(null!));

        Assert.Empty(ambiente.Chamadas);
        Assert.True(ambiente.BancoExiste);
        Assert.True(ambiente.EspelhoExiste);
    }

    [Fact]
    public async Task LimparTudo_FalhaNoBanco_AindaRemoveOEspelho()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Integro,
            SituacaoEspelho.Legivel,
            ComparacaoSchemaEspelho.Igual));
        ambiente.RemocaoBancoLanca = true;

        var excecao = await Assert.ThrowsAsync<LimpezaTotalException>(() => ambiente.Criar().LimparTudoAsync(PedidoLimparTudo.Explicito));

        Assert.NotNull(excecao.FalhaBancoVivo);
        Assert.Null(excecao.FalhaEspelho);
        Assert.Equal(1, ambiente.RemocoesEspelho);
        Assert.False(ambiente.EspelhoExiste);
        Assert.True(ambiente.BancoExiste);
        Assert.Equal(0, ambiente.Semeaduras);
        Assert.Equal(["remover-banco", "remover-espelho"], ambiente.Chamadas);
    }

    [Fact]
    public async Task LimparTudo_FalhaNoEspelho_BancoJaFoiRemovido()
    {
        var ambiente = AmbienteFalso.Observando(new EstadoObservadoArmazenamento(
            SituacaoBancoVivo.Integro,
            SituacaoEspelho.Ausente,
            ComparacaoSchemaEspelho.SemEspelho));
        ambiente.RemocaoEspelhoLanca = true;

        var excecao = await Assert.ThrowsAsync<LimpezaTotalException>(() => ambiente.Criar().LimparTudoAsync(PedidoLimparTudo.Explicito));

        Assert.Null(excecao.FalhaBancoVivo);
        Assert.NotNull(excecao.FalhaEspelho);
        Assert.False(ambiente.BancoExiste);
        Assert.Equal(1, ambiente.RemocoesBanco);
        Assert.Equal(1, ambiente.RemocoesEspelho);
    }

    [Fact]
    public void Contrato_LimparTudoExigePedidoExplicito()
    {
        var metodos = typeof(IArmazenamentoDados).GetMethods();
        var limpezas = metodos.Where(metodo => metodo.Name == nameof(IArmazenamentoDados.LimparTudoAsync)).ToList();

        var limpeza = Assert.Single(limpezas);
        Assert.Contains(limpeza.GetParameters(), parametro => parametro.ParameterType == typeof(PedidoLimparTudo));
        Assert.DoesNotContain(limpeza.GetParameters(), parametro => parametro.ParameterType == typeof(bool));
        Assert.Contains(metodos, metodo => metodo.Name == nameof(IArmazenamentoDados.PrepararAsync));
    }

    [Fact]
    public void CoordenadorMatrizEClassificacao_NaoAbremArquivoNemSqlite()
    {
        var raiz = EncontrarRaiz();
        foreach (var relativo in new[]
        {
            Path.Combine("OrcaFacil.Core", "Armazenamento", "CoordenadorArmazenamento.cs"),
            Path.Combine("OrcaFacil.Core", "Armazenamento", "MatrizDecisaoArmazenamento.cs"),
            Path.Combine("OrcaFacil.Core", "Armazenamento", "ClassificacaoBancoVivo.cs")
        })
        {
            var fonte = File.ReadAllText(Path.Combine(raiz, relativo));
            Assert.DoesNotContain("sqlite_master", fonte, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MigrateAsync", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("ExecuteSql", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("File.", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("Directory.", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("SeedData", fonte, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LeituraERestauracao_RecusamCombinacaoInvalida()
    {
        Assert.Throws<ArgumentException>(() => new LeituraEspelho(true, null, null));
        Assert.Throws<ArgumentException>(() => new LeituraEspelho(true, -1, null));
        Assert.Throws<ArgumentException>(() => new LeituraEspelho(false, 1, "ruim"));
        Assert.Throws<ArgumentException>(() => LeituraEspelho.Ilegivel("  "));
        Assert.Throws<ArgumentException>(() => ResultadoRestauracao.Falha(" "));
    }

    private static void AssertFalhaDeRestauracao(AmbienteFalso ambiente, ResultadoArmazenamento resultado)
    {
        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(AcaoArmazenamento.RestaurarDoEspelho, resultado.AcaoPlanejada);
        Assert.NotEqual(AcaoArmazenamento.Semear, resultado.Acao);
        Assert.NotNull(resultado.Falha);
        Assert.Equal(1, ambiente.Restauracoes);
        Assert.Equal(0, ambiente.Semeaduras);
        Assert.Equal(0, ambiente.Migracoes);
        Assert.Equal(0, ambiente.RemocoesBanco);
        Assert.Equal(0, ambiente.RemocoesEspelho);
        Assert.Equal(ambiente.BancoExistia, ambiente.BancoExiste);
        Assert.True(ambiente.EspelhoExiste);
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

        throw new InvalidOperationException("Não foi possível localizar o coordenador a partir de " + AppContext.BaseDirectory);
    }

    private sealed class AmbienteFalso
    {
        public List<string> Chamadas { get; } = [];

        public bool BancoExiste { get; set; }

        public bool BancoExistia { get; private set; }

        public bool EspelhoExiste { get; set; }

        public RelatorioIntegridadeBanco Relatorio { get; set; } = new();

        public Exception? FalhaAoVerificar { get; set; }

        public LeituraEspelho Leitura { get; set; } = LeituraEspelho.Ilegivel("não configurado");

        public int VersaoApp { get; set; } = 10;

        public bool RestauracaoSucesso { get; set; } = true;

        public bool RestauracaoLanca { get; set; }

        public bool MigracaoLanca { get; set; }

        public ResultadoPreparoBanco ResultadoMigracao { get; set; } = new(null, new RelatorioIntegridadeBanco());

        public bool SemeaduraLanca { get; set; }

        public bool RemocaoBancoLanca { get; set; }

        public bool RemocaoEspelhoLanca { get; set; }

        public int Verificacoes { get; private set; }

        public int LeiturasEspelho { get; private set; }

        public int Restauracoes { get; private set; }

        public int Migracoes { get; private set; }

        public int Semeaduras { get; private set; }

        public int RemocoesBanco { get; private set; }

        public int RemocoesEspelho { get; private set; }

        public static AmbienteFalso Observando(EstadoObservadoArmazenamento estado)
        {
            var ambiente = new AmbienteFalso();
            switch (estado.BancoVivo)
            {
                case SituacaoBancoVivo.Ausente:
                    ambiente.BancoExiste = false;
                    break;
                case SituacaoBancoVivo.Integro:
                    ambiente.BancoExiste = true;
                    ambiente.Relatorio = new RelatorioIntegridadeBanco();
                    break;
                case SituacaoBancoVivo.Desatualizado:
                    ambiente.BancoExiste = true;
                    ambiente.Relatorio = new RelatorioIntegridadeBanco
                    {
                        MigrationsAusentesNoHistorico = ["20261016011256_AddDescritivoItemFatura"]
                    };
                    break;
                case SituacaoBancoVivo.Inconsistente:
                    ambiente.BancoExiste = true;
                    ambiente.Relatorio = new RelatorioIntegridadeBanco
                    {
                        MigrationsPelaMetade = ["20261015194450_AddCartoes"],
                        MigrationsAusentesNoHistorico = ["20261015194450_AddCartoes"]
                    };
                    break;
                case SituacaoBancoVivo.SchemaMaisNovoQueOApp:
                    ambiente.BancoExiste = true;
                    ambiente.Relatorio = new RelatorioIntegridadeBanco
                    {
                        MigrationsDesconhecidasNoHistorico = ["20990101000000_Futura"],
                        MigrationsAusentesNoHistorico = ["20261016182243_AddEhEstornoItemFatura"],
                        MigrationsPelaMetade = ["20261015194450_AddCartoes"]
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(estado));
            }

            switch (estado.Espelho)
            {
                case SituacaoEspelho.Ausente:
                    ambiente.EspelhoExiste = false;
                    break;
                case SituacaoEspelho.Corrompido:
                    ambiente.EspelhoExiste = true;
                    ambiente.Leitura = LeituraEspelho.Ilegivel("integridade do espelho falhou");
                    break;
                case SituacaoEspelho.Legivel:
                    ambiente.EspelhoExiste = true;
                    var versao = estado.SchemaEspelho switch
                    {
                        ComparacaoSchemaEspelho.Igual => ambiente.VersaoApp,
                        ComparacaoSchemaEspelho.EspelhoMaisAntigo => ambiente.VersaoApp - 1,
                        ComparacaoSchemaEspelho.EspelhoMaisNovo => ambiente.VersaoApp + 1,
                        _ => throw new ArgumentOutOfRangeException(nameof(estado))
                    };
                    ambiente.Leitura = LeituraEspelho.Ok(versao);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(estado));
            }

            ambiente.BancoExistia = ambiente.BancoExiste;
            return ambiente;
        }

        public CoordenadorArmazenamento Criar()
            => new(new Sonda(this), new Espelho(this), new Versao(this), new Restauracao(this), new Migracao(this), new Semeadura(this), new Limpeza(this));

        private sealed class Sonda(AmbienteFalso ambiente) : ISondaBancoVivo
        {
            public bool Existe()
            {
                ambiente.Chamadas.Add("banco.existe");
                return ambiente.BancoExiste;
            }

            public Task<RelatorioIntegridadeBanco> VerificarAsync(CancellationToken cancellationToken = default)
            {
                ambiente.Chamadas.Add("banco.verificar");
                ambiente.Verificacoes++;
                cancellationToken.ThrowIfCancellationRequested();
                if (ambiente.FalhaAoVerificar is not null)
                    throw ambiente.FalhaAoVerificar;
                return Task.FromResult(ambiente.Relatorio);
            }
        }

        private sealed class Espelho(AmbienteFalso ambiente) : IEspelhoDados
        {
            public bool Existe()
            {
                ambiente.Chamadas.Add("espelho.existe");
                return ambiente.EspelhoExiste;
            }

            public Task<LeituraEspelho> LerAsync(CancellationToken cancellationToken = default)
            {
                ambiente.Chamadas.Add("espelho.ler");
                ambiente.LeiturasEspelho++;
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(ambiente.Leitura);
            }
        }

        private sealed class Versao(AmbienteFalso ambiente) : IVersaoSchemaApp
        {
            public int Atual
            {
                get
                {
                    ambiente.Chamadas.Add("versao");
                    return ambiente.VersaoApp;
                }
            }
        }

        private sealed class Restauracao(AmbienteFalso ambiente) : IRestauracaoEspelho
        {
            public Task<ResultadoRestauracao> RestaurarAsync(CancellationToken cancellationToken = default)
            {
                ambiente.Chamadas.Add("restaurar");
                ambiente.Restauracoes++;
                cancellationToken.ThrowIfCancellationRequested();
                if (ambiente.RestauracaoLanca)
                    throw new IOException("cópia interrompida");
                if (!ambiente.RestauracaoSucesso)
                    return Task.FromResult(ResultadoRestauracao.Falha("cópia incompleta"));

                ambiente.BancoExiste = true;
                return Task.FromResult(ResultadoRestauracao.Ok());
            }
        }

        private sealed class Migracao(AmbienteFalso ambiente) : IMigracaoBancoVivo
        {
            public Task<ResultadoPreparoBanco> MigrarEVerificarAsync(CancellationToken cancellationToken = default)
            {
                ambiente.Chamadas.Add("migrar");
                ambiente.Migracoes++;
                cancellationToken.ThrowIfCancellationRequested();
                if (ambiente.MigracaoLanca)
                    throw new InvalidOperationException("migração interrompida");
                return Task.FromResult(ambiente.ResultadoMigracao);
            }
        }

        private sealed class Semeadura(AmbienteFalso ambiente) : ISemeaduraDados
        {
            public Task SemearAsync(CancellationToken cancellationToken = default)
            {
                ambiente.Chamadas.Add("semear");
                ambiente.Semeaduras++;
                cancellationToken.ThrowIfCancellationRequested();
                if (ambiente.SemeaduraLanca)
                    throw new InvalidOperationException("semeadura interrompida");
                ambiente.BancoExiste = true;
                return Task.CompletedTask;
            }
        }

        private sealed class Limpeza(AmbienteFalso ambiente) : ILimpezaTotalDados
        {
            public Task RemoverBancoVivoAsync(CancellationToken cancellationToken = default)
            {
                ambiente.Chamadas.Add("remover-banco");
                ambiente.RemocoesBanco++;
                cancellationToken.ThrowIfCancellationRequested();
                if (ambiente.RemocaoBancoLanca)
                    throw new IOException("banco preso");
                ambiente.BancoExiste = false;
                return Task.CompletedTask;
            }

            public Task RemoverEspelhoAsync(CancellationToken cancellationToken = default)
            {
                ambiente.Chamadas.Add("remover-espelho");
                ambiente.RemocoesEspelho++;
                cancellationToken.ThrowIfCancellationRequested();
                if (ambiente.RemocaoEspelhoLanca)
                    throw new IOException("espelho preso");
                ambiente.EspelhoExiste = false;
                return Task.CompletedTask;
            }
        }
    }
}
