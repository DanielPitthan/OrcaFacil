using System.Globalization;
using System.Text;
using OrcaFacil.Core.B3;

namespace OrcaFacil.Tests.B3;

public class ParserExtratoB3Tests
{
    private const string Cabecalho =
        "Entrada/Saída;Data;Movimentação;Produto;Instituição;Quantidade;Preço unitário;Valor da Operação";

    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    [Fact]
    public void Amostra_CobreAs98Linhas_SemErroESemDescarte()
    {
        var resultado = LerAmostra();
        var registrosNoArquivo = File.ReadAllLines(CaminhoAmostra()).Count(linha => !string.IsNullOrWhiteSpace(linha)) - 1;

        Assert.Equal(98, registrosNoArquivo);
        Assert.Equal(98, resultado.Linhas.Count);
        Assert.Empty(resultado.Erros);
        Assert.Equal(registrosNoArquivo, resultado.Linhas.Count + resultado.Erros.Count);
        Assert.Equal(Enumerable.Range(2, 98), resultado.Linhas.Select(linha => linha.Linha));
    }

    [Fact]
    public void Amostra_TravaContagensDeRendaVariavelProventosRendaFixaEIgnorada()
    {
        var linhas = LerAmostra().Linhas;

        var comprasVariavel = linhas.Count(linha =>
            linha.Classe != ClasseAtivoB3.RendaFixa
            && linha.Tipo == TipoOperacaoB3.TransferenciaLiquidacao
            && linha.Direcao == DirecaoB3.Credito);
        var vendasVariavel = linhas.Count(linha =>
            linha.Classe != ClasseAtivoB3.RendaFixa
            && linha.Tipo == TipoOperacaoB3.TransferenciaLiquidacao
            && linha.Direcao == DirecaoB3.Debito);
        var proventos = linhas.Count(linha => linha.Tipo is TipoOperacaoB3.Rendimento or TipoOperacaoB3.Jcp);
        var rendaFixa = linhas.Where(linha => linha.Classe == ClasseAtivoB3.RendaFixa).ToList();
        var ignoradas = linhas.Count(linha => linha.Tipo == TipoOperacaoB3.Ignorada);

        Assert.Equal(40, comprasVariavel);
        Assert.Equal(25, vendasVariavel);
        Assert.Equal(6, proventos);
        Assert.Equal(4, linhas.Count(linha => linha.Tipo == TipoOperacaoB3.Rendimento));
        Assert.Equal(2, linhas.Count(linha => linha.Tipo == TipoOperacaoB3.Jcp));
        Assert.Equal(26, rendaFixa.Count);
        Assert.Equal(1, ignoradas);
        Assert.Equal(98, comprasVariavel + vendasVariavel + proventos + rendaFixa.Count + ignoradas);

        var nikos = rendaFixa.Where(linha => linha.Instituicao.Contains("NIKOS", StringComparison.Ordinal)).ToList();
        Assert.Equal(18, nikos.Count);
        Assert.Equal(9, nikos.Count(linha => linha.Direcao == DirecaoB3.Credito && linha.Tipo == TipoOperacaoB3.Compra));
        Assert.Equal(9, nikos.Count(linha => linha.Direcao == DirecaoB3.Debito && linha.Tipo == TipoOperacaoB3.Venda));

        var tesouro = rendaFixa.Where(linha => linha.Ticker.Contains("TESOURO", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, tesouro.Count(linha => linha.Tipo == TipoOperacaoB3.Compra && linha.Direcao == DirecaoB3.Credito));
        Assert.Equal(3, tesouro.Count(linha => linha.Tipo == TipoOperacaoB3.Venda && linha.Direcao == DirecaoB3.Debito));
        Assert.Equal(1, rendaFixa.Count(linha => linha.Tipo == TipoOperacaoB3.Aplicacao));
        Assert.Equal(1, rendaFixa.Count(linha => linha.Tipo == TipoOperacaoB3.ResgateAntecipado));
    }

    [Fact]
    public void Amostra_MapeiaCadaMovimentacaoDaAmostra()
    {
        var grupos = LerAmostra().Linhas
            .GroupBy(linha => linha.MovimentacaoBruta)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Select(linha => linha.Tipo).Distinct().OrderBy(tipo => tipo).ToArray());

        AssertTipos(grupos, "Transferência - Liquidação", TipoOperacaoB3.TransferenciaLiquidacao);
        AssertTipos(grupos, "COMPRA / VENDA", TipoOperacaoB3.Compra, TipoOperacaoB3.Venda);
        AssertTipos(grupos, "Compra", TipoOperacaoB3.Compra);
        AssertTipos(grupos, "Venda", TipoOperacaoB3.Venda);
        AssertTipos(grupos, "Rendimento", TipoOperacaoB3.Rendimento);
        AssertTipos(grupos, "Juros Sobre Capital Próprio", TipoOperacaoB3.Jcp);
        AssertTipos(grupos, "RESGATE ANTECIPADO", TipoOperacaoB3.ResgateAntecipado);
        AssertTipos(grupos, "Atualização", TipoOperacaoB3.Ignorada);
        AssertTipos(grupos, "APLICAÇÃO", TipoOperacaoB3.Aplicacao);
        Assert.Equal(9, grupos.Count);
    }

    [Fact]
    public void Amostra_CadaLinhaBateComOArquivo_EDecimaisSaoExatos()
    {
        var resultado = LerAmostra();
        var brutas = File.ReadAllLines(CaminhoAmostra());
        decimal soma = 0m;

        foreach (var linha in resultado.Linhas)
        {
            var campos = brutas[linha.Linha - 1].Split(';');
            Assert.Equal(8, campos.Length);

            var direcao = campos[0].Trim() == "Credito" ? DirecaoB3.Credito : DirecaoB3.Debito;
            var data = DateOnly.ParseExact(campos[1].Trim(), "dd/MM/yyyy", Invariante);
            var ticker = TickerEsperado(campos[3]);
            var quantidade = NumeroEsperado(campos[5]);
            var preco = NumeroEsperado(campos[6]);
            var valor = NumeroEsperado(campos[7]);

            Assert.Equal(direcao, linha.Direcao);
            Assert.Equal(data, linha.Data);
            Assert.Equal(campos[2].Trim(), linha.MovimentacaoBruta);
            Assert.Equal(TipoEsperado(campos[2], direcao), linha.Tipo);
            Assert.Equal(ticker, linha.Ticker);
            Assert.Equal(ticker, linha.Ticker.Trim().ToUpperInvariant());
            Assert.Equal(campos[3].Trim(), linha.ProdutoDescricao);
            Assert.Equal(ClasseEsperada(ticker, campos[3]), linha.Classe);
            Assert.Equal(campos[4].Trim(), linha.Instituicao);
            Assert.Equal(quantidade, linha.Quantidade);
            Assert.Equal(preco, linha.PrecoUnitario);
            Assert.Equal(valor, linha.ValorOperacao);
            soma += valor ?? 0m;
        }

        Assert.Equal(17566.53m, soma);
        Assert.Equal(1.4909m, resultado.Linhas.Single(linha => linha.Linha == 2).PrecoUnitario);
        Assert.Equal(5002.00m, resultado.Linhas.Single(linha => linha.Linha == 2).ValorOperacao);
        Assert.Equal(23.776m, resultado.Linhas.Single(linha => linha.Linha == 3).PrecoUnitario);
        Assert.Equal(0.018117m, resultado.Linhas.Single(linha => linha.Linha == 4).PrecoUnitario);
        Assert.Equal(0.06m, resultado.Linhas.Single(linha => linha.Linha == 79).Quantidade);
        Assert.Equal(1979.71m, resultado.Linhas.Single(linha => linha.Linha == 79).PrecoUnitario);
    }

    [Fact]
    public void Amostra_ResgateCdbc25AqgpuDoItauEntraNasLinhas()
    {
        var linha = Assert.Single(LerAmostra().Linhas, item => item.ProdutoDescricao.Contains("CDBC25AQGPU", StringComparison.Ordinal));

        Assert.Equal(4, linha.Linha);
        Assert.Equal(DirecaoB3.Debito, linha.Direcao);
        Assert.Equal(new DateOnly(2026, 3, 11), linha.Data);
        Assert.Equal(TipoOperacaoB3.ResgateAntecipado, linha.Tipo);
        Assert.Equal("CDB", linha.Ticker);
        Assert.Equal(ClasseAtivoB3.RendaFixa, linha.Classe);
        Assert.Equal("ITAU UNIBANCO S.A.", linha.Instituicao);
        Assert.Equal(264024m, linha.Quantidade);
        Assert.Equal(0.018117m, linha.PrecoUnitario);
        Assert.Equal(2718.50m, linha.ValorOperacao);
    }

    [Fact]
    public void Amostra_ValorDaOperacaoNaoEhQuantidadeVezesPreco()
    {
        var aplicacao = LerAmostra().Linhas.Single(linha => linha.Linha == 2);

        Assert.Equal(5002m, aplicacao.Quantidade);
        Assert.Equal(1.4909m, aplicacao.PrecoUnitario);
        Assert.Equal(5002.00m, aplicacao.ValorOperacao);
        Assert.NotEqual(aplicacao.Quantidade * aplicacao.PrecoUnitario, aplicacao.ValorOperacao);
    }

    [Fact]
    public void Amostra_PreservaInterrogacaoNoNomeDoAgro3_EFiagroComoFii()
    {
        var linhas = LerAmostra().Linhas;
        var agro = linhas.Where(linha => linha.Ticker == "AGRO3").ToList();

        Assert.Equal(2, agro.Count);
        Assert.All(agro, linha =>
        {
            Assert.Contains('?', linha.ProdutoDescricao);
            Assert.Equal(ClasseAtivoB3.Acao, linha.Classe);
        });

        Assert.Equal(
            new[] { "BLMG11", "FIVN11", "SNAG11" },
            linhas.Where(linha => linha.Classe == ClasseAtivoB3.FII).Select(linha => linha.Ticker).Distinct().Order().ToList());
        Assert.All(
            linhas.Where(linha => linha.Ticker is "BOVA11" or "GOLD11" or "SMAL11"),
            linha => Assert.Equal(ClasseAtivoB3.Acao, linha.Classe));
        Assert.DoesNotContain(linhas, linha => linha.Classe == ClasseAtivoB3.Outro);
    }

    [Fact]
    public void Amostra_AtualizacaoFicaIgnoradaComPrecoEValorVazios()
    {
        var linha = Assert.Single(LerAmostra().Linhas, item => item.Tipo == TipoOperacaoB3.Ignorada);

        Assert.Equal("Atualização", linha.MovimentacaoBruta);
        Assert.Equal(new DateOnly(2025, 9, 9), linha.Data);
        Assert.Equal("BOVA11", linha.Ticker);
        Assert.Equal(1m, linha.Quantidade);
        Assert.Null(linha.PrecoUnitario);
        Assert.Null(linha.ValorOperacao);
    }

    [Fact]
    public void CamposEstaveis_TickerEmMaiusculasEInstituicaoSemEspaco()
    {
        var resultado = Ler(Cabecalho + "\nCredito;02/01/2024;Rendimento;  petr4 - petrobras ;  xp investimentos  ;1; R$1,50 ; R$3,00 \n");
        var linha = Assert.Single(resultado.Linhas);

        Assert.Empty(resultado.Erros);
        Assert.Equal("PETR4", linha.Ticker);
        Assert.Equal("petr4 - petrobras", linha.ProdutoDescricao);
        Assert.Equal("xp investimentos", linha.Instituicao);
        Assert.Equal(new DateOnly(2024, 1, 2), linha.Data);
        Assert.Equal(TipoOperacaoB3.Rendimento, linha.Tipo);
        Assert.Equal(1m, linha.Quantidade);
        Assert.Equal(1.50m, linha.PrecoUnitario);
        Assert.Equal(3.00m, linha.ValorOperacao);
    }

    [Theory]
    [InlineData("Credito;01/01/2024;Compra;;XP;1;R$1,00;R$1,00", "Informe o ticker")]
    [InlineData("Credito;01/01/2024;Compra;ZZ - NAO E TICKER;XP;1;R$1,00;R$1,00", "Ticker inválido: 'ZZ'")]
    [InlineData("Credito;32/01/2024;Compra;PETR4 - PETROBRAS;XP;1;R$1,00;R$1,00", "Data inválida")]
    [InlineData("Credito;01/01/2024;Compra;PETR4 - PETROBRAS;XP;abc;R$1,00;R$1,00", "Quantidade inválida")]
    [InlineData("Credito;01/01/2024;Compra;PETR4 - PETROBRAS;XP;1;nao;R$1,00", "Preço unitário inválido")]
    [InlineData("Credito;01/01/2024;Compra;PETR4 - PETROBRAS;XP;1;R$1,00;xyz", "Valor da operação inválido")]
    [InlineData("Entrada;01/01/2024;Compra;PETR4 - PETROBRAS;XP;1;R$1,00;R$1,00", "Entrada/Saída inválida")]
    [InlineData("Credito;01/01/2024;Bonificação;PETR4 - PETROBRAS;XP;1;R$1,00;R$1,00", "Movimentação não mapeada")]
    [InlineData("Credito;01/01/2024;Compra;PETR4 - PETROBRAS;XP;1;R$1,00", "8 colunas")]
    public void LinhaInvalida_VaiParaErrosENaoParaLinhas(string registro, string trecho)
    {
        var resultado = Ler(Cabecalho + "\n" + registro + "\nCredito;02/01/2024;Compra;VALE3 - VALE;XP;2;R$10,00;R$20,00\n");

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal(2, erro.Linha);
        Assert.Contains(trecho, erro.Motivo);
        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal("VALE3", linha.Ticker);
        Assert.Equal(3, linha.Linha);
    }

    [Fact]
    public void TracoEVazio_FicamNulosSemVirarErro()
    {
        var resultado = Ler(Cabecalho + "\nCredito;09/09/2025;Atualização;BOVA11 - ISHARES IBOVESPA FUNDO DE INDICE;XP;1; - ; \n");
        var linha = Assert.Single(resultado.Linhas);

        Assert.Empty(resultado.Erros);
        Assert.Equal(TipoOperacaoB3.Ignorada, linha.Tipo);
        Assert.Null(linha.PrecoUnitario);
        Assert.Null(linha.ValorOperacao);
        Assert.Equal(1m, linha.Quantidade);
    }

    [Fact]
    public void AliasJcpERendaFixaLciLca_SaoReconhecidos()
    {
        var csv = Cabecalho + "\n"
            + "Credito;01/03/2024;JCP;ITUB4 - ITAU;XP;1;R$0,02;R$0,02\n"
            + "Credito;02/03/2024;Compra;LCI - LCI123;ITAU;10;R$1,00;R$10,00\n"
            + "Debito;03/03/2024;Venda;LCA - LCA999;ITAU;3;R$2,50;R$7,50\n"
            + "Credito;04/03/2024;COMPRA/VENDA;CDB - CDBABC;NIKOS;4;R$0,01;R$0,04\n";
        var linhas = Ler(csv).Linhas;

        Assert.Equal(4, linhas.Count);
        Assert.Equal(TipoOperacaoB3.Jcp, linhas[0].Tipo);
        Assert.Equal(ClasseAtivoB3.Acao, linhas[0].Classe);
        Assert.Equal(ClasseAtivoB3.RendaFixa, linhas[1].Classe);
        Assert.Equal("LCI", linhas[1].Ticker);
        Assert.Equal(ClasseAtivoB3.RendaFixa, linhas[2].Classe);
        Assert.Equal(TipoOperacaoB3.Venda, linhas[2].Tipo);
        Assert.Equal(TipoOperacaoB3.Compra, linhas[3].Tipo);
        Assert.Equal("CDB", linhas[3].Ticker);
    }

    [Fact]
    public void Aspas_NaoPartemOProdutoNoPontoEVirgula()
    {
        var resultado = Ler(Cabecalho + "\nCredito;01/01/2024;Compra;\"PETR4 - PETRO;BRAS\";XP;1;R$1,00;R$1,00\n");
        var linha = Assert.Single(resultado.Linhas);

        Assert.Equal("PETR4", linha.Ticker);
        Assert.Equal("PETR4 - PETRO;BRAS", linha.ProdutoDescricao);
    }

    [Fact]
    public void BomUtf8_EWindows1252_SaoLidos()
    {
        var texto = Cabecalho + "\nCredito;01/02/2024;Transferência - Liquidação;PETR4 - PETROBRAS;XP;1;R$10,50;R$10,50\n";
        var utf8 = Encoding.UTF8.GetBytes(texto);
        var comBom = new byte[utf8.Length + 3];
        comBom[0] = 0xEF;
        comBom[1] = 0xBB;
        comBom[2] = 0xBF;
        utf8.CopyTo(comBom, 3);

        var porBom = Ler(comBom);
        Assert.Empty(porBom.Erros);
        Assert.Equal(new DateOnly(2024, 2, 1), Assert.Single(porBom.Linhas).Data);
        Assert.Equal(TipoOperacaoB3.TransferenciaLiquidacao, porBom.Linhas[0].Tipo);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var por1252 = Ler(Encoding.GetEncoding(1252).GetBytes(texto));
        Assert.Empty(por1252.Erros);
        Assert.Equal("PETR4", Assert.Single(por1252.Linhas).Ticker);
        Assert.Equal(10.50m, por1252.Linhas[0].ValorOperacao);
    }

    [Fact]
    public void TabelaCustomizada_TrocaOTipoDoTexto()
    {
        var tabela = new Dictionary<string, RegraMovimentacaoB3>(StringComparer.Ordinal)
        {
            ["Rendimento"] = RegraMovimentacaoB3.Fixa(TipoOperacaoB3.Ignorada)
        };
        var resultado = Ler(
            Cabecalho + "\nCredito;01/01/2024;Rendimento;MXRF11 - MAXI RENDA FII;XP;1;R$0,10;R$0,10\n",
            new ParserExtratoB3(tabela));
        var linha = Assert.Single(resultado.Linhas);

        Assert.Equal(TipoOperacaoB3.Ignorada, linha.Tipo);
        Assert.Equal(ClasseAtivoB3.FII, linha.Classe);
        Assert.Equal("MXRF11", linha.Ticker);
    }

    [Fact]
    public void ArquivoVazio_ViraErro_ESoCabecalhoNaoInventaLinha()
    {
        var vazio = Ler("");
        var erro = Assert.Single(vazio.Erros);
        Assert.Equal(1, erro.Linha);
        Assert.Equal("O arquivo está vazio.", erro.Motivo);
        Assert.Empty(vazio.Linhas);

        var soCabecalho = Ler(Cabecalho + "\n\n");
        Assert.Empty(soCabecalho.Linhas);
        Assert.Empty(soCabecalho.Erros);
    }

    [Fact]
    public void FluxoNulo_Lanca()
    {
        var parser = new ParserExtratoB3();
        Assert.Throws<ArgumentNullException>(() => parser.Ler(null!));
    }

    private static void AssertTipos(
        IReadOnlyDictionary<string, TipoOperacaoB3[]> grupos,
        string movimentacao,
        params TipoOperacaoB3[] tipos)
    {
        Assert.Equal(tipos, grupos[movimentacao].AsEnumerable());
    }

    private static ResultadoParseB3 LerAmostra()
    {
        using var fluxo = File.OpenRead(CaminhoAmostra());
        return new ParserExtratoB3().Ler(fluxo);
    }

    private static string CaminhoAmostra()
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", "B3", "extrato-b3-modelo.csv");

    private static ResultadoParseB3 Ler(string csv, IParserExtratoB3? parser = null)
        => Ler(Encoding.UTF8.GetBytes(csv), parser);

    private static ResultadoParseB3 Ler(byte[] bytes, IParserExtratoB3? parser = null)
    {
        parser ??= new ParserExtratoB3();
        using var fluxo = new MemoryStream(bytes);
        return parser.Ler(fluxo);
    }

    private static string TickerEsperado(string produto)
    {
        var texto = produto.Trim();
        var indice = texto.IndexOf(" - ", StringComparison.Ordinal);
        var bruto = indice < 0 ? texto : texto[..indice];
        return bruto.Trim().ToUpperInvariant();
    }

    private static decimal? NumeroEsperado(string bruto)
    {
        var texto = bruto.Trim().Replace("R$", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "", StringComparison.Ordinal);
        if (texto is "" or "-" or "—" or "–")
            return null;

        texto = texto.Replace(".", "", StringComparison.Ordinal).Replace(',', '.');
        return decimal.Parse(texto, NumberStyles.Number, Invariante);
    }

    private static TipoOperacaoB3 TipoEsperado(string movimentacao, DirecaoB3 direcao)
    {
        return movimentacao.Trim() switch
        {
            "Transferência - Liquidação" => TipoOperacaoB3.TransferenciaLiquidacao,
            "COMPRA / VENDA" => direcao == DirecaoB3.Credito ? TipoOperacaoB3.Compra : TipoOperacaoB3.Venda,
            "Compra" => TipoOperacaoB3.Compra,
            "Venda" => TipoOperacaoB3.Venda,
            "Rendimento" => TipoOperacaoB3.Rendimento,
            "Juros Sobre Capital Próprio" => TipoOperacaoB3.Jcp,
            "RESGATE ANTECIPADO" => TipoOperacaoB3.ResgateAntecipado,
            "Atualização" => TipoOperacaoB3.Ignorada,
            "APLICAÇÃO" => TipoOperacaoB3.Aplicacao,
            _ => throw new InvalidOperationException($"Movimentação inesperada na amostra: {movimentacao}")
        };
    }

    private static ClasseAtivoB3 ClasseEsperada(string ticker, string produto)
    {
        var nome = produto.ToUpperInvariant();
        if (ticker is "CDB" or "LCI" or "LCA" || nome.Contains("TESOURO", StringComparison.Ordinal))
            return ClasseAtivoB3.RendaFixa;

        if (ticker.EndsWith("11", StringComparison.Ordinal)
            && (nome.Contains("FII", StringComparison.Ordinal)
                || nome.Contains("FIAGRO", StringComparison.Ordinal)
                || nome.Contains("IMOB", StringComparison.Ordinal)))
            return ClasseAtivoB3.FII;

        return ClasseAtivoB3.Acao;
    }
}
