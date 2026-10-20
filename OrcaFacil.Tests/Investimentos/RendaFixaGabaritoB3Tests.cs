using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Investimentos;

/// <summary>
/// Gabarito do extrato anonimizado. O resultado usa o Valor da Operação.
/// Nikos fecha em +8,93 em 04/2025 e o Tesouro em +19,28 em 05/2023.
/// </summary>
public class RendaFixaGabaritoB3Tests
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private static readonly HashSet<string> MovimentosRendaFixa = new(StringComparer.Ordinal)
    {
        "APLICAÇÃO",
        "RESGATE ANTECIPADO",
        "COMPRA / VENDA",
        "Compra",
        "Venda"
    };

    [Fact]
    public async Task Extrato_Nikos893_Tesouro1928_EResgateSemPosicaoERecusado()
    {
        var linhas = LerLinhas();
        Assert.Equal(26, linhas.Count);

        await using var ambiente = await CriarAsync();
        var titulos = new Dictionary<(string Codigo, string Corretora), int>();
        var orfao = linhas.Single(l => l.Codigo == "CDBC25AQGPU");
        var gravaveis = linhas.Where(l => l.Codigo != "CDBC25AQGPU").ToList();

        foreach (var linha in gravaveis)
        {
            if (!titulos.TryGetValue((linha.Codigo, linha.Corretora), out var tituloId))
            {
                tituloId = await ambiente.CriarTituloAsync(linha);
                titulos[(linha.Codigo, linha.Corretora)] = tituloId;
            }

            await ambiente.Service.SalvarAsync(ParaModelo(tituloId, linha), ambiente.MembroId);
        }

        var lancamentos = await ambiente.LancamentosAsync();
        var nikos = lancamentos.Where(t => t.Data.Year == 2025 && t.Data.Month == 4).ToList();
        Assert.Equal(9, nikos.Count);
        Assert.All(nikos, t => Assert.Equal(TipoTransacao.Receita, t.Tipo));
        Assert.Equal(8.93m, nikos.Sum(t => t.Valor));

        var tesouro = lancamentos.Where(t => t.Data.Year == 2023 && t.Data.Month == 5).ToList();
        Assert.Equal(3, tesouro.Count);
        Assert.All(tesouro, t => Assert.Equal(TipoTransacao.Receita, t.Tipo));
        Assert.Equal(19.28m, tesouro.Sum(t => t.Valor));
        Assert.Equal(8.57m, tesouro.Single(t => t.Data == new DateTime(2023, 5, 26)).Valor);
        Assert.Equal(4.50m, tesouro.Single(t => t.Data == new DateTime(2023, 5, 25)).Valor);
        Assert.Equal(6.21m, tesouro.Single(t => t.Data == new DateTime(2023, 5, 24)).Valor);

        var aberto = titulos[("CDB9268YCA7", "Itaú")];
        var posicao = await ambiente.Service.ObterPosicaoAsync(aberto);
        Assert.Equal(5002m, posicao.Quantidade);
        Assert.Equal(5002.00m, posicao.CustoTotal);
        Assert.Equal(1m, posicao.CustoMedio);
        Assert.DoesNotContain(lancamentos, t => t.Descricao.Contains("CDB9268YCA7", StringComparison.Ordinal));

        var tituloOrfao = await ambiente.CriarTituloAsync(orfao);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ambiente.Service.SalvarAsync(ParaModelo(tituloOrfao, orfao), ambiente.MembroId));
        Assert.Contains("CDBC25AQGPU", ex.Message, StringComparison.Ordinal);
        Assert.Contains("excede a posição", ex.Message, StringComparison.Ordinal);
        Assert.Empty(await ambiente.OperacoesDoTituloAsync(tituloOrfao));
    }

    [Fact]
    public async Task MesFechado_BloqueiaEdicaoQueMudariaOResultadoDoNikos()
    {
        var linhaAplicacao = LerLinhas().Single(l => l.Codigo == "CDB12547BHY" && l.Tipo == TipoOperacaoRendaFixa.Aplicacao);
        var linhaResgate = LerLinhas().Single(l => l.Codigo == "CDB12547BHY" && l.Tipo == TipoOperacaoRendaFixa.Resgate);

        await using var ambiente = await CriarAsync();
        var titulo = await ambiente.CriarTituloAsync(linhaAplicacao);
        var aplicacao = await ambiente.Service.SalvarAsync(ParaModelo(titulo, linhaAplicacao), ambiente.MembroId);
        await ambiente.Service.SalvarAsync(ParaModelo(titulo, linhaResgate), ambiente.MembroId);
        await ambiente.FecharAsync(4, 2025);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ambiente.Service.SalvarAsync(new OperacaoRendaFixaFormModel
        {
            Id = aplicacao.Id,
            TituloRendaFixaId = titulo,
            Data = linhaAplicacao.Data,
            Tipo = TipoOperacaoRendaFixa.Aplicacao,
            Quantidade = linhaAplicacao.Quantidade,
            PrecoUnitario = linhaAplicacao.PrecoUnitario,
            Valor = 3.00m
        }, ambiente.MembroId));

        Assert.Contains("Mês fechado", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Nada foi gravado", ex.Message, StringComparison.Ordinal);
        Assert.Equal(3.90m, (await ambiente.OperacaoAsync(aplicacao.Id)).Valor);
        Assert.Equal(0.14m, (await ambiente.UnicoLancamentoAsync()).Valor);
    }

    [Fact]
    public void Tesouro_TickerNormalizado_EAplicacaoResgateNaoSeguemADirecao()
    {
        Assert.Equal("TESOURO IPCA+ 2029", NormalizarTitulo("  tesouro   ipca+   2029 "));

        var linhas = LerLinhas();
        Assert.Contains(linhas, l => l.Codigo == "TESOURO IPCA+ 2045" && l.Tipo == TipoOperacaoRendaFixa.Resgate);
        Assert.Contains(linhas, l => l.Codigo == "TESOURO PREFIXADO 2026" && l.Tipo == TipoOperacaoRendaFixa.Aplicacao);
        Assert.Contains(linhas, l => l.Codigo == "TESOURO IPCA+ 2035" && l.Tipo == TipoOperacaoRendaFixa.Resgate);
        Assert.DoesNotContain(linhas, l => l.Codigo.Contains("Tesouro ", StringComparison.Ordinal));

        var aplicacoes = linhas.Where(l => l.Codigo.StartsWith("CDB", StringComparison.Ordinal) && l.Tipo == TipoOperacaoRendaFixa.Aplicacao).ToList();
        Assert.Contains(aplicacoes, l => l.Codigo == "CDB9268YCA7");

        Assert.Equal(TipoOperacaoRendaFixa.Aplicacao, TipoDaLinha("APLICAÇÃO", "Debito"));
        Assert.Equal(TipoOperacaoRendaFixa.Resgate, TipoDaLinha("RESGATE ANTECIPADO", "Credito"));
        Assert.Equal(TipoOperacaoRendaFixa.Aplicacao, TipoDaLinha("Compra", "Credito"));
        Assert.Equal(TipoOperacaoRendaFixa.Resgate, TipoDaLinha("Venda", "Debito"));
        Assert.Equal(TipoOperacaoRendaFixa.Resgate, TipoDaLinha("COMPRA / VENDA", "Debito"));
    }

    private static OperacaoRendaFixaFormModel ParaModelo(int tituloId, Linha linha) => new()
    {
        TituloRendaFixaId = tituloId,
        Data = linha.Data,
        Tipo = linha.Tipo,
        Quantidade = linha.Quantidade,
        PrecoUnitario = linha.PrecoUnitario,
        Valor = linha.Valor,
        Origem = OrigemOperacaoRendaFixa.Manual
    };

    private static List<Linha> LerLinhas()
    {
        var linhas = new List<Linha>();
        var ordem = 0;
        foreach (var campos in File.ReadLines(CaminhoCsv()))
        {
            if (campos.Length == 0)
                continue;

            var colunas = campos.Split(';');
            if (colunas.Length < 8)
                continue;
            if (colunas[0].Trim().StartsWith("Entrada", StringComparison.Ordinal))
                continue;

            var movimento = colunas[2].Trim();
            if (!MovimentosRendaFixa.Contains(movimento))
                continue;

            var produto = InterpretarProduto(colunas[3].Trim());
            linhas.Add(new Linha(
                ordem++,
                DateTime.ParseExact(colunas[1].Trim(), "dd/MM/yyyy", PtBr),
                TipoDaLinha(movimento, colunas[0].Trim()),
                produto.Codigo,
                produto.Nome,
                produto.Tipo,
                produto.Emissor,
                produto.Ano,
                Apelido(colunas[4]),
                DecimalPtBr(colunas[5]),
                Dinheiro(colunas[6]),
                Dinheiro(colunas[7])));
        }

        return linhas
            .OrderBy(l => l.Data)
            .ThenBy(l => l.Tipo == TipoOperacaoRendaFixa.Aplicacao ? 0 : 1)
            .ThenBy(l => l.Ordem)
            .ToList();
    }

    private static (string Codigo, string Nome, TipoTituloRendaFixa Tipo, string? Emissor, int? Ano) InterpretarProduto(string produto)
    {
        var partes = produto.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length >= 2 && partes[0].Equals("CDB", StringComparison.OrdinalIgnoreCase))
        {
            var codigo = partes[1].Trim().ToUpperInvariant();
            var emissor = partes.Length >= 3 ? partes[2] : null;
            var nome = emissor is null ? codigo : $"{codigo} — {emissor}";
            if (nome.Length > 80)
                nome = nome[..80];
            return (codigo, nome, TipoTituloRendaFixa.CDB, emissor, null);
        }

        var titulo = NormalizarTitulo(produto);
        var tipo = titulo.Contains("PREFIXADO", StringComparison.Ordinal) ? TipoTituloRendaFixa.TesouroPrefixado
            : titulo.Contains("IPCA", StringComparison.Ordinal) ? TipoTituloRendaFixa.TesouroIPCA
            : titulo.Contains("SELIC", StringComparison.Ordinal) ? TipoTituloRendaFixa.TesouroSelic
            : TipoTituloRendaFixa.Outro;
        int? ano = null;
        var ultimo = titulo.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (ultimo is not null && int.TryParse(ultimo, out var anoLido) && anoLido is >= 2000 and <= 2100)
            ano = anoLido;
        return (titulo, titulo, tipo, null, ano);
    }

    /// <summary>
    /// Aplicação e resgate antecipado seguem o tipo da movimentação (TipoOperacaoB3.Aplicacao
    /// e TipoOperacaoB3.ResgateAntecipado). Compra e venda continuam pela direção.
    /// </summary>
    private static TipoOperacaoRendaFixa TipoDaLinha(string movimentacao, string direcao)
    {
        var chave = string.Join(' ', movimentacao.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
        if (chave is "APLICAÇÃO" or "APLICACAO")
            return TipoOperacaoRendaFixa.Aplicacao;
        if (chave is "RESGATE ANTECIPADO")
            return TipoOperacaoRendaFixa.Resgate;

        return direcao == "Credito" ? TipoOperacaoRendaFixa.Aplicacao : TipoOperacaoRendaFixa.Resgate;
    }

    private static string NormalizarTitulo(string texto)
    {
        var aparado = texto.Trim().ToUpperInvariant();
        return string.Join(' ', aparado.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Apelido(string instituicao)
    {
        var texto = instituicao.ToUpperInvariant();
        if (texto.Contains("NIKOS", StringComparison.Ordinal))
            return "Nikos";
        if (texto.Contains("CLEAR", StringComparison.Ordinal))
            return "Clear";
        if (texto.Contains("ITAU", StringComparison.Ordinal) || texto.Contains("ITAÚ", StringComparison.Ordinal))
            return "Itaú";
        if (texto.Contains("XP", StringComparison.Ordinal))
            return "XP";

        var aparado = instituicao.Trim();
        return aparado.Length <= 40 ? aparado : aparado[..40];
    }

    private static decimal Dinheiro(string texto)
    {
        var limpo = texto.Trim().Replace("R$", "", StringComparison.Ordinal).Replace("\u00A0", "", StringComparison.Ordinal).Trim();
        if (limpo is "" or "-")
            return 0m;
        return decimal.Parse(limpo, PtBr);
    }

    private static decimal DecimalPtBr(string texto)
        => decimal.Parse(texto.Trim(), PtBr);

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

    private static async Task<Ambiente> CriarAsync()
    {
        var factory = new TestDbContextFactory();
        await using var contexto = await factory.CreateDbContextAsync();
        var membro = new MembroFamilia { Nome = "Usuário" };
        contexto.MembrosFamilia.Add(membro);
        await contexto.SaveChangesAsync();
        return new Ambiente(factory, new RendaFixaService(factory), membro.Id);
    }

    private sealed record Linha(
        int Ordem,
        DateTime Data,
        TipoOperacaoRendaFixa Tipo,
        string Codigo,
        string Nome,
        TipoTituloRendaFixa TipoTitulo,
        string? Emissor,
        int? Ano,
        string Corretora,
        decimal Quantidade,
        decimal PrecoUnitario,
        decimal Valor);

    private sealed class Ambiente : IAsyncDisposable
    {
        private readonly TestDbContextFactory _factory;
        public RendaFixaService Service { get; }
        public int MembroId { get; }

        public Ambiente(TestDbContextFactory factory, RendaFixaService service, int membroId)
        {
            _factory = factory;
            Service = service;
            MembroId = membroId;
        }

        public async Task<int> CriarTituloAsync(Linha linha)
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            var titulo = new TituloRendaFixa
            {
                Tipo = linha.TipoTitulo,
                Codigo = linha.Codigo,
                Nome = linha.Nome,
                Emissor = linha.Emissor,
                AnoVencimento = linha.Ano,
                Corretora = linha.Corretora
            };
            contexto.TitulosRendaFixa.Add(titulo);
            await contexto.SaveChangesAsync();
            return titulo.Id;
        }

        public async Task FecharAsync(int mes, int ano)
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = mes, Ano = ano, Fechado = true });
            await contexto.SaveChangesAsync();
        }

        public async Task<List<Transacao>> LancamentosAsync()
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            return await contexto.Transacoes.ToListAsync();
        }

        public async Task<Transacao> UnicoLancamentoAsync()
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            return await contexto.Transacoes.SingleAsync();
        }

        public async Task<OperacaoRendaFixa> OperacaoAsync(int id)
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            return await contexto.OperacoesRendaFixa.FirstAsync(o => o.Id == id);
        }

        public async Task<List<OperacaoRendaFixa>> OperacoesDoTituloAsync(int tituloId)
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            return await contexto.OperacoesRendaFixa.Where(o => o.TituloRendaFixaId == tituloId).ToListAsync();
        }

        public ValueTask DisposeAsync()
        {
            _factory.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
