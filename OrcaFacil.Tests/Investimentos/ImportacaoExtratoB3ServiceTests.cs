using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.B3;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Investimentos;

public class ImportacaoExtratoB3ServiceTests
{
    [Fact]
    public void Hash_UsaOsCamposEstaveis_ComDecimalInvariant()
    {
        var linha = new LinhaExtratoB3(
            2,
            DirecaoB3.Credito,
            new DateOnly(2026, 9, 16),
            "APLICAÇÃO",
            TipoOperacaoB3.Aplicacao,
            "CDB9268YCA7",
            "CDB - CDB9268YCA7",
            ClasseAtivoB3.RendaFixa,
            "ITAU UNIBANCO S.A.",
            5002m,
            1.4909m,
            5002.00m);

        var texto = HashLinhaExtratoB3.Texto(linha, 0);
        // 5002,00 / 5002 = 1. O preço lido 1,4909 não entra na chave.
        Assert.Equal("2026-09-16|Aplicacao|CDB9268YCA7|5002|1|5002|ITAU UNIBANCO S.A.|0", texto);
        var esperado = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
        Assert.Equal(esperado, HashLinhaExtratoB3.Calcular(linha, 0));
        Assert.Equal(64, esperado.Length);

        var outraPosicao = linha with { Linha = 40 };
        Assert.Equal(HashLinhaExtratoB3.Calcular(linha, 0), HashLinhaExtratoB3.Calcular(outraPosicao, 0));
        Assert.NotEqual(HashLinhaExtratoB3.Calcular(linha, 0), HashLinhaExtratoB3.Calcular(linha, 1));
        Assert.Equal([0, 1], HashLinhaExtratoB3.Ocorrencias([linha, outraPosicao]));
    }

    [Fact]
    public void PrecoEfetivo_QuatroCasas_AfastaDoZero()
    {
        var terco = LinhaDePreco(3m, 100m);
        Assert.Contains("|33.3333|100|", HashLinhaExtratoB3.Chave(terco), StringComparison.Ordinal);

        var mil = LinhaDePreco(1m, 1.00005m);
        Assert.Contains("|1.0001|1.00005|", HashLinhaExtratoB3.Chave(mil), StringComparison.Ordinal);

        static LinhaExtratoB3 LinhaDePreco(decimal quantidade, decimal valor) => new(
            1,
            DirecaoB3.Credito,
            new DateOnly(2024, 2, 1),
            "Transferência - Liquidação",
            TipoOperacaoB3.Compra,
            "PETR4",
            "PETR4 - PETROLEO BRASILEIRO",
            ClasseAtivoB3.Acao,
            "XP INVESTIMENTOS CCTVM S/A",
            quantidade,
            null,
            valor);
    }

    [Fact]
    public async Task OverflowNoPrecoEfetivo_MarcaSoALinha_EAsOutrasSeguem()
    {
        const string Cabecalho =
            "Entrada/Saída;Data;Movimentação;Produto;Instituição;Quantidade;Preço unitário;Valor da Operação\n";
        const string Boa =
            "Credito;01/02/2024;Transferência - Liquidação;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;10;10,00;100,00\n";
        const string Estouro =
            "Credito;01/02/2024;Transferência - Liquidação;VALE3 - VALE;XP INVESTIMENTOS CCTVM S/A;0,1;1,00;79228162514264337593543950335\n";
        const string Outra =
            "Credito;02/02/2024;Transferência - Liquidação;ITUB4 - ITAU UNIBANCO;XP INVESTIMENTOS CCTVM S/A;5;20,00;100,00\n";
        var csv = Cabecalho + Boa + Estouro + Estouro + Boa + Outra;

        using var leitura = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var lido = new ParserExtratoB3().Ler(leitura);
        Assert.Equal(5, lido.Linhas.Count);
        Assert.Empty(lido.Erros);
        Assert.Throws<OverflowException>(() => HashLinhaExtratoB3.Chave(lido.Linhas[1]));
        Assert.Throws<OverflowException>(() => HashLinhaExtratoB3.Chave(lido.Linhas[2]));

        await using var ambiente = await Ambiente.CriarAsync();
        await using var fluxo = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var previa = await ambiente.Service.PreverAsync(fluxo, ambiente.MembroId);

        Assert.Equal(5, previa.Linhas.Count);
        Assert.Equal(2, previa.Contar(StatusLinhaImportacaoB3.Erro));
        Assert.Equal(3, previa.Contar(StatusLinhaImportacaoB3.Nova));

        var erros = previa.Linhas.Where(linha => linha.Status == StatusLinhaImportacaoB3.Erro).ToList();
        Assert.Equal([3, 4], erros.Select(linha => linha.Numero));
        Assert.All(erros, linha =>
        {
            Assert.Equal("VALE3", linha.Titulo);
            Assert.Equal(ImportacaoExtratoB3Service.MensagemPrecoEfetivo, linha.Motivo);
            Assert.Equal(ImportacaoExtratoB3Service.MensagemPrecoEfetivo, linha.Subtitulo);
            Assert.Equal("Erro", linha.RotuloStatus);
            Assert.False(linha.Marcada);
            Assert.False(linha.PodeMarcar);
            Assert.Null(linha.Comando);
        });

        var primeira = previa.Linhas.Single(linha => linha.Numero == 2);
        var segunda = previa.Linhas.Single(linha => linha.Numero == 5);
        var itub = previa.Linhas.Single(linha => linha.Numero == 6);
        Assert.All(new[] { primeira, segunda, itub }, linha =>
        {
            Assert.Equal(StatusLinhaImportacaoB3.Nova, linha.Status);
            Assert.True(linha.Marcada);
            Assert.True(linha.PodeMarcar);
        });
        Assert.Equal(HashLinhaExtratoB3.Calcular(lido.Linhas[0], 0), primeira.Comando!.Hash);
        Assert.Equal(HashLinhaExtratoB3.Calcular(lido.Linhas[3], 1), segunda.Comando!.Hash);
        Assert.Equal(HashLinhaExtratoB3.Calcular(lido.Linhas[4], 0), itub.Comando!.Hash);
        Assert.NotEqual(primeira.Comando.Hash, segunda.Comando.Hash);

        Assert.Equal(3, (await ambiente.Service.GravarAsync(previa, ambiente.MembroId)).Linhas);
        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        var tickers = await contexto.OperacoesAtivo.Include(o => o.Ativo).Select(o => o.Ativo!.Ticker).ToListAsync();
        Assert.Equal(3, tickers.Count);
        Assert.DoesNotContain("VALE3", tickers);
    }

    [Fact]
    public void PrecosEfetivosDiferentes_FicamEmSeriesDistintas_AmbasOcorrenciaZero()
    {
        var primeiro = new LinhaExtratoB3(
            2,
            DirecaoB3.Credito,
            new DateOnly(2024, 2, 1),
            "Transferência - Liquidação",
            TipoOperacaoB3.Compra,
            "PETR4",
            "PETR4 - PETROLEO BRASILEIRO",
            ClasseAtivoB3.Acao,
            "XP INVESTIMENTOS CCTVM S/A",
            10m,
            10m,
            100m);
        var segundo = primeiro with { Linha = 8, Quantidade = 8m };

        Assert.Equal(primeiro.ValorOperacao, segundo.ValorOperacao);
        Assert.NotEqual(primeiro.Quantidade, segundo.Quantidade);
        Assert.Equal(primeiro.PrecoUnitario, segundo.PrecoUnitario);
        Assert.Equal([0, 0], HashLinhaExtratoB3.Ocorrencias([primeiro, segundo]));
        Assert.NotEqual(HashLinhaExtratoB3.Chave(primeiro), HashLinhaExtratoB3.Chave(segundo));
    }

    [Fact]
    public async Task EscalaDoDecimal_NaoAbreOutraSerie()
    {
        const string cab =
            "Entrada/Saída;Data;Movimentação;Produto;Instituição;Quantidade;Preço unitário;Valor da Operação";
        static string Arq(string q, string preco, string valor) =>
            cab + $"\nCredito;01/02/2024;Transferência - Liquidação;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;{q};{preco};{valor}\n";

        var pares = new (string A, string B)[]
        {
            (Arq("10", " R$10,5 ", " R$100,00 "), Arq("10", " R$10,50 ", " R$100,00 ")),
            (Arq("10", " R$10,00 ", " R$100,00 "), Arq("10,00", " R$10,00 ", " R$100,00 ")),
            (Arq("10", " R$10,00 ", "100"), Arq("10", " R$10,00 ", " R$100,00 ")),
        };

        foreach (var (a, b) in pares)
        {
            LinhaExtratoB3 Ler(string csv)
            {
                using var fluxo = new MemoryStream(Encoding.UTF8.GetBytes(csv));
                return new ParserExtratoB3().Ler(fluxo).Linhas[0];
            }

            var linhaA = Ler(a);
            var linhaB = Ler(b);
            Assert.Equal(HashLinhaExtratoB3.Chave(linhaA), HashLinhaExtratoB3.Chave(linhaB));
            Assert.Equal(HashLinhaExtratoB3.Calcular(linhaA, 0), HashLinhaExtratoB3.Calcular(linhaB, 0));

            await using var ambiente = await Ambiente.CriarAsync();
            await using (var s1 = new MemoryStream(Encoding.UTF8.GetBytes(a)))
            {
                var previa = await ambiente.Service.PreverAsync(s1, ambiente.MembroId);
                Assert.Equal(1, (await ambiente.Service.GravarAsync(previa, ambiente.MembroId)).Linhas);
            }

            await using var s2 = new MemoryStream(Encoding.UTF8.GetBytes(b));
            var segunda = await ambiente.Service.PreverAsync(s2, ambiente.MembroId);
            Assert.Equal(1, segunda.Contar(StatusLinhaImportacaoB3.DuplicataImportada));
            Assert.Equal(0, segunda.Contar(StatusLinhaImportacaoB3.Nova));
        }
    }

    [Fact]
    public async Task PrecoTracoDepoisPreenchido_NaoDuplicaLiquidacao()
    {
        const string cab =
            "Entrada/Saída;Data;Movimentação;Produto;Instituição;Quantidade;Preço unitário;Valor da Operação\n";
        const string corpo =
            "Credito;01/02/2024;Transferência - Liquidação;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;";
        var semPreco = cab + corpo + "10; - ; R$100,00 \n";
        var comPreco = cab + corpo + "10; R$10,00 ; R$100,00 \n";

        await using var ambiente = await Ambiente.CriarAsync();
        await using (var s1 = new MemoryStream(Encoding.UTF8.GetBytes(semPreco)))
        {
            var previa = await ambiente.Service.PreverAsync(s1, ambiente.MembroId);
            Assert.Equal(1, (await ambiente.Service.GravarAsync(previa, ambiente.MembroId)).Linhas);
        }

        await using var s2 = new MemoryStream(Encoding.UTF8.GetBytes(comPreco));
        var segunda = await ambiente.Service.PreverAsync(s2, ambiente.MembroId);
        Assert.Equal(0, segunda.Contar(StatusLinhaImportacaoB3.Nova));
        Assert.Equal(1, segunda.Contar(StatusLinhaImportacaoB3.DuplicataImportada));
    }

    [Fact]
    public async Task PreviaDoModelo_ContaOsStatus()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var previa = await ambiente.PreverModeloAsync();

        Assert.Equal(98, previa.Linhas.Count);
        Assert.Equal(0, previa.Contar(StatusLinhaImportacaoB3.Erro));
        Assert.Equal(1, previa.Contar(StatusLinhaImportacaoB3.Informativa));
        Assert.Equal(1, previa.Contar(StatusLinhaImportacaoB3.SemPosicao));
        Assert.Equal(0, previa.Contar(StatusLinhaImportacaoB3.MesFechado));
        Assert.Equal(0, previa.Contar(StatusLinhaImportacaoB3.DuplicataImportada));
        Assert.Equal(0, previa.Contar(StatusLinhaImportacaoB3.PossivelDuplicataManual));
        Assert.Equal(96, previa.Contar(StatusLinhaImportacaoB3.Nova));

        var semPosicao = Assert.Single(previa.Linhas, l => l.Status == StatusLinhaImportacaoB3.SemPosicao);
        Assert.Equal("CDBC25AQGPU", semPosicao.Titulo);
        Assert.False(semPosicao.Marcada);
        Assert.False(semPosicao.PodeMarcar);

        var informativa = Assert.Single(previa.Linhas, l => l.Status == StatusLinhaImportacaoB3.Informativa);
        Assert.Contains("BOVA11", informativa.Titulo, StringComparison.Ordinal);
        Assert.False(informativa.Marcada);

        Assert.All(previa.Linhas.Where(l => l.Status == StatusLinhaImportacaoB3.Nova), l => Assert.True(l.Marcada));
        Assert.Contains(previa.Linhas, l => l.Titulo == "SNAG11" && l.Comando!.TipoAtivo == TipoAtivo.Fii);
        Assert.Contains(previa.Linhas, l => l.Titulo == "BOVA11" && l.Comando?.TipoAtivo == TipoAtivo.Etf);
        Assert.Contains(previa.Linhas, l => l.Titulo == "GOLD11" && l.Comando?.TipoAtivo == TipoAtivo.Etf);
        Assert.Contains(previa.Linhas, l => l.Titulo == "SMAL11" && l.Comando?.TipoAtivo == TipoAtivo.Etf);
        Assert.Contains(previa.Linhas, l => l.Titulo == "AGRO3" && l.Comando?.TipoAtivo == TipoAtivo.Acao);
        Assert.DoesNotContain(
            previa.Linhas,
            l => (l.Titulo is "BOVA11" or "GOLD11" or "SMAL11") && l.Comando?.TipoAtivo == TipoAtivo.Acao);
    }

    [Fact]
    public async Task Gravar_EtfConhecidoJaComoAcao_PassaAEtf_ENaoDuplicaOAtivo()
    {
        const string csv =
            """
            Entrada/Saída;Data;Movimentação;Produto;Instituição;Quantidade;Preço unitário;Valor da Operação
            Credito;01/02/2024;Transferência - Liquidação;BOVA11 - ISHARES IBOVESPA FUNDO DE INDICE;XP INVESTIMENTOS CCTVM S/A;1; R$100,00 ; R$100,00 
            Credito;01/02/2024;Transferência - Liquidação;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;1; R$10,00 ; R$10,00 
            """;

        await using var ambiente = await Ambiente.CriarAsync();
        await using (var contexto = await ambiente.Factory.CreateDbContextAsync())
        {
            contexto.AtivosFinanceiros.Add(new AtivoFinanceiro
            {
                Ticker = "BOVA11",
                Nome = "Ibovespa",
                Tipo = TipoAtivo.Acao
            });
            await contexto.SaveChangesAsync();
        }

        await using var fluxo = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var previa = await ambiente.Service.PreverAsync(fluxo, ambiente.MembroId);
        Assert.Equal(TipoAtivo.Etf, previa.Linhas.Single(l => l.Titulo == "BOVA11").Comando!.TipoAtivo);
        Assert.Equal(TipoAtivo.Acao, previa.Linhas.Single(l => l.Titulo == "PETR4").Comando!.TipoAtivo);
        await ambiente.Service.GravarAsync(previa, ambiente.MembroId);

        await using var conferido = await ambiente.Factory.CreateDbContextAsync();
        var ativos = await conferido.AtivosFinanceiros.ToListAsync();
        Assert.Equal(2, ativos.Count);
        Assert.Equal(TipoAtivo.Etf, ativos.Single(a => a.Ticker == "BOVA11").Tipo);
        Assert.Equal("Ibovespa", ativos.Single(a => a.Ticker == "BOVA11").Nome);
        Assert.Equal(TipoAtivo.Acao, ativos.Single(a => a.Ticker == "PETR4").Tipo);
        Assert.Equal(0, await CatalogoEtfConhecido.ReclassificarAsync(conferido));
        Assert.Equal(2, await conferido.AtivosFinanceiros.CountAsync());
    }

    [Fact]
    public async Task SegundaImportacao_LinhasGravadasFicamDuplicata()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var primeira = await ambiente.PreverModeloAsync();
        var gravadas = await ambiente.Service.GravarAsync(primeira, ambiente.MembroId);

        var segunda = await ambiente.PreverModeloAsync();
        Assert.Equal(gravadas.Linhas, segunda.Contar(StatusLinhaImportacaoB3.DuplicataImportada));
        Assert.Equal(0, segunda.Contar(StatusLinhaImportacaoB3.Nova));
        Assert.Equal(1, segunda.Contar(StatusLinhaImportacaoB3.Informativa));
        Assert.Equal(1, segunda.Contar(StatusLinhaImportacaoB3.SemPosicao));
        Assert.All(segunda.Linhas.Where(l => l.Status == StatusLinhaImportacaoB3.DuplicataImportada), l =>
        {
            Assert.False(l.Marcada);
            Assert.False(l.PodeMarcar);
        });
    }

    [Fact]
    public async Task ReimportarEmbaralhado_TudoGravadoFicaDuplicata()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var primeira = await ambiente.PreverModeloAsync();
        var gravadas = await ambiente.Service.GravarAsync(primeira, ambiente.MembroId);

        await using var csv = new MemoryStream(Embaralhar(CaminhoCsv()));
        var segunda = await ambiente.Service.PreverAsync(csv, ambiente.MembroId);

        Assert.Equal(gravadas.Linhas, segunda.Contar(StatusLinhaImportacaoB3.DuplicataImportada));
        Assert.Equal(0, segunda.Contar(StatusLinhaImportacaoB3.Nova));
        Assert.Equal(1, segunda.Contar(StatusLinhaImportacaoB3.Informativa));
        Assert.Equal(1, segunda.Contar(StatusLinhaImportacaoB3.SemPosicao));
        Assert.All(
            segunda.Linhas.Where(l => l.Status is not StatusLinhaImportacaoB3.Informativa and not StatusLinhaImportacaoB3.SemPosicao),
            l => Assert.Equal(StatusLinhaImportacaoB3.DuplicataImportada, l.Status));
    }

    [Fact]
    public async Task PeriodoMaior_SoAsLinhasExtrasFicamNovas()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var primeira = await ambiente.PreverModeloAsync();
        var gravadas = await ambiente.Service.GravarAsync(primeira, ambiente.MembroId);

        await using var csv = new MemoryStream(Acrescentar(CaminhoCsv(), LinhaExtraAntes, LinhaExtraDepois));
        var segunda = await ambiente.Service.PreverAsync(csv, ambiente.MembroId);

        Assert.Equal(gravadas.Linhas, segunda.Contar(StatusLinhaImportacaoB3.DuplicataImportada));
        Assert.Equal(2, segunda.Contar(StatusLinhaImportacaoB3.Nova));
        Assert.Equal(1, segunda.Contar(StatusLinhaImportacaoB3.Informativa));
        Assert.Equal(1, segunda.Contar(StatusLinhaImportacaoB3.SemPosicao));
        Assert.All(segunda.Linhas.Where(l => l.Status == StatusLinhaImportacaoB3.Nova), l =>
        {
            Assert.True(l.Titulo is "EXTR3" or "EXTR11");
            Assert.True(l.Marcada);
        });
    }

    [Fact]
    public async Task LinhasIdenticas_GravaAsDuas_EReimportacaoMarcaDuplicata()
    {
        const string csv =
            """
            Entrada/Saída;Data;Movimentação;Produto;Instituição;Quantidade;Preço unitário;Valor da Operação
            Credito;01/02/2024;Transferência - Liquidação;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;10; R$10,00 ; R$100,00 
            Credito;01/02/2024;Transferência - Liquidação;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;10; R$10,00 ; R$100,00 
            """;

        await using var ambiente = await Ambiente.CriarAsync();
        await using var primeiraLeitura = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var previa = await ambiente.Service.PreverAsync(primeiraLeitura, ambiente.MembroId);
        Assert.Equal(2, previa.Contar(StatusLinhaImportacaoB3.Nova));
        Assert.Equal(2, previa.Linhas.Select(l => l.Comando!.Hash).Distinct().Count());

        var gravadas = await ambiente.Service.GravarAsync(previa, ambiente.MembroId);
        Assert.Equal(2, gravadas.Linhas);

        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        var hashes = await contexto.OperacoesAtivo.Select(o => o.HashImportacao).ToListAsync();
        Assert.Equal(2, hashes.Distinct().Count());

        await using var releitura = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var segunda = await ambiente.Service.PreverAsync(releitura, ambiente.MembroId);
        Assert.Equal(2, segunda.Contar(StatusLinhaImportacaoB3.DuplicataImportada));
        Assert.Equal(0, segunda.Contar(StatusLinhaImportacaoB3.Nova));
    }

    [Fact]
    public async Task DuasIdenticasDepoisTres_TrazExatamenteUmaNova()
    {
        const string cabecalho =
            "Entrada/Saída;Data;Movimentação;Produto;Instituição;Quantidade;Preço unitário;Valor da Operação";
        const string linha =
            "Credito;01/02/2024;Transferência - Liquidação;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;10; R$10,00 ; R$100,00 ";

        static string Arquivo(int quantidade)
            => cabecalho + "\n" + string.Join('\n', Enumerable.Repeat(linha, quantidade)) + "\n";

        await using var ambiente = await Ambiente.CriarAsync();
        await using (var duas = new MemoryStream(Encoding.UTF8.GetBytes(Arquivo(2))))
        {
            var previa = await ambiente.Service.PreverAsync(duas, ambiente.MembroId);
            Assert.Equal(2, (await ambiente.Service.GravarAsync(previa, ambiente.MembroId)).Linhas);
        }

        await using var tres = new MemoryStream(Encoding.UTF8.GetBytes(Arquivo(3)));
        var seguinte = await ambiente.Service.PreverAsync(tres, ambiente.MembroId);
        Assert.Equal(1, seguinte.Contar(StatusLinhaImportacaoB3.Nova));
        Assert.Equal(2, seguinte.Contar(StatusLinhaImportacaoB3.DuplicataImportada));

        await ambiente.Service.GravarAsync(seguinte, ambiente.MembroId);
        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        Assert.Equal(3, await contexto.OperacoesAtivo.CountAsync());
    }

    [Fact]
    public async Task ManualDeProvento_AvisoNaoCitaQuantidade()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var previa = await ambiente.PreverModeloAsync();
        var provento = previa.Linhas.First(l => l.Comando?.Destino == DestinoImportacaoB3.Provento);
        var comando = provento.Comando!;

        await using (var contexto = await ambiente.Factory.CreateDbContextAsync())
        {
            var ativo = new AtivoFinanceiro
            {
                Ticker = comando.Ticker,
                Nome = comando.Nome,
                Tipo = comando.TipoAtivo
            };
            contexto.AtivosFinanceiros.Add(ativo);
            await contexto.SaveChangesAsync();
            await ambiente.Provento.SalvarAsync(new ProventoFormModel
            {
                AtivoId = ativo.Id,
                Tipo = comando.TipoProvento,
                DataPagamento = comando.Data,
                ValorLiquido = comando.Valor,
                Origem = OrigemProvento.Manual
            }, ambiente.MembroId);
        }

        var deNovo = await ambiente.PreverModeloAsync();
        var linha = deNovo.Linhas.Single(l => l.Numero == provento.Numero);
        Assert.Equal(StatusLinhaImportacaoB3.PossivelDuplicataManual, linha.Status);
        Assert.DoesNotContain("quantidade", linha.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ativo", linha.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data", linha.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tipo", linha.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("valor", linha.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManualIgual_ViraPossivelDuplicata_DesmarcadaEPodeMarcar()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var previa = await ambiente.PreverModeloAsync();
        var compra = previa.Linhas.First(l =>
            l.Comando?.Destino == DestinoImportacaoB3.OperacaoAtivo
            && l.Comando.TipoOperacao == TipoOperacao.Compra);
        var comando = compra.Comando!;

        await using (var contexto = await ambiente.Factory.CreateDbContextAsync())
        {
            var ativo = new AtivoFinanceiro
            {
                Ticker = comando.Ticker,
                Nome = comando.Nome,
                Tipo = comando.TipoAtivo
            };
            contexto.AtivosFinanceiros.Add(ativo);
            await contexto.SaveChangesAsync();
            await ambiente.Resultado.SalvarAsync(new OperacaoAtivoFormModel
            {
                AtivoId = ativo.Id,
                Tipo = comando.TipoOperacao,
                Data = comando.Data,
                Quantidade = comando.Quantidade,
                PrecoUnitario = comando.PrecoUnitario,
                ValorOperacao = comando.Valor,
                Origem = OrigemOperacaoAtivo.Manual
            }, ambiente.MembroId);
        }

        var deNovo = await ambiente.PreverModeloAsync();
        var linha = deNovo.Linhas.Single(l => l.Numero == compra.Numero);
        Assert.Equal(StatusLinhaImportacaoB3.PossivelDuplicataManual, linha.Status);
        Assert.False(linha.Marcada);
        Assert.True(linha.PodeMarcar);

        linha.Marcada = true;
        var reclassificada = await ambiente.Service.ReclassificarAsync(deNovo, ambiente.MembroId);
        var marcada = reclassificada.Linhas.Single(l => l.Numero == compra.Numero);
        Assert.Equal(StatusLinhaImportacaoB3.PossivelDuplicataManual, marcada.Status);
        Assert.True(marcada.Marcada);
    }

    [Fact]
    public async Task MesFechado_BloqueiaENaoGrava()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        await ambiente.FecharAsync(4, 2025);
        var previa = await ambiente.PreverModeloAsync();

        Assert.True(previa.Contar(StatusLinhaImportacaoB3.MesFechado) > 0);
        Assert.All(previa.Linhas.Where(l => l.Status == StatusLinhaImportacaoB3.MesFechado), l =>
        {
            Assert.False(l.Marcada);
            Assert.False(l.PodeMarcar);
        });

        await ambiente.Service.GravarAsync(previa, ambiente.MembroId);

        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        Assert.DoesNotContain(await contexto.Transacoes.ToListAsync(), t => t.Data.Year == 2025 && t.Data.Month == 4);
        Assert.DoesNotContain(await contexto.TitulosRendaFixa.Include(t => t.Operacoes).ToListAsync(),
            t => t.Operacoes.Any(o => o.Data.Year == 2025 && o.Data.Month == 4 && o.Tipo == TipoOperacaoRendaFixa.Resgate));
    }

    [Fact]
    public async Task MesFechadoDepoisDaPrevia_NaoGravaNada()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var previa = await ambiente.PreverModeloAsync();
        Assert.Contains(previa.Linhas, l => l.Marcada && l.Comando!.Data.Year == 2025 && l.Comando.Data.Month == 4);
        await ambiente.FecharAsync(4, 2025);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ambiente.Service.GravarAsync(previa, ambiente.MembroId));
        Assert.Equal(ImportacaoExtratoB3Service.MensagemNadaGravado, ex.Message);

        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        Assert.Empty(await contexto.OperacoesAtivo.ToListAsync());
        Assert.Empty(await contexto.Proventos.ToListAsync());
        Assert.Empty(await contexto.OperacoesRendaFixa.ToListAsync());
        Assert.Empty(await contexto.Transacoes.ToListAsync());
    }

    [Fact]
    public async Task Gravar_Nikos893_Tesouro1928_ESemPosicaoNaoEntra()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var previa = await ambiente.PreverModeloAsync();
        await ambiente.Service.GravarAsync(previa, ambiente.MembroId);

        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        var lancamentos = await contexto.Transacoes.ToListAsync();
        var nikos = lancamentos.Where(t => t.Data.Year == 2025 && t.Data.Month == 4).ToList();
        Assert.Equal(9, nikos.Count);
        Assert.All(nikos, t => Assert.Equal(TipoTransacao.Receita, t.Tipo));
        Assert.Equal(8.93m, nikos.Sum(t => t.Valor));

        var tesouro = lancamentos.Where(t => t.Data.Year == 2023 && t.Data.Month == 5).ToList();
        Assert.Equal(3, tesouro.Count);
        Assert.All(tesouro, t => Assert.Equal(TipoTransacao.Receita, t.Tipo));
        Assert.Equal(19.28m, tesouro.Sum(t => t.Valor));

        Assert.DoesNotContain(await contexto.TitulosRendaFixa.ToListAsync(), t => t.Codigo == "CDBC25AQGPU");
        Assert.All(await contexto.OperacoesAtivo.ToListAsync(), o =>
        {
            Assert.Equal(OrigemOperacaoAtivo.ExtratoB3, o.Origem);
            Assert.False(string.IsNullOrWhiteSpace(o.HashImportacao));
        });
        Assert.All(await contexto.Proventos.ToListAsync(), p =>
        {
            Assert.Equal(OrigemProvento.ExtratoB3, p.Origem);
            Assert.False(string.IsNullOrWhiteSpace(p.HashImportacao));
        });
        Assert.Contains(await contexto.AtivosFinanceiros.ToListAsync(), a => a.Ticker == "SNAG11" && a.Tipo == TipoAtivo.Fii);
    }

    [Fact]
    public async Task Gravar_IgualaReceitaEDespesaDoLancamentoManual()
    {
        await using var importado = await Ambiente.CriarAsync();
        var previa = await importado.PreverModeloAsync();
        await importado.Service.GravarAsync(previa, importado.MembroId);
        var (receitaImportada, despesaImportada) = await TotaisAsync(importado.Factory);

        await using var manual = await Ambiente.CriarAsync();
        await RepetirManualAsync(manual, previa);
        var (receitaManual, despesaManual) = await TotaisAsync(manual.Factory);

        Assert.Equal(receitaManual, receitaImportada);
        Assert.Equal(despesaManual, despesaImportada);
        Assert.True(receitaImportada > 0);
    }

    [Fact]
    public async Task FalhaNoMeio_DesfazTudo()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var falha = new FalhaNoMeio();
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var opcoesFalha = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(falha).Options;

        int membroId;
        await using (var contexto = new AppDbContext(opcoes))
        {
            contexto.Database.EnsureCreated();
            var membro = new MembroFamilia { Nome = "Usuário" };
            contexto.MembrosFamilia.Add(membro);
            await contexto.SaveChangesAsync();
            membroId = membro.Id;
        }

        var fabrica = new Fabrica(opcoesFalha);
        var servico = new ImportacaoExtratoB3Service(
            new ParserExtratoB3(),
            fabrica,
            new ResultadoRealizadoService(fabrica),
            new ProventoService(fabrica),
            new RendaFixaService(fabrica));

        PreviaExtratoB3 previa;
        await using (var csv = File.OpenRead(CaminhoCsv()))
            previa = await servico.PreverAsync(csv, membroId);

        falha.Armado = true;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => servico.GravarAsync(previa, membroId));
        Assert.Equal(ImportacaoExtratoB3Service.MensagemFalha, ex.Message);

        await using var conferido = new AppDbContext(opcoes);
        Assert.Empty(await conferido.OperacoesAtivo.ToListAsync());
        Assert.Empty(await conferido.Proventos.ToListAsync());
        Assert.Empty(await conferido.OperacoesRendaFixa.ToListAsync());
        Assert.Empty(await conferido.Transacoes.ToListAsync());
        Assert.Empty(await conferido.AtivosFinanceiros.ToListAsync());
        Assert.Empty(await conferido.TitulosRendaFixa.ToListAsync());
    }

    [Fact]
    public async Task ViradaDeMes_PregaoNaApuracao_LancamentoEHashNaLiquidacao_ReimportacaoNaoDuplica()
    {
        const string csv =
            """
            Entrada/Saída;Data;Movimentação;Produto;Instituição;Quantidade;Preço unitário;Valor da Operação
            Credito;10/01/2024;Transferência - Liquidação;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;100; R$100,00 ; R$10.000,00 
            Debito;02/02/2024;Transferência - Liquidação;PETR4 - PETROLEO BRASILEIRO;XP INVESTIMENTOS CCTVM S/A;100; R$250,00 ; R$25.000,00 
            """;

        await using var ambiente = await Ambiente.CriarAsync();
        await using var primeiraLeitura = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var previa = await ambiente.Service.PreverAsync(primeiraLeitura, ambiente.MembroId);
        Assert.Equal(2, previa.Contar(StatusLinhaImportacaoB3.Nova));
        Assert.Empty(new ParserExtratoB3().Ler(new MemoryStream(Encoding.UTF8.GetBytes(csv))).Erros);

        await ambiente.Service.GravarAsync(previa, ambiente.MembroId);

        await using (var contexto = await ambiente.Factory.CreateDbContextAsync())
        {
            var operacoes = await contexto.OperacoesAtivo.OrderBy(o => o.Data).ToListAsync();
            Assert.Equal(2, operacoes.Count);

            var compra = operacoes[0];
            Assert.Equal(TipoOperacao.Compra, compra.Tipo);
            Assert.Equal(new DateTime(2024, 1, 10), compra.Data);
            Assert.Equal(new DateTime(2024, 1, 8), compra.DataPregao);

            var venda = operacoes[1];
            Assert.Equal(TipoOperacao.Venda, venda.Tipo);
            Assert.Equal(new DateTime(2024, 2, 2), venda.Data);
            Assert.Equal(new DateTime(2024, 1, 31), venda.DataPregao);

            var lido = new ParserExtratoB3().Ler(new MemoryStream(Encoding.UTF8.GetBytes(csv)));
            var ocorrencias = HashLinhaExtratoB3.Ocorrencias(lido.Linhas);
            var indiceVenda = lido.Linhas.ToList().FindIndex(linha => linha.Data == new DateOnly(2024, 2, 2));
            var hashLiquidacao = HashLinhaExtratoB3.Calcular(lido.Linhas[indiceVenda], ocorrencias[indiceVenda]);
            var noPregao = lido.Linhas[indiceVenda] with { Data = new DateOnly(2024, 1, 31) };
            Assert.Equal(hashLiquidacao, venda.HashImportacao);
            Assert.NotEqual(hashLiquidacao, HashLinhaExtratoB3.Calcular(noPregao, ocorrencias[indiceVenda]));
            Assert.StartsWith("2024-02-02|", HashLinhaExtratoB3.Chave(lido.Linhas[indiceVenda]), StringComparison.Ordinal);

            var lancamento = Assert.Single(await contexto.Transacoes.ToListAsync());
            Assert.Equal(TipoTransacao.Receita, lancamento.Tipo);
            Assert.Equal(15_000m, lancamento.Valor);
            Assert.Equal(new DateTime(2024, 2, 2), lancamento.Data);
            Assert.Equal(venda.Id, lancamento.OperacaoAtivoId);

            var apuracao = ApuradorIrMensal.Apurar(contexto, 2024);
            var janeiro = apuracao.Meses.Single(linha => linha.Mes == 1 && linha.Cesta == CestaApuracaoIr.Comum);
            Assert.Equal(25_000m, janeiro.VendasAcoesComuns);
            Assert.Equal(15_000m, janeiro.Resultado);
            Assert.Equal(new DateTime(2024, 2, 29), apuracao.Darfs.Single(linha => linha.Mes == 1).Vencimento);
            Assert.Equal(0m, apuracao.Meses.Single(linha => linha.Mes == 2 && linha.Cesta == CestaApuracaoIr.Comum).Resultado);

            await ambiente.Resultado.SalvarAsync(new OperacaoAtivoFormModel
            {
                Id = venda.Id,
                AtivoId = venda.AtivoId,
                Tipo = venda.Tipo,
                Data = venda.Data,
                Quantidade = venda.Quantidade,
                PrecoUnitario = venda.PrecoUnitario,
                ValorOperacao = venda.ValorOperacao,
                Custos = venda.Custos,
                Corretora = venda.Corretora
            }, ambiente.MembroId);
        }

        await using (var contexto = await ambiente.Factory.CreateDbContextAsync())
        {
            var venda = await contexto.OperacoesAtivo.SingleAsync(o => o.Tipo == TipoOperacao.Venda);
            Assert.Equal(OrigemOperacaoAtivo.ExtratoB3, venda.Origem);
            Assert.Equal(new DateTime(2024, 2, 2), venda.Data);
            Assert.Equal(new DateTime(2024, 1, 31), venda.DataPregao);
            Assert.Equal(new DateTime(2024, 2, 2), (await contexto.Transacoes.SingleAsync()).Data);
        }

        await using var releitura = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var segunda = await ambiente.Service.PreverAsync(releitura, ambiente.MembroId);
        Assert.Equal(2, segunda.Contar(StatusLinhaImportacaoB3.DuplicataImportada));
        Assert.Equal(0, segunda.Contar(StatusLinhaImportacaoB3.Nova));
        var repetida = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ambiente.Service.GravarAsync(segunda, ambiente.MembroId));
        Assert.Equal(ImportacaoExtratoB3Service.MensagemSemMarcacao, repetida.Message);

        await using var conferido = await ambiente.Factory.CreateDbContextAsync();
        Assert.Equal(2, await conferido.OperacoesAtivo.CountAsync());
    }

    [Fact]
    public async Task LciELcaNovos_GravamOTipo_EOutroJaSalvoPermanece()
    {
        const string cab =
            "Entrada/Saída;Data;Movimentação;Produto;Instituição;Quantidade;Preço unitário;Valor da Operação\n";
        var csv = cab
            + "Credito;02/03/2024;Compra;LCI - LCI123 - ITAU;ITAU UNIBANCO S.A.;10;R$1,00;R$10,00\n"
            + "Credito;03/03/2024;Compra;LCA - LCA999;ITAU UNIBANCO S.A.;3;R$2,50;R$7,50\n"
            + "Credito;04/03/2024;Compra;CDB - CDBABC;NIKOS;4;R$1,00;R$4,00\n"
            + "Credito;05/03/2024;Compra;LCI - LCINOVO - ITAU;ITAU UNIBANCO S.A.;2;R$1,00;R$2,00\n"
            + "Credito;06/03/2024;Compra;LCI - CDB9999;NIKOS;1;R$1,00;R$1,00\n"
            + "Credito;07/03/2024;Compra;LCA - CDB9999;XP INVESTIMENTOS CCTVM S/A;1;R$1,00;R$1,00\n"
            + "Credito;08/03/2024;Compra;LCA - LCI999 - BANCO;CLEAR CORRETORA;1;R$1,00;R$1,00\n"
            + "Credito;09/03/2024;Compra;CDB - CDB1111 - BANCO;XP INVESTIMENTOS CCTVM S/A;1;R$1,00;R$1,00\n";

        await using var ambiente = await Ambiente.CriarAsync();
        await using (var contexto = await ambiente.Factory.CreateDbContextAsync())
        {
            contexto.TitulosRendaFixa.Add(new TituloRendaFixa
            {
                Tipo = TipoTituloRendaFixa.Outro,
                Codigo = "LCI123",
                Nome = "Letra antiga",
                Corretora = "Itaú",
                Ativo = true
            });
            await contexto.SaveChangesAsync();
        }

        await using var fluxo = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var previa = await ambiente.Service.PreverAsync(fluxo, ambiente.MembroId);
        Assert.Equal(8, (await ambiente.Service.GravarAsync(previa, ambiente.MembroId)).Linhas);

        await using var conferido = await ambiente.Factory.CreateDbContextAsync();
        var titulos = await conferido.TitulosRendaFixa.ToListAsync();
        Assert.Equal(TipoTituloRendaFixa.Outro, Assert.Single(titulos, titulo => titulo.Codigo == "LCI123").Tipo);
        Assert.Equal(TipoTituloRendaFixa.LCI, Assert.Single(titulos, titulo => titulo.Codigo == "LCINOVO").Tipo);
        Assert.Equal(TipoTituloRendaFixa.LCA, Assert.Single(titulos, titulo => titulo.Codigo == "LCA999").Tipo);
        Assert.Equal(TipoTituloRendaFixa.LCI, Assert.Single(titulos, titulo => titulo.Codigo == "CDB9999" && titulo.Corretora == "Nikos").Tipo);
        Assert.Equal(TipoTituloRendaFixa.LCA, Assert.Single(titulos, titulo => titulo.Codigo == "CDB9999" && titulo.Corretora == "XP").Tipo);
        Assert.Equal(TipoTituloRendaFixa.LCA, Assert.Single(titulos, titulo => titulo.Codigo == "LCI999").Tipo);
        Assert.Equal(TipoTituloRendaFixa.CDB, Assert.Single(titulos, titulo => titulo.Codigo == "CDBABC").Tipo);
        Assert.Equal(TipoTituloRendaFixa.CDB, Assert.Single(titulos, titulo => titulo.Codigo == "CDB1111").Tipo);
    }

    private static async Task RepetirManualAsync(Ambiente ambiente, PreviaExtratoB3 previa)
    {
        var linhas = previa.Linhas
            .Where(l => l.Marcada && l.Comando is not null)
            .OrderBy(l => l.Comando!.Data)
            .ThenBy(l => l.Comando!.Destino == DestinoImportacaoB3.Provento ? 2
                : l.Comando!.Destino == DestinoImportacaoB3.RendaFixa
                    ? (l.Comando.TipoRendaFixa == TipoOperacaoRendaFixa.Aplicacao ? 0 : 1)
                    : (l.Comando!.TipoOperacao == TipoOperacao.Compra ? 0 : 1))
            .ThenBy(l => l.Numero)
            .ToList();

        var ativos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var titulos = new Dictionary<(string, string), int>();
        foreach (var linha in linhas)
        {
            var comando = linha.Comando!;
            if (comando.Destino == DestinoImportacaoB3.OperacaoAtivo)
            {
                var ativoId = await GarantirAtivoManualAsync(ambiente, ativos, comando);
                await ambiente.Resultado.SalvarAsync(new OperacaoAtivoFormModel
                {
                    AtivoId = ativoId,
                    Tipo = comando.TipoOperacao,
                    Data = comando.Data,
                    Quantidade = comando.Quantidade,
                    PrecoUnitario = comando.PrecoUnitario,
                    ValorOperacao = comando.Valor,
                    Corretora = comando.Corretora,
                    Origem = OrigemOperacaoAtivo.Manual
                }, ambiente.MembroId);
            }
            else if (comando.Destino == DestinoImportacaoB3.Provento)
            {
                var ativoId = await GarantirAtivoManualAsync(ambiente, ativos, comando);
                await ambiente.Provento.SalvarAsync(new ProventoFormModel
                {
                    AtivoId = ativoId,
                    Tipo = comando.TipoProvento,
                    DataPagamento = comando.Data,
                    ValorLiquido = comando.Valor,
                    Origem = OrigemProvento.Manual
                }, ambiente.MembroId);
            }
            else if (comando.Destino == DestinoImportacaoB3.RendaFixa)
            {
                var tituloId = await GarantirTituloManualAsync(ambiente, titulos, comando);
                await ambiente.RendaFixa.SalvarAsync(new OperacaoRendaFixaFormModel
                {
                    TituloRendaFixaId = tituloId,
                    Data = comando.Data,
                    Tipo = comando.TipoRendaFixa,
                    Quantidade = comando.Quantidade,
                    PrecoUnitario = comando.PrecoUnitario,
                    Valor = comando.Valor,
                    Origem = OrigemOperacaoRendaFixa.Manual
                }, ambiente.MembroId);
            }
        }
    }

    private static async Task<int> GarantirAtivoManualAsync(
        Ambiente ambiente,
        Dictionary<string, int> cache,
        ComandoImportacaoB3 comando)
    {
        if (cache.TryGetValue(comando.Ticker, out var id))
            return id;
        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        var ativo = new AtivoFinanceiro { Ticker = comando.Ticker, Nome = comando.Nome, Tipo = comando.TipoAtivo };
        contexto.AtivosFinanceiros.Add(ativo);
        await contexto.SaveChangesAsync();
        cache[comando.Ticker] = ativo.Id;
        return ativo.Id;
    }

    private static async Task<int> GarantirTituloManualAsync(
        Ambiente ambiente,
        Dictionary<(string, string), int> cache,
        ComandoImportacaoB3 comando)
    {
        var chave = (comando.Ticker, comando.Corretora);
        if (cache.TryGetValue(chave, out var id))
            return id;
        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        var titulo = new TituloRendaFixa
        {
            Tipo = comando.TipoTitulo,
            Codigo = comando.Ticker,
            Nome = comando.Nome,
            Emissor = comando.Emissor,
            AnoVencimento = comando.AnoVencimento,
            Corretora = comando.Corretora
        };
        contexto.TitulosRendaFixa.Add(titulo);
        await contexto.SaveChangesAsync();
        cache[chave] = titulo.Id;
        return titulo.Id;
    }

    private static async Task<(decimal Receita, decimal Despesa)> TotaisAsync(TestDbContextFactory factory)
    {
        await using var contexto = await factory.CreateDbContextAsync();
        var lancamentos = await contexto.Transacoes.ToListAsync();
        return (
            lancamentos.Where(t => t.Tipo == TipoTransacao.Receita).Sum(t => t.Valor),
            lancamentos.Where(t => t.Tipo == TipoTransacao.Despesa).Sum(t => t.Valor));
    }

    private const string LinhaExtraAntes =
        "Credito;02/01/2020;Transferência - Liquidação;EXTR3 - EXTRA ANTES;XP INVESTIMENTOS CCTVM S/A;7; R$3,00 ; R$21,00 ";

    private const string LinhaExtraDepois =
        "Credito;31/12/2026;Rendimento;EXTR11 - EXTRA DEPOIS;CLEAR CORRETORA - GRUPO XP;4; R$0,10 ; R$0,40 ";

    private static byte[] Embaralhar(string caminho)
    {
        var dados = LinhasDeDados(caminho);
        var original = string.Join('\n', dados);
        var rng = new Random(20261020);
        for (var i = dados.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (dados[i], dados[j]) = (dados[j], dados[i]);
        }

        Assert.NotEqual(original, string.Join('\n', dados));
        return Codificar(caminho, dados);
    }

    private static byte[] Acrescentar(string caminho, params string[] extras)
    {
        var dados = LinhasDeDados(caminho);
        dados.Insert(0, extras[0]);
        dados.Add(extras[1]);
        return Codificar(caminho, dados);
    }

    private static List<string> LinhasDeDados(string caminho)
    {
        var texto = File.ReadAllText(caminho).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var linhas = texto.Split('\n').ToList();
        while (linhas.Count > 0 && linhas[^1].Length == 0)
            linhas.RemoveAt(linhas.Count - 1);
        return linhas.Skip(1).ToList();
    }

    private static byte[] Codificar(string caminho, IReadOnlyList<string> dados)
    {
        var cabecalho = File.ReadLines(caminho).First();
        var texto = cabecalho + "\n" + string.Join('\n', dados) + "\n";
        return Encoding.UTF8.GetBytes(texto);
    }

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

    private sealed class FalhaNoMeio : SaveChangesInterceptor
    {
        public bool Armado { get; set; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (Armado)
                throw new IOException("falha no meio do SaveChanges");
            return new ValueTask<int>(result);
        }
    }

    private sealed class Fabrica(DbContextOptions<AppDbContext> opcoes) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(opcoes);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class Ambiente : IAsyncDisposable
    {
        public TestDbContextFactory Factory { get; }
        public ImportacaoExtratoB3Service Service { get; }
        public ResultadoRealizadoService Resultado { get; }
        public ProventoService Provento { get; }
        public RendaFixaService RendaFixa { get; }
        public int MembroId { get; }

        private Ambiente(
            TestDbContextFactory factory,
            ImportacaoExtratoB3Service service,
            ResultadoRealizadoService resultado,
            ProventoService provento,
            RendaFixaService rendaFixa,
            int membroId)
        {
            Factory = factory;
            Service = service;
            Resultado = resultado;
            Provento = provento;
            RendaFixa = rendaFixa;
            MembroId = membroId;
        }

        public static async Task<Ambiente> CriarAsync()
        {
            var factory = new TestDbContextFactory();
            await using var contexto = await factory.CreateDbContextAsync();
            var membro = new MembroFamilia { Nome = "Usuário" };
            contexto.MembrosFamilia.Add(membro);
            await contexto.SaveChangesAsync();
            var resultado = new ResultadoRealizadoService(factory);
            var provento = new ProventoService(factory);
            var renda = new RendaFixaService(factory);
            var servico = new ImportacaoExtratoB3Service(new ParserExtratoB3(), factory, resultado, provento, renda);
            return new Ambiente(factory, servico, resultado, provento, renda, membro.Id);
        }

        public async Task<PreviaExtratoB3> PreverModeloAsync()
        {
            await using var csv = File.OpenRead(CaminhoCsv());
            return await Service.PreverAsync(csv, MembroId);
        }

        public async Task FecharAsync(int mes, int ano)
        {
            await using var contexto = await Factory.CreateDbContextAsync();
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = mes, Ano = ano, Fechado = true });
            await contexto.SaveChangesAsync();
        }

        public ValueTask DisposeAsync()
        {
            Factory.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
