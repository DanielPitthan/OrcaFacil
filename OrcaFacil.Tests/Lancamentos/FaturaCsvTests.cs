using System.Text;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Tests.Lancamentos;

public class FaturaCsvTests
{
    private static readonly Categoria[] Categorias =
    [
        Cat(1, "Alimentação"),
        Cat(2, "Lazer"),
        Cat(3, "Compras"),
        Cat(4, "Saúde")
    ];

    [Fact]
    public void Ler_ArquivoValido_LeItensEPreencheDescritivoVazio()
    {
        var texto = Arquivo(
            "16/10/2026;Supermercado;234,56;alimentação;Avulsa;;;Compras da semana",
            "02/04/2026;Netflix;39,90;Lazer;Assinatura;;;",
            "10/03/2026;Notebook;350,00;Compras;Parcelada;2;12;Loja",
            "05/01/2026;Farmácia;18,00;Saúde;;;;");

        var leitura = Ler(texto);

        Assert.True(leitura.ArquivoAceito);
        Assert.Empty(leitura.Erros);
        Assert.Equal(4, leitura.Linhas.Count);

        var mercado = leitura.Linhas[0];
        Assert.Equal(3, mercado.Numero);
        Assert.Equal(new DateTime(2026, 10, 16), mercado.Data);
        Assert.Equal("Supermercado", mercado.Descricao);
        Assert.Equal(234.56m, mercado.Valor);
        Assert.False(mercado.EhEstorno);
        Assert.Equal(1, mercado.CategoriaId);
        Assert.Equal("Alimentação", mercado.NomeCategoria);
        Assert.Equal(NaturezaItemLancamento.Avulsa, mercado.Natureza);
        Assert.Null(mercado.ParcelaAtual);
        Assert.Null(mercado.TotalParcelas);
        Assert.Equal("Compras da semana", mercado.Descritivo);
        Assert.Equal(
            FaturaCsv.CalcularHash(mercado.Data, mercado.Descricao, mercado.Valor, false, 1, NaturezaItemLancamento.Avulsa, null, null, mercado.Descritivo),
            mercado.Hash);

        var netflix = leitura.Linhas[1];
        Assert.Equal(NaturezaItemLancamento.Assinatura, netflix.Natureza);
        Assert.Equal(39.90m, netflix.Valor);
        Assert.Equal("Netflix", netflix.Descritivo);
        Assert.Null(netflix.ParcelaAtual);

        var notebook = leitura.Linhas[2];
        Assert.Equal(NaturezaItemLancamento.Parcelada, notebook.Natureza);
        Assert.Equal(2, notebook.ParcelaAtual);
        Assert.Equal(12, notebook.TotalParcelas);
        Assert.Equal("Loja", notebook.Descritivo);

        var farmacia = leitura.Linhas[3];
        Assert.Equal(NaturezaItemLancamento.Avulsa, farmacia.Natureza);
        Assert.Equal(18m, farmacia.Valor);
        Assert.Equal("Farmácia", farmacia.Descritivo);
        Assert.Equal(4, farmacia.CategoriaId);
    }

    [Fact]
    public void Ler_SemBom_RecusaOArquivo()
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(
            Arquivo("20/01/2026;Padaria;12,50;Alimentação;;;;"));

        var leitura = FaturaCsv.Ler(bytes, Categorias);

        Assert.False(leitura.ArquivoAceito);
        Assert.Contains("BOM", leitura.ErroArquivo);
        Assert.Empty(leitura.Linhas);
        Assert.Empty(leitura.Erros);
    }

    [Fact]
    public void Ler_Utf8Invalido_NaoCaiNoWindows1252()
    {
        var prefixo = FaturaCsvLayout.Codificar(Prologo() + "16/10/2026;A");
        var sufixo = Encoding.ASCII.GetBytes(";10,00;Mercado;Avulsa;;;\n");
        var bytes = prefixo.Concat(new byte[] { 0xE7 }).Concat(sufixo).ToArray();

        var leitura = FaturaCsv.Ler(bytes, Categorias);

        Assert.False(leitura.ArquivoAceito);
        Assert.Contains("UTF-8", leitura.ErroArquivo);
        Assert.DoesNotContain("BOM", leitura.ErroArquivo);
        Assert.Empty(leitura.Linhas);
        Assert.Empty(leitura.Erros);
    }

    [Fact]
    public void Ler_VersaoMaior_NaoImportaNada()
    {
        var texto = "OrcaFacil;Fatura;2\n" + FaturaCsvLayout.Cabecalho + "\n"
            + "20/01/2026;Padaria;12,50;Alimentação;;;;\n";

        var leitura = Ler(texto);

        Assert.False(leitura.ArquivoAceito);
        Assert.Contains("versão 2", leitura.ErroArquivo);
        Assert.Contains("Nada foi importado", leitura.ErroArquivo);
        Assert.Empty(leitura.Linhas);
        Assert.Empty(leitura.Erros);
    }

    [Fact]
    public void Ler_VersaoAusente_NaoImportaNada()
    {
        var texto = FaturaCsvLayout.Cabecalho + "\n20/01/2026;Padaria;12,50;Alimentação;;;;\n";

        var leitura = Ler(texto);

        Assert.False(leitura.ArquivoAceito);
        Assert.Contains("OrcaFacil;Fatura;1", leitura.ErroArquivo);
        Assert.Empty(leitura.Linhas);
        Assert.Empty(leitura.Erros);
    }

    [Fact]
    public void Ler_CabecalhoDiferente_NaoImportaNada()
    {
        var texto = FaturaCsvLayout.LinhaVersao + "\n"
            + "data;descricao;valor;categoria;natureza;parcelaatual;totalparcelas;descritivo\n"
            + "20/01/2026;Padaria;12,50;Alimentação;;;;\n";

        var leitura = Ler(texto);

        Assert.False(leitura.ArquivoAceito);
        Assert.Contains("cabeçalho", leitura.ErroArquivo);
        Assert.Contains(FaturaCsvLayout.Cabecalho, leitura.ErroArquivo);
        Assert.Empty(leitura.Linhas);
        Assert.Empty(leitura.Erros);
    }

    [Fact]
    public void Ler_DataInvalida_ErroNaColunaEMantemALinhaBoa()
    {
        var leitura = Ler(Arquivo(
            "20/01/2026;Padaria;10,00;Alimentação;;;;",
            "2026-10-16;Iso;10,00;Alimentação;;;;",
            "31/02/2026;Fevereiro;10,00;Alimentação;;;;",
            "5/10/2026;Curta;10,00;Alimentação;;;;",
            ";Vazia;10,00;Alimentação;;;;"));

        Assert.True(leitura.ArquivoAceito);
        var boa = Assert.Single(leitura.Linhas);
        Assert.Equal("Padaria", boa.Descricao);
        Assert.Equal(3, boa.Numero);
        Assert.Equal(4, leitura.Erros.Count);
        Assert.All(leitura.Erros, erro => Assert.Equal(FaturaCsvLayout.ColunaData, erro.Coluna));
        Assert.Contains(leitura.Erros, erro => erro.Numero == 4 && erro.Mensagem.Contains("dd/MM/yyyy", StringComparison.Ordinal));
        Assert.Contains(leitura.Erros, erro => erro.Numero == 7 && erro.Mensagem.Contains("Informe a data", StringComparison.Ordinal));
    }

    [Fact]
    public void Ler_ValorInvalido_RejeitaSimboloPontoEMilhar()
    {
        var leitura = Ler(Arquivo(
            "20/01/2026;Padaria;10,5;Alimentação;;;;",
            "16/10/2026;Moeda;R$ 10,00;Alimentação;;;;",
            "16/10/2026;Milhar;1.234,56;Alimentação;;;;",
            "16/10/2026;Ponto;1234.56;Alimentação;;;;",
            "16/10/2026;Zero;0,00;Alimentação;;;;",
            "16/10/2026;Vazio;;Alimentação;;;;",
            "32/13/2026;Ambos;1.234,56;Alimentação;;;;"));

        Assert.True(leitura.ArquivoAceito);
        var boa = Assert.Single(leitura.Linhas);
        Assert.Equal(10.50m, boa.Valor);
        Assert.Equal("Padaria", boa.Descricao);

        Assert.Contains(leitura.Erros, erro => erro.Numero == 4 && erro.Coluna == FaturaCsvLayout.ColunaValor && erro.Mensagem.Contains("1234,56", StringComparison.Ordinal));
        Assert.Contains(leitura.Erros, erro => erro.Numero == 5 && erro.Coluna == FaturaCsvLayout.ColunaValor);
        Assert.Contains(leitura.Erros, erro => erro.Numero == 6 && erro.Coluna == FaturaCsvLayout.ColunaValor);
        Assert.Contains(leitura.Erros, erro => erro.Numero == 7 && erro.Coluna == FaturaCsvLayout.ColunaValor && erro.Mensagem.Contains("maior que zero", StringComparison.Ordinal));
        Assert.Contains(leitura.Erros, erro => erro.Numero == 8 && erro.Coluna == FaturaCsvLayout.ColunaValor && erro.Mensagem.Contains("Informe o valor", StringComparison.Ordinal));
        Assert.Contains(leitura.Erros, erro => erro.Numero == 9 && erro.Coluna == FaturaCsvLayout.ColunaData);
        Assert.Contains(leitura.Erros, erro => erro.Numero == 9 && erro.Coluna == FaturaCsvLayout.ColunaValor);
        Assert.DoesNotContain(leitura.Linhas, linha => linha.Descricao == "Ambos");
    }

    [Fact]
    public void Ler_ValorNegativo_ViraEstornoAvulso()
    {
        var leitura = Ler(Arquivo(
            "16/10/2026;Devolução mercado;-45,90;Alimentação;;;;",
            "17/10/2026;Ajuste;-10,00;Lazer;Avulsa;;;",
            "18/10/2026;Parcela estornada;-8,50;Compras;Parcelada;1;2;",
            "19/10/2026;Assinatura estornada;-3,00;Lazer;Assinatura;;;"));

        Assert.True(leitura.ArquivoAceito);
        Assert.Equal(2, leitura.Linhas.Count);

        var mercado = leitura.Linhas[0];
        Assert.Equal(45.90m, mercado.Valor);
        Assert.True(mercado.EhEstorno);
        Assert.Equal(NaturezaItemLancamento.Avulsa, mercado.Natureza);
        Assert.Null(mercado.ParcelaAtual);
        Assert.Null(mercado.TotalParcelas);
        Assert.Equal("Devolução mercado", mercado.Descritivo);
        Assert.NotEqual(
            FaturaCsv.CalcularHash(mercado.Data, mercado.Descricao, mercado.Valor, false, mercado.CategoriaId, mercado.Natureza, null, null, mercado.Descritivo),
            mercado.Hash);

        var ajuste = leitura.Linhas[1];
        Assert.Equal(10m, ajuste.Valor);
        Assert.True(ajuste.EhEstorno);
        Assert.Equal(NaturezaItemLancamento.Avulsa, ajuste.Natureza);
        Assert.Equal(2, ajuste.CategoriaId);

        Assert.Equal(2, leitura.Erros.Count);
        Assert.All(leitura.Erros, erro =>
        {
            Assert.Equal(FaturaCsvLayout.ColunaNatureza, erro.Coluna);
            Assert.Contains("Estorno só pode ser avulso", erro.Mensagem);
        });
        Assert.DoesNotContain(leitura.Linhas, linha => linha.Descricao.Contains("estornada", StringComparison.Ordinal));
    }

    [Fact]
    public void Ler_ParceladaSemTotais_ErroNaLinha()
    {
        var leitura = Ler(Arquivo(
            "10/03/2026;Notebook;350,00;Compras;Parcelada;2;12;Loja",
            "11/03/2026;Mesa;200,00;Compras;Parcelada;;;",
            "12/03/2026;Cadeira;80,00;Compras;Parcelada;2;;",
            "13/03/2026;Sofá;90,00;Compras;Parcelada;3;2;",
            "14/03/2026;Pão;5,00;Alimentação;Avulsa;1;2;",
            "15/03/2026;Revista;12,00;Lazer;Assinatura;;4;"));

        Assert.True(leitura.ArquivoAceito);
        var boa = Assert.Single(leitura.Linhas);
        Assert.Equal("Notebook", boa.Descricao);
        Assert.Equal(2, boa.ParcelaAtual);
        Assert.Equal(12, boa.TotalParcelas);

        Assert.Contains(leitura.Erros, erro => erro.Numero == 4 && erro.Coluna == FaturaCsvLayout.ColunaParcelaAtual && erro.Mensagem.Contains("total de parcelas", StringComparison.Ordinal));
        Assert.Contains(leitura.Erros, erro => erro.Numero == 5 && erro.Coluna == FaturaCsvLayout.ColunaTotalParcelas && erro.Mensagem.Contains("juntas", StringComparison.Ordinal));
        Assert.Contains(leitura.Erros, erro => erro.Numero == 6 && erro.Mensagem.Contains("não pode ser maior", StringComparison.Ordinal));
        Assert.Contains(leitura.Erros, erro => erro.Numero == 7 && erro.Mensagem.Contains("só valem para a natureza Parcelada", StringComparison.Ordinal));
        Assert.Contains(leitura.Erros, erro => erro.Numero == 8 && erro.Coluna == FaturaCsvLayout.ColunaTotalParcelas);
    }

    [Fact]
    public void Ler_DescritivoAcimaDe120_ErroNaColuna()
    {
        var noLimite = new string('a', 120);
        var acima = new string('b', 121);

        var leitura = Ler(Arquivo(
            $"16/10/2026;Ok;10,00;Alimentação;Avulsa;;;{noLimite}",
            $"16/10/2026;Longo;10,00;Alimentação;Avulsa;;;{acima}",
            $"16/10/2026;{acima};10,00;Alimentação;Avulsa;;;",
            $"16/10/2026;{noLimite};10,00;Alimentação;;;;"));

        Assert.True(leitura.ArquivoAceito);
        Assert.Equal(2, leitura.Linhas.Count);
        Assert.Equal(noLimite, leitura.Linhas[0].Descritivo);
        Assert.Equal(noLimite, leitura.Linhas[1].Descritivo);
        Assert.Equal("Ok", leitura.Linhas[0].Descricao);

        var descritivo = Assert.Single(leitura.Erros, erro => erro.Coluna == FaturaCsvLayout.ColunaDescritivo);
        Assert.Equal(4, descritivo.Numero);
        Assert.Contains("120", descritivo.Mensagem);
        var descricao = Assert.Single(leitura.Erros, erro => erro.Coluna == FaturaCsvLayout.ColunaDescricao);
        Assert.Equal(5, descricao.Numero);
    }

    [Fact]
    public void Ler_CategoriaENaturezaInvalidas_ErroNaColuna()
    {
        var leitura = Ler(Arquivo(
            "16/10/2026;Viagem;10,00;Viagem;Avulsa;;;",
            "16/10/2026;Mensal;10,00;Alimentação;Mensal;;;",
            "16/10/2026;Sem categoria;10,00;;Avulsa;;;"));

        Assert.True(leitura.ArquivoAceito);
        Assert.Empty(leitura.Linhas);
        Assert.Contains(leitura.Erros, erro => erro.Coluna == FaturaCsvLayout.ColunaCategoria && erro.Mensagem.Contains("Viagem", StringComparison.Ordinal) && erro.Mensagem.Contains("não existe", StringComparison.Ordinal));
        Assert.Contains(leitura.Erros, erro => erro.Coluna == FaturaCsvLayout.ColunaNatureza && erro.Mensagem.Contains("Avulsa", StringComparison.Ordinal));
        Assert.Contains(leitura.Erros, erro => erro.Coluna == FaturaCsvLayout.ColunaCategoria && erro.Mensagem.Contains("Informe a categoria", StringComparison.Ordinal));
    }

    [Fact]
    public void Ler_LinhasDeExemplo_SaoIgnoradas()
    {
        var leitura = Ler(
            Arquivo(
                FaturaCsvLayout.Exemplo1,
                FaturaCsvLayout.Exemplo2,
                "20/01/2026;Padaria;12,50;Alimentação;;;;"),
            Cat(1, "Alimentação"));

        Assert.True(leitura.ArquivoAceito);
        Assert.Empty(leitura.Erros);
        var linha = Assert.Single(leitura.Linhas);
        Assert.Equal(5, linha.Numero);
        Assert.Equal("Padaria", linha.Descricao);
        Assert.Equal(12.50m, linha.Valor);
        Assert.Equal("Padaria", linha.Descritivo);
        Assert.Equal(NaturezaItemLancamento.Avulsa, linha.Natureza);
    }

    [Fact]
    public void Ler_ExemploAlterado_NaoEIgnorado()
    {
        var leitura = Ler(Arquivo(FaturaCsvLayout.Exemplo1 + " extra"), Cat(1, "Alimentação"));

        var linha = Assert.Single(leitura.Linhas);
        Assert.Equal("Supermercado", linha.Descricao);
        Assert.Equal("Compras da semana extra", linha.Descritivo);
        Assert.Empty(leitura.Erros);
    }

    [Fact]
    public void GerarModelo_PublicaLayoutEReimportaSemErro()
    {
        Assert.Equal("orcafacil-modelo-fatura-v1.csv", FaturaCsvLayout.NomeArquivoModelo);
        Assert.Equal(FaturaCsvLayout.LinhaVersao, $"OrcaFacil;Fatura;{FaturaCsvLayout.Versao}");
        Assert.Equal(
            ["Data", "Descricao", "Valor", "Categoria", "Natureza", "ParcelaAtual", "TotalParcelas", "Descritivo"],
            FaturaCsvLayout.Colunas);

        var texto = FaturaCsvLayout.GerarModelo();
        var linhas = texto.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        Assert.Equal(FaturaCsvLayout.LinhaVersao, linhas[0]);
        Assert.Equal(FaturaCsvLayout.Cabecalho, linhas[1]);
        Assert.Equal(FaturaCsvLayout.Exemplo1, linhas[2]);
        Assert.Equal(FaturaCsvLayout.Exemplo2, linhas[3]);
        Assert.Equal(4, linhas.Length);
        Assert.EndsWith("\r\n", texto);

        var bytes = FaturaCsvLayout.Codificar(texto);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);

        var leitura = FaturaCsv.Ler(bytes, []);
        Assert.True(leitura.ArquivoAceito);
        Assert.Empty(leitura.Erros);
        Assert.Empty(leitura.Linhas);
    }

    [Fact]
    public void Escapar_SegueARegraDoCsvDeExportacao()
    {
        Assert.Equal("simples", FaturaCsvLayout.Escapar("simples"));
        Assert.Equal("\"Padaria; centro\"", FaturaCsvLayout.Escapar("Padaria; centro"));
        Assert.Equal("\"Nota \"\"x\"\"\"", FaturaCsvLayout.Escapar("Nota \"x\""));
        Assert.Equal("\"linha\nquebra\"", FaturaCsvLayout.Escapar("linha\nquebra"));
    }

    [Fact]
    public void Ler_Aspas_PreservamPontoEVirgulaEAspasDuplas()
    {
        var leitura = Ler(Arquivo(
            "16/10/2026;\"Padaria; centro\";10,00;Alimentação;Avulsa;;;\"Nota \"\"x\"\"\"",
            "16/10/2026;\"Sem fechar;10,00;Alimentação;Avulsa;;;Compras",
            "16/10/2026;Curta;10,00;Alimentação"));

        Assert.True(leitura.ArquivoAceito);
        var linha = Assert.Single(leitura.Linhas);
        Assert.Equal("Padaria; centro", linha.Descricao);
        Assert.Equal("Nota \"x\"", linha.Descritivo);
        Assert.Equal(10m, linha.Valor);

        var erro = Assert.Single(leitura.Erros);
        Assert.Equal(4, erro.Numero);
        Assert.Equal("Linha", erro.Coluna);
        Assert.Contains("aspas", erro.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void CalcularHash_MesmaLinhaRepete_EstornoMuda()
    {
        var data = new DateTime(2026, 10, 16, 15, 30, 0);
        var compra = FaturaCsv.CalcularHash(data, "Padaria", 10m, false, 1, NaturezaItemLancamento.Avulsa, null, null, "Padaria");
        var deNovo = FaturaCsv.CalcularHash(data.Date, "Padaria", 10.00m, false, 1, NaturezaItemLancamento.Avulsa, null, null, "Padaria");
        var estorno = FaturaCsv.CalcularHash(data, "Padaria", 10m, true, 1, NaturezaItemLancamento.Avulsa, null, null, "Padaria");
        var outraParcela = FaturaCsv.CalcularHash(data, "Padaria", 10m, false, 1, NaturezaItemLancamento.Parcelada, 2, 12, "Padaria");

        Assert.Equal(compra, deNovo);
        Assert.NotEqual(compra, estorno);
        Assert.NotEqual(compra, outraParcela);
        Assert.Matches("^[0-9A-F]{64}$", compra);
    }

    [Fact]
    public void Ler_QuebraDeLinhaEntreAspas_MantemOCampoEALinhaSeguinte()
    {
        var leitura = Ler(Arquivo(
            "16/10/2026;\"Uber\nviagem\";20,00;Lazer;;;;",
            "18/10/2026;Padaria;10,00;Alimentação;;;;"));

        Assert.True(leitura.ArquivoAceito);
        Assert.Empty(leitura.Erros);
        Assert.Equal(2, leitura.Linhas.Count);
        Assert.Equal(3, leitura.Linhas[0].Numero);
        Assert.Equal("Uber\nviagem", leitura.Linhas[0].Descricao);
        Assert.Equal(20m, leitura.Linhas[0].Valor);
        Assert.Equal(5, leitura.Linhas[1].Numero);
        Assert.Equal("Padaria", leitura.Linhas[1].Descricao);
    }

    [Fact]
    public void EscaparELer_PrefixoDeFormula_NaoMexeNoValorNegativo()
    {
        Assert.Equal("'=cmd", FaturaCsvLayout.Escapar("=cmd"));
        Assert.Equal("'+soma", FaturaCsvLayout.Escapar("+soma"));
        Assert.Equal("'@a", FaturaCsvLayout.Escapar("@a"));
        Assert.Equal("'-tarifa", FaturaCsvLayout.Escapar("-tarifa"));
        Assert.Equal("-18,00", FaturaCsvLayout.Escapar("-18,00"));
        Assert.Equal("-18", FaturaCsvLayout.Escapar("-18"));

        var linha = string.Join(';',
            "16/10/2026",
            FaturaCsvLayout.Escapar("=cmd"),
            "-18,00",
            "Alimentação",
            "Avulsa",
            "",
            "",
            FaturaCsvLayout.Escapar("-tarifa"));

        var leitura = Ler(Arquivo(linha));

        Assert.True(leitura.ArquivoAceito);
        Assert.Empty(leitura.Erros);
        var estorno = Assert.Single(leitura.Linhas);
        Assert.Equal("=cmd", estorno.Descricao);
        Assert.Equal("-tarifa", estorno.Descritivo);
        Assert.True(estorno.EhEstorno);
        Assert.Equal(18m, estorno.Valor);
        Assert.Equal("Alimentação", estorno.NomeCategoria);
    }

    [Fact]
    public void Ler_ArquivoMaiorQueUmMega_RecusaSemLer()
    {
        var grande = new byte[FaturaCsvLayout.TamanhoMaximoBytes + 1];
        grande[0] = 0xEF;
        grande[1] = 0xBB;
        grande[2] = 0xBF;

        var leitura = FaturaCsv.Ler(grande, Categorias);

        Assert.False(leitura.ArquivoAceito);
        Assert.Contains("1 MB", leitura.ErroArquivo);
        Assert.Contains("Nada foi importado", leitura.ErroArquivo);
        Assert.Empty(leitura.Linhas);
        Assert.Empty(leitura.Erros);
    }

    private static FaturaCsv.Leitura Ler(string texto, params Categoria[] categorias)
        => FaturaCsv.Ler(FaturaCsvLayout.Codificar(texto), categorias.Length == 0 ? Categorias : categorias);

    private static string Prologo()
        => FaturaCsvLayout.LinhaVersao + "\n" + FaturaCsvLayout.Cabecalho + "\n";

    private static string Arquivo(params string[] dados)
    {
        var sb = new StringBuilder();
        sb.Append(Prologo());
        foreach (var linha in dados)
            sb.Append(linha).Append('\n');
        return sb.ToString();
    }

    private static Categoria Cat(int id, string nome) => new() { Id = id, Nome = nome };
}
