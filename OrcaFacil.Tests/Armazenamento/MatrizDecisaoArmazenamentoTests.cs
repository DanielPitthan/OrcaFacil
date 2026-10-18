using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Tests.Armazenamento;

public class MatrizDecisaoArmazenamentoTests
{
    [Fact]
    public void TodaCombinacaoCoerente_SegueARegra_EIncoerenteERecusada()
    {
        var coerentes = 0;
        foreach (var banco in Enum.GetValues<SituacaoBancoVivo>())
        {
            foreach (var espelho in Enum.GetValues<SituacaoEspelho>())
            {
                foreach (var schema in Enum.GetValues<ComparacaoSchemaEspelho>())
                {
                    var estado = new EstadoObservadoArmazenamento(banco, espelho, schema);
                    if (!estado.EhCoerente)
                    {
                        Assert.Throws<ArgumentException>(() => MatrizDecisaoArmazenamento.Decidir(estado));
                        Assert.DoesNotContain(
                            MatrizDecisaoArmazenamento.Tabela,
                            linha => linha.BancoVivo == banco && linha.Espelho == espelho && linha.SchemaEspelho == schema);
                        continue;
                    }

                    coerentes++;
                    var decisao = MatrizDecisaoArmazenamento.Decidir(estado);
                    Assert.Equal(AcaoEsperada(estado), decisao.Acao);
                    Assert.False(string.IsNullOrWhiteSpace(decisao.Motivo));
                    Assert.Equal(decisao.Acao, Linha(estado).Acao);
                    Assert.Equal(decisao.Motivo, Linha(estado).Motivo);
                }
            }
        }

        Assert.Equal(40, coerentes);
        Assert.Equal(40, MatrizDecisaoArmazenamento.Tabela.Count);
    }

    [Fact]
    public void Semear_QuandoNaoHaDadoUtilizavel_OuQuandoOSchemaNaoTemPerfil()
    {
        var semeaduras = MatrizDecisaoArmazenamento.Tabela
            .Where(linha => linha.Acao == AcaoArmazenamento.Semear)
            .OrderBy(linha => linha.BancoVivo)
            .ToList();

        Assert.Equal(7, semeaduras.Count);
        Assert.Contains(semeaduras, linha => linha.BancoVivo == SituacaoBancoVivo.Ausente);
        Assert.Contains(semeaduras, linha => linha.BancoVivo == SituacaoBancoVivo.Vazio);
        Assert.Equal(5, semeaduras.Count(linha => linha.BancoVivo == SituacaoBancoVivo.SemPerfil));

        foreach (var linha in MatrizDecisaoArmazenamento.Tabela)
        {
            var semDadoESemEspelho = linha.Espelho == SituacaoEspelho.Ausente
                && linha.BancoVivo is SituacaoBancoVivo.Ausente or SituacaoBancoVivo.Vazio;
            var schemaSemPerfil = linha.BancoVivo == SituacaoBancoVivo.SemPerfil;
            if (semDadoESemEspelho || schemaSemPerfil)
                Assert.Equal(AcaoArmazenamento.Semear, linha.Acao);
            else
                Assert.NotEqual(AcaoArmazenamento.Semear, linha.Acao);
        }

        Assert.All(
            semeaduras.Where(linha => linha.BancoVivo is SituacaoBancoVivo.Ausente or SituacaoBancoVivo.Vazio),
            linha => Assert.Equal(SituacaoEspelho.Ausente, linha.Espelho));
    }

    [Fact]
    public void BancoUsavel_NaoDependeDoEspelho()
    {
        foreach (var linha in MatrizDecisaoArmazenamento.Tabela)
        {
            var esperada = linha.BancoVivo switch
            {
                SituacaoBancoVivo.Integro => AcaoArmazenamento.UsarBancoVivo,
                SituacaoBancoVivo.SemPerfil => AcaoArmazenamento.Semear,
                SituacaoBancoVivo.Desatualizado => AcaoArmazenamento.MigrarBancoVivo,
                SituacaoBancoVivo.ParcialAddCartoes => AcaoArmazenamento.MigrarBancoVivo,
                SituacaoBancoVivo.SchemaMaisNovoQueOApp => AcaoArmazenamento.PedirAtualizacaoDoApp,
                _ => (AcaoArmazenamento?)null
            };

            if (esperada is AcaoArmazenamento acao)
                Assert.Equal(acao, linha.Acao);
        }

        Assert.Equal(5, MatrizDecisaoArmazenamento.Tabela.Count(linha => linha.Acao == AcaoArmazenamento.UsarBancoVivo));
        Assert.Equal(10, MatrizDecisaoArmazenamento.Tabela.Count(linha => linha.Acao == AcaoArmazenamento.MigrarBancoVivo));
        Assert.Equal(8, MatrizDecisaoArmazenamento.Tabela.Count(linha => linha.Acao == AcaoArmazenamento.PedirAtualizacaoDoApp));
        Assert.Equal(6, MatrizDecisaoArmazenamento.Tabela.Count(linha => linha.Acao == AcaoArmazenamento.RestaurarDoEspelho));
        Assert.Equal(4, MatrizDecisaoArmazenamento.Tabela.Count(linha => linha.Acao == AcaoArmazenamento.TelaDeRecuperacao));
        Assert.Equal(7, MatrizDecisaoArmazenamento.Tabela.Count(linha => linha.Acao == AcaoArmazenamento.Semear));
    }

    [Fact]
    public void EspelhoMaisNovo_ComBancoInutilizavel_PedeAtualizacao_ENaoRestaura()
    {
        foreach (var banco in new[] { SituacaoBancoVivo.Ausente, SituacaoBancoVivo.Vazio, SituacaoBancoVivo.Inconsistente })
        {
            var estado = new EstadoObservadoArmazenamento(
                banco,
                SituacaoEspelho.Legivel,
                ComparacaoSchemaEspelho.EspelhoMaisNovo);

            var decisao = MatrizDecisaoArmazenamento.Decidir(estado);

            Assert.Equal(AcaoArmazenamento.PedirAtualizacaoDoApp, decisao.Acao);
            Assert.NotEqual(AcaoArmazenamento.RestaurarDoEspelho, decisao.Acao);
            Assert.NotEqual(AcaoArmazenamento.Semear, decisao.Acao);
        }
    }

    [Fact]
    public void Decidir_EstadoNulo_Lanca()
    {
        Assert.Throws<ArgumentNullException>(() => MatrizDecisaoArmazenamento.Decidir(null!));
    }

    [Theory]
    [InlineData(0, 0, ComparacaoSchemaEspelho.Igual)]
    [InlineData(4, 4, ComparacaoSchemaEspelho.Igual)]
    [InlineData(4, 3, ComparacaoSchemaEspelho.EspelhoMaisAntigo)]
    [InlineData(4, 5, ComparacaoSchemaEspelho.EspelhoMaisNovo)]
    public void CompararSchema(int versaoApp, int versaoEspelho, ComparacaoSchemaEspelho esperada)
    {
        Assert.Equal(esperada, ComparadorSchemaEspelho.Comparar(versaoApp, versaoEspelho));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void CompararSchema_Negativo_Lanca(int versaoApp, int versaoEspelho)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ComparadorSchemaEspelho.Comparar(versaoApp, versaoEspelho));
    }

    /// <summary>
    /// Especificação independente da lista. Se a tabela mudar uma célula, este switch não acompanha sozinho.
    /// </summary>
    private static AcaoArmazenamento AcaoEsperada(EstadoObservadoArmazenamento estado) => estado.BancoVivo switch
    {
        SituacaoBancoVivo.Integro => AcaoArmazenamento.UsarBancoVivo,
        SituacaoBancoVivo.SemPerfil => AcaoArmazenamento.Semear,
        SituacaoBancoVivo.Desatualizado => AcaoArmazenamento.MigrarBancoVivo,
        SituacaoBancoVivo.SchemaMaisNovoQueOApp => AcaoArmazenamento.PedirAtualizacaoDoApp,
        SituacaoBancoVivo.ParcialAddCartoes => AcaoArmazenamento.MigrarBancoVivo,
        SituacaoBancoVivo.Ausente or SituacaoBancoVivo.Vazio => estado.Espelho switch
        {
            SituacaoEspelho.Ausente => AcaoArmazenamento.Semear,
            SituacaoEspelho.Corrompido => AcaoArmazenamento.TelaDeRecuperacao,
            SituacaoEspelho.Legivel when estado.SchemaEspelho == ComparacaoSchemaEspelho.EspelhoMaisNovo => AcaoArmazenamento.PedirAtualizacaoDoApp,
            SituacaoEspelho.Legivel => AcaoArmazenamento.RestaurarDoEspelho,
            _ => throw new InvalidOperationException($"Espelho fora da especificação: {estado.Espelho}.")
        },
        SituacaoBancoVivo.Inconsistente => estado.Espelho switch
        {
            SituacaoEspelho.Ausente or SituacaoEspelho.Corrompido => AcaoArmazenamento.TelaDeRecuperacao,
            SituacaoEspelho.Legivel when estado.SchemaEspelho == ComparacaoSchemaEspelho.EspelhoMaisNovo => AcaoArmazenamento.PedirAtualizacaoDoApp,
            SituacaoEspelho.Legivel => AcaoArmazenamento.RestaurarDoEspelho,
            _ => throw new InvalidOperationException($"Espelho fora da especificação: {estado.Espelho}.")
        },
        _ => throw new InvalidOperationException($"Banco fora da especificação: {estado.BancoVivo}.")
    };

    private static LinhaMatrizArmazenamento Linha(EstadoObservadoArmazenamento estado)
        => MatrizDecisaoArmazenamento.Tabela.Single(linha =>
            linha.BancoVivo == estado.BancoVivo
            && linha.Espelho == estado.Espelho
            && linha.SchemaEspelho == estado.SchemaEspelho);
}
