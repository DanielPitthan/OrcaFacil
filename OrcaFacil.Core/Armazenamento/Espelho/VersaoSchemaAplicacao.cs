using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Número de schema que este build grava e compara. É a quantidade de migrations conhecidas,
/// na ordem do identificador. O histórico só vira esse número quando é um prefixo contíguo.
/// Um furo ou um id desconhecido não é o schema atual: o número de um app mais novo vem do
/// cabeçalho autenticado do espelho, não de um recálculo local.
/// Histórico vazio devolve 0, o mesmo caso que a classificação chama de
/// <see cref="SituacaoBancoVivo.Vazio"/> quando também não há tabelas. Esse 0 não é schema publicável.
/// </summary>
public sealed class VersaoSchemaAplicacao : IVersaoSchemaApp
{
    private readonly IReadOnlyList<string> _migrations;

    public VersaoSchemaAplicacao(IReadOnlyList<string> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        if (migrations.Count == 0)
            throw new ArgumentException("O app precisa conhecer ao menos uma migration.", nameof(migrations));

        _migrations = migrations.ToArray();
    }

    public static VersaoSchemaAplicacao DoApp { get; } = CriarDoAssembly();

    public int Atual => _migrations.Count;

    public string UltimaMigration => _migrations[^1];

    public IReadOnlyList<string> Migrations => _migrations;

    /// <summary>
    /// 0 se o histórico está vazio (SQLite sem migrations: situação <see cref="SituacaoBancoVivo.Vazio"/>
    /// quando o relatório também não tem tabelas). A quantidade de migrations quando a lista é exatamente
    /// o prefixo contíguo das migrations conhecidas, na mesma ordem. Null se falta uma no meio,
    /// se a ordem não é esse prefixo, ou se há id que este app não conhece.
    /// AddCartoes pela metade não entra neste número: a classificação usa o relatório, não este cálculo.
    /// </summary>
    public int? Calcular(IReadOnlyList<string> historico)
    {
        ArgumentNullException.ThrowIfNull(historico);
        if (historico.Count == 0)
            return 0;

        if (historico.Count > _migrations.Count)
            return null;

        for (var i = 0; i < historico.Count; i++)
        {
            if (!string.Equals(historico[i], _migrations[i], StringComparison.Ordinal))
                return null;
        }

        return historico.Count;
    }

    private static VersaoSchemaAplicacao CriarDoAssembly()
    {
        var ids = typeof(AppDbContext).Assembly.GetTypes()
            .Where(tipo => tipo.IsSubclassOf(typeof(Migration)) && !tipo.IsAbstract)
            .Select(tipo => tipo.GetCustomAttribute<MigrationAttribute>()?.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new VersaoSchemaAplicacao(ids);
    }
}
