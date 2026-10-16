using System.ComponentModel.DataAnnotations;
using System.Reflection;
using OrcaFacil.Core.Categorias;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Tests.Categorias;

public class IconesCategoriaTests
{
    private static readonly string[] Originais =
    [
        "🏠", "🚗", "💡", "📱", "🛒", "💳", "🏥", "🎓", "🎉", "💰",
        "📈", "🧾", "🍔", "☕", "🚌", "✈️", "🎁", "🐾", "🧴", "📦",
        "🏷️", "⚡", "🎯", "🛟", "🧑‍🍼", "📚", "🏋️", "🎮", "🚿", "🌐"
    ];

    private static readonly string[] Novos = ["👕", "⛽", "🏦", "🤝", "📺"];

    [Fact]
    public void MantemOsIconesOriginaisEAcrescentaCincoNoFinal()
    {
        Assert.Equal(Originais.Length + Novos.Length, IconesCategoria.Opcoes.Count);
        Assert.Equal(Originais, IconesCategoria.Opcoes.Take(Originais.Length));
        Assert.Equal(Novos, IconesCategoria.Opcoes.Skip(Originais.Length));
    }

    [Fact]
    public void NaoRepeteIconeEOsNovosCabemNoCampoDaCategoria()
    {
        var tamanhoMaximo = typeof(Categoria)
            .GetProperty(nameof(Categoria.Icone))!
            .GetCustomAttribute<MaxLengthAttribute>()!
            .Length;

        Assert.Equal(IconesCategoria.Opcoes.Count, IconesCategoria.Opcoes.Distinct().Count());
        Assert.All(IconesCategoria.Opcoes, icone => Assert.False(string.IsNullOrWhiteSpace(icone)));
        Assert.All(Novos, icone => Assert.InRange(icone.Length, 1, tamanhoMaximo));
    }
}
