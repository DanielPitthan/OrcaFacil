using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Número de schema que este build grava e compara. É a quantidade de migrations conhecidas,
/// na ordem do identificador. Uma migration desconhecida no histórico conta como schema mais novo.
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
    /// 0 se o banco não tem histórico. O índice (1-based) da última migration conhecida.
    /// Se qualquer id do histórico não pertence a este app, devolve <see cref="Atual"/> + 1.
    /// </summary>
    public int Calcular(IReadOnlyList<string> historico)
    {
        ArgumentNullException.ThrowIfNull(historico);
        if (historico.Count == 0)
            return 0;

        foreach (var id in historico)
        {
            if (!_migrations.Contains(id, StringComparer.Ordinal))
                return _migrations.Count + 1;
        }

        var indice = IndexOf(historico[^1]);
        return indice + 1;
    }

    private int IndexOf(string id)
    {
        for (var i = 0; i < _migrations.Count; i++)
        {
            if (string.Equals(_migrations[i], id, StringComparison.Ordinal))
                return i;
        }

        return -1;
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
