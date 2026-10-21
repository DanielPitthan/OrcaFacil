using System.Text;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.B3;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Investimentos;

public class ResultadoGravacaoB3Tests
{
    [Fact]
    public void ViradaDeAno_MesMaisRecenteEhJaneiro()
    {
        var resultado = ResultadoGravacaoB3.Criar(2,
        [
            new DateTime(2025, 12, 15),
            new DateTime(2026, 1, 2)
        ]);

        Assert.Equal(2, resultado.Lancamentos);
        Assert.Equal(
            [new DateTime(2025, 12, 1), new DateTime(2026, 1, 1)],
            resultado.Meses);
        Assert.Equal(new DateTime(2026, 1, 1), resultado.MesMaisRecente);
        Assert.Equal("/lancamentos?mes=2026-01", resultado.Rota);
        Assert.Equal("2 lançamentos em dez/2025 e jan/2026.", resultado.Texto);
    }

    [Fact]
    public void UmSoMes_TextoDaSnackbar()
    {
        var resultado = ResultadoGravacaoB3.Criar(1, [new DateTime(2026, 2, 2)]);

        Assert.Equal(1, resultado.Lancamentos);
        Assert.Equal("1 lançamento em fev/2026.", resultado.Texto);
        Assert.Equal("/lancamentos?mes=2026-02", resultado.Rota);
    }

    [Fact]
    public void SemDatas_NaoGeraLancamento()
    {
        var resultado = ResultadoGravacaoB3.Criar(3, []);

        Assert.Equal(3, resultado.Linhas);
        Assert.Equal(0, resultado.Lancamentos);
        Assert.Empty(resultado.Meses);
        Assert.Null(resultado.MesMaisRecente);
        Assert.Equal("/ativos", resultado.Rota);
        Assert.Equal("Nenhum lançamento foi gerado.", resultado.Texto);
    }

    [Fact]
    public async Task Modelo_Gravado_EscolheMarco2026_EBateComAsTransacoes()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        await using var csv = File.OpenRead(CaminhoCsv());
        var previa = await ambiente.Service.PreverAsync(csv, ambiente.MembroId);

        var resultado = await ambiente.Service.GravarAsync(previa, ambiente.MembroId);

        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        var datas = await contexto.Transacoes.Select(t => t.Data).ToListAsync();
        Assert.Equal(datas.Count, resultado.Lancamentos);
        Assert.Equal(Meses(datas), resultado.Meses);
        Assert.Equal(43, resultado.Lancamentos);
        Assert.Equal(new DateTime(2026, 3, 1), resultado.MesMaisRecente);
        Assert.Equal("/lancamentos?mes=2026-03", resultado.Rota);
        Assert.Equal(
            [
                new DateTime(2023, 1, 1),
                new DateTime(2023, 2, 1),
                new DateTime(2023, 4, 1),
                new DateTime(2023, 5, 1),
                new DateTime(2025, 4, 1),
                new DateTime(2025, 7, 1),
                new DateTime(2025, 8, 1),
                new DateTime(2025, 9, 1),
                new DateTime(2025, 10, 1),
                new DateTime(2025, 11, 1),
                new DateTime(2026, 3, 1)
            ],
            resultado.Meses);
        Assert.Equal(
            "43 lançamentos em jan/2023, fev/2023, abr/2023, mai/2023, abr/2025, jul/2025, ago/2025, set/2025, out/2025, nov/2025 e mar/2026.",
            resultado.Texto);
    }

    [Fact]
    public async Task LotePrevio_VendaPeloMesmoCusto_DeixaSoORendimentoDeFevereiro()
    {
        const string cabecalho =
            "Entrada/Saída;Data;Movimentação;Produto;Instituição;Quantidade;Preço unitário;Valor da Operação";
        const string compra =
            cabecalho + "\n" +
            "Credito;02/01/2024;Transferência - Liquidação;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;10; R$10,00 ; R$100,00 \n";
        const string arquivo =
            cabecalho + "\n" +
            "Credito;02/02/2026;Rendimento;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;10; R$1,00 ; R$10,00 \n" +
            "Debito;02/03/2026;Transferência - Liquidação;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;10; R$10,00 ; R$100,00 \n";

        await using var ambiente = await Ambiente.CriarAsync();
        await using (var fluxoCompra = new MemoryStream(Encoding.UTF8.GetBytes(compra)))
        {
            var previaCompra = await ambiente.Service.PreverAsync(fluxoCompra, ambiente.MembroId);
            var gravada = await ambiente.Service.GravarAsync(previaCompra, ambiente.MembroId);
            Assert.Equal(0, gravada.Lancamentos);
        }

        await using var fluxo = new MemoryStream(Encoding.UTF8.GetBytes(arquivo));
        var previa = await ambiente.Service.PreverAsync(fluxo, ambiente.MembroId);
        var resultado = await ambiente.Service.GravarAsync(previa, ambiente.MembroId);

        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        var datas = await contexto.Transacoes.Select(t => t.Data).ToListAsync();
        Assert.Equal(new DateTime(2026, 2, 1), resultado.MesMaisRecente);
        Assert.Equal(1, resultado.Lancamentos);
        Assert.Equal(datas.Count, resultado.Lancamentos);
        Assert.Equal(Meses(datas), resultado.Meses);
        Assert.Equal(new DateTime(2026, 2, 2), Assert.Single(datas).Date);
        Assert.Equal(1, await contexto.Proventos.CountAsync());
        Assert.Equal(1, await contexto.OperacoesAtivo.CountAsync(o => o.Tipo == TipoOperacao.Venda && o.Data == new DateTime(2026, 3, 2)));
        Assert.Equal("1 lançamento em fev/2026.", resultado.Texto);
    }

    private static List<DateTime> Meses(IEnumerable<DateTime> datas)
        => datas
            .Select(data => new DateTime(data.Year, data.Month, 1))
            .Distinct()
            .OrderBy(mes => mes)
            .ToList();

    private static string CaminhoCsv()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var noProjeto = Path.Combine(dir.FullName, "Fixtures", "B3", "extrato-b3-modelo.csv");
            if (File.Exists(noProjeto))
                return noProjeto;
            var naRaiz = Path.Combine(dir.FullName, "OrcaFacil.Tests", "Fixtures", "B3", "extrato-b3-modelo.csv");
            if (File.Exists(naRaiz))
                return naRaiz;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Fixture do extrato B3 não encontrada.");
    }

    private sealed class Ambiente : IAsyncDisposable
    {
        public TestDbContextFactory Factory { get; }
        public ImportacaoExtratoB3Service Service { get; }
        public int MembroId { get; }

        private Ambiente(TestDbContextFactory factory, ImportacaoExtratoB3Service service, int membroId)
        {
            Factory = factory;
            Service = service;
            MembroId = membroId;
        }

        public static async Task<Ambiente> CriarAsync()
        {
            var factory = new TestDbContextFactory();
            await using var contexto = await factory.CreateDbContextAsync();
            var membro = new MembroFamilia { Nome = "Usuário" };
            contexto.MembrosFamilia.Add(membro);
            await contexto.SaveChangesAsync();
            var servico = new ImportacaoExtratoB3Service(
                new ParserExtratoB3(),
                factory,
                new ResultadoRealizadoService(factory),
                new ProventoService(factory),
                new RendaFixaService(factory));
            return new Ambiente(factory, servico, membro.Id);
        }

        public ValueTask DisposeAsync()
        {
            Factory.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
