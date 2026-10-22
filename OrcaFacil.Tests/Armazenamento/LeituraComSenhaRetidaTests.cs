using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Espelho;

namespace OrcaFacil.Tests.Armazenamento;

public class LeituraComSenhaRetidaTests
{
    private const string SenhaAntiga = "NAO-LOGAR-SENHA-9f3a";
    private const string SenhaNova = "TrilhaAzul92";

    [Fact]
    public async Task NovaFalha_AntigaAbre_ESoltaARetencao()
    {
        var provedor = ProvedorComRetencao();
        var tentativas = new List<string>();

        var leitura = await LerAsync(provedor, tentativas, senha =>
            senha == SenhaAntiga
                ? LeituraEspelho.Ok(1)
                : throw new SenhaIncorretaException());

        Assert.True(leitura.Legivel);
        Assert.Equal([SenhaNova, SenhaAntiga], tentativas);
        Assert.Equal(SenhaAntiga, provedor.Obter());
        Assert.Null(provedor.ObterAnterior());
        AssertNaoVaza(leitura, provedor);
    }

    [Fact]
    public async Task AsDuasFalham_AsDuasFicam_EProximaLeituraTentaAsDuasDeNovo()
    {
        var provedor = ProvedorComRetencao();
        var tentativas = new List<string>();

        var leitura = await LerAsync(provedor, tentativas, _ => throw new SenhaIncorretaException());

        Assert.False(leitura.Legivel);
        Assert.Equal("Senha incorreta.", leitura.Motivo);
        Assert.Equal([SenhaNova, SenhaAntiga], tentativas);
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.Equal(SenhaAntiga, provedor.ObterAnterior());
        AssertNaoVaza(leitura, provedor);

        tentativas.Clear();
        var seguinte = await LerAsync(provedor, tentativas, _ => throw new SenhaIncorretaException());

        Assert.False(seguinte.Legivel);
        Assert.Equal("Senha incorreta.", seguinte.Motivo);
        Assert.Equal([SenhaNova, SenhaAntiga], tentativas);
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.Equal(SenhaAntiga, provedor.ObterAnterior());
        AssertNaoVaza(seguinte, provedor);
    }

    [Fact]
    public async Task NovaAbre_AntigaNuncaETentada()
    {
        var provedor = ProvedorComRetencao();
        var tentativas = new List<string>();

        var leitura = await LerAsync(provedor, tentativas, senha =>
            senha == SenhaNova
                ? LeituraEspelho.Ok(2)
                : throw new SenhaIncorretaException());

        Assert.True(leitura.Legivel);
        Assert.Equal(2, leitura.VersaoSchema);
        Assert.Equal([SenhaNova], tentativas);
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.Null(provedor.ObterAnterior());
        AssertNaoVaza(leitura, provedor);
    }

    private static ProvedorSenhaEspelhoMemoria ProvedorComRetencao()
    {
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(SenhaAntiga, SenhaAntiga).Aceita);
        Assert.True(provedor.DefinirRetendoAnterior(SenhaNova, SenhaNova).Aceita);
        Assert.Equal(SenhaNova, provedor.Obter());
        Assert.Equal(SenhaAntiga, provedor.ObterAnterior());
        return provedor;
    }

    private static Task<LeituraEspelho> LerAsync(
        ProvedorSenhaEspelhoMemoria provedor,
        List<string> tentativas,
        Func<string, LeituraEspelho> abrir)
        => LeituraComSenhaRetida.LerAsync(
            "espelho.bin",
            provedor,
            VersaoSchemaAplicacao.DoApp,
            CancellationToken.None,
            (_, senha, _, _) =>
            {
                tentativas.Add(senha);
                return Task.FromResult(abrir(senha));
            });

    private static void AssertNaoVaza(LeituraEspelho leitura, ProvedorSenhaEspelhoMemoria provedor)
    {
        Assert.DoesNotContain(SenhaAntiga, leitura.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SenhaNova, leitura.ToString(), StringComparison.Ordinal);
        if (leitura.Motivo is not null)
        {
            Assert.DoesNotContain(SenhaAntiga, leitura.Motivo, StringComparison.Ordinal);
            Assert.DoesNotContain(SenhaNova, leitura.Motivo, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(SenhaAntiga, provedor.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SenhaNova, provedor.ToString(), StringComparison.Ordinal);
    }
}
