using System.ComponentModel.DataAnnotations;
using System.Reflection;
using OrcaFacil.Core.Categorias;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Tests.Categorias;

public class CoresCategoriaTests
{
    private static readonly string[] Originais =
    [
        "#6DA538", "#D64242", "#659ECD", "#A2B94F", "#EF7F5D",
        "#c7a98f", "#a97ca0", "#8fa9c7"
    ];

    private static readonly string[] Novas = ["#21867A", "#A8740A", "#9751CD"];

    private static readonly string[] Fundos = ["#FFFFFF", "#EFF3F7", "#12151B", "#1B1F27"];

    [Fact]
    public void MantemAsCoresOriginaisEAcrescentaTresNoFinal()
    {
        Assert.Equal(Originais.Length + Novas.Length, CoresCategoria.Opcoes.Count);
        Assert.Equal(Originais, CoresCategoria.Opcoes.Take(Originais.Length));
        Assert.Equal(Novas, CoresCategoria.Opcoes.Skip(Originais.Length));
    }

    [Fact]
    public void NaoRepeteCorECabeNoCampoDaCategoria()
    {
        var tamanhoMaximo = typeof(Categoria)
            .GetProperty(nameof(Categoria.CorHex))!
            .GetCustomAttribute<MaxLengthAttribute>()!
            .Length;

        var distintas = CoresCategoria.Opcoes
            .Select(cor => cor.ToUpperInvariant())
            .Distinct()
            .Count();

        Assert.Equal(CoresCategoria.Opcoes.Count, distintas);
        Assert.All(CoresCategoria.Opcoes, cor =>
        {
            Assert.Matches("^#[0-9A-Fa-f]{6}$", cor);
            Assert.InRange(cor.Length, 1, tamanhoMaximo);
        });
    }

    [Fact]
    public void NovasCoresContrastamComFundoClaroEEscuro()
    {
        Assert.All(Novas, cor =>
            Assert.All(Fundos, fundo =>
                Assert.True(Contraste(cor, fundo) >= 3.0, $"{cor} sobre {fundo}")));
    }

    private static double Contraste(string cor, string fundo)
    {
        var clara = Math.Max(Luminancia(cor), Luminancia(fundo));
        var escura = Math.Min(Luminancia(cor), Luminancia(fundo));
        return (clara + 0.05) / (escura + 0.05);
    }

    private static double Luminancia(string hex)
    {
        double Canal(int inicio)
        {
            var valor = Convert.ToInt32(hex.Substring(inicio, 2), 16) / 255d;
            return valor <= 0.04045 ? valor / 12.92 : Math.Pow((valor + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Canal(1) + 0.7152 * Canal(3) + 0.0722 * Canal(5);
    }
}
