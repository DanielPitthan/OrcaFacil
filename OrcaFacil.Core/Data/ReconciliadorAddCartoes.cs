using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OrcaFacil.Core.Data;

/// <summary>
/// A migration AddCartoes reconstrói Transacoes e ComprasCesta no SQLite.
/// O <c>PRAGMA foreign_keys</c> não cabe numa transação, então o EF confirma o
/// que já rodou antes de cada pragma. Se o processo morre no meio, a tentativa
/// seguinte conclui a partir do que ficou. O DROP e o RENAME de cada tabela
/// entram na mesma transação: morrer entre os dois desfaz a troca. Uma cópia
/// ef_temp só é apagada quando o conteúdo prova que ela é redundante.
/// </summary>
public static class ReconciliadorAddCartoes
{
    public const string MigrationId = "20261015194450_AddCartoes";

    internal const string AvisoArquivoPreservado =
        "O arquivo do banco não foi substituído. Se uma tabela já tinha sido removida, a cópia ef_temp foi mantida.";

    private static readonly AsyncLocal<Func<string, CancellationToken, ValueTask>?> GanchoDepoisDoDrop = new();

    /// <summary>
    /// Teste: corre depois do DROP e antes do RENAME, ainda dentro da transação.
    /// Lançar daqui desfaz a troca. Não vale fora do fluxo de teste.
    /// </summary>
    internal static Func<string, CancellationToken, ValueTask>? DepoisDoDropAntesDoRename
    {
        get => GanchoDepoisDoDrop.Value;
        set => GanchoDepoisDoDrop.Value = value;
    }

    private static readonly (string Tabela, string Coluna, string Destino)[] ColunasNovas =
    [
        ("Transacoes", "CartaoId", "Cartoes"),
        ("ComprasCesta", "CartaoBeneficioId", "Cartoes"),
        ("ComprasCesta", "CartaoCreditoId", "Cartoes"),
        ("ComprasCesta", "ItemFaturaId", "ItensLancamento")
    ];

    private static readonly string[] IndicesNovos =
    [
        "IX_Transacoes_CartaoId",
        "IX_ComprasCesta_CartaoBeneficioId",
        "IX_ComprasCesta_CartaoCreditoId",
        "IX_ComprasCesta_ItemFaturaId",
        "IX_Cartoes_Nome",
        "IX_RecargasCartao_CartaoId_Data"
    ];

    private static readonly Lazy<IReadOnlyList<ComandoSql>> ComandosLazy = new(CriarComandos);

    public static async Task<ResultadoReconciliacao> ReconciliarSeParcialAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await HistoricoContemAsync(context, cancellationToken))
            return ResultadoReconciliacao.Nada;

        if (!await HaSinalDaMigrationAsync(context, cancellationToken))
            return ResultadoReconciliacao.Nada;

        var comandos = ComandosLazy.Value;
        var impedimento = await ImpedimentoAsync(context, comandos, cancellationToken);
        if (impedimento is not null)
            return Bloqueio(impedimento);

        if (!await EstaCompletaAsync(context, comandos, cancellationToken))
        {
            await ConcluirParaFrenteAsync(context, comandos, cancellationToken);
            if (!await EstaCompletaAsync(context, comandos, cancellationToken))
            {
                return Bloqueio(
                    "A migration AddCartoes parou no meio e não foi possível concluir o que faltava. " + AvisoArquivoPreservado);
            }
        }

        if (await HaVinculoQuebradoAsync(context, cancellationToken))
        {
            return Bloqueio(
                "A migration AddCartoes montou a estrutura, mas o PRAGMA foreign_key_check achou vínculo quebrado. O histórico não foi gravado. " + AvisoArquivoPreservado);
        }

        await InserirHistoricoAsync(context, cancellationToken);
        return new ResultadoReconciliacao(true, false, null);
    }

    private static ResultadoReconciliacao Bloqueio(string motivo) => new(false, true, motivo);

    private static async Task<bool> HaSinalDaMigrationAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await TabelaExisteAsync(context, "Cartoes", cancellationToken)
            || await TabelaExisteAsync(context, "RecargasCartao", cancellationToken)
            || await TabelaExisteAsync(context, "ef_temp_Transacoes", cancellationToken)
            || await TabelaExisteAsync(context, "ef_temp_ComprasCesta", cancellationToken))
            return true;

        foreach (var (tabela, coluna, _) in ColunasNovas)
        {
            if (await ColunaExisteAsync(context, tabela, coluna, cancellationToken))
                return true;
        }

        var indices = await NomesAsync(context, "SELECT name FROM sqlite_master WHERE type = 'index'", cancellationToken);
        return IndicesNovos.Any(indices.Contains);
    }

    private static async Task<string?> ImpedimentoAsync(
        AppDbContext context,
        IReadOnlyList<ComandoSql> comandos,
        CancellationToken cancellationToken)
    {
        var fks = MapaFks(comandos);
        foreach (var tabela in TabelasReconstruidas(comandos))
        {
            var temp = "ef_temp_" + tabela;
            var original = await TabelaExisteAsync(context, tabela, cancellationToken);
            var copia = await TabelaExisteAsync(context, temp, cancellationToken);
            if (!original && !copia)
            {
                return $"A migration AddCartoes parou no meio e a tabela {tabela} não está no banco nem na cópia ef_temp. {AvisoArquivoPreservado}";
            }

            if (!original || !copia)
                continue;

            var comparacao = await CompararAsync(context, tabela, temp, cancellationToken);
            if (!comparacao.Comparavel)
            {
                return $"A migration AddCartoes parou no meio da cópia de {tabela}: não deu para comparar o conteúdo das duas tabelas. Nenhuma das duas foi apagada. {AvisoArquivoPreservado}";
            }

            var reconstruida = await ReconstruidaAsync(context, tabela, fks, cancellationToken);
            var linhasCopia = await ContagemTabelaAsync(context, temp, cancellationToken);
            if (reconstruida && !comparacao.CopiaRedundante)
            {
                return $"A migration AddCartoes parou no meio da cópia de {tabela}: a tabela já reconstruída e a cópia ef_temp não conferem. A cópia não foi apagada. {AvisoArquivoPreservado}";
            }

            if (!reconstruida && linhasCopia > 0 && !comparacao.Identicas)
            {
                return $"A migration AddCartoes parou no meio da cópia de {tabela}: a tabela e a cópia ef_temp não têm o mesmo conteúdo. Nenhuma das duas foi apagada. {AvisoArquivoPreservado}";
            }
        }

        foreach (var (tabela, coluna, destino) in ColunasNovas)
        {
            if (!await TabelaExisteAsync(context, tabela, cancellationToken))
                continue;

            if (await ReconstruidaAsync(context, tabela, fks, cancellationToken))
                continue;

            if (await CopiaJaFeitaAsync(context, tabela, cancellationToken))
                continue;

            var orfaos = await ValoresSemDestinoAsync(context, tabela, coluna, destino, cancellationToken);
            if (orfaos <= 0)
                continue;

            var cartoes = await TabelaExisteAsync(context, "Cartoes", cancellationToken);
            var estadoTabela = cartoes ? "a tabela Cartoes não está pronta" : "a tabela Cartoes não existe";
            if (destino == "Cartoes")
            {
                return $"A migration AddCartoes parou no meio: a coluna {tabela}.{coluna} existe e {estadoTabela}. Há valores nessa coluna, então ela não foi removida e a migração não foi repetida, para não perder o vínculo nem falhar de novo com coluna duplicada. {AvisoArquivoPreservado}";
            }

            return $"A migration AddCartoes parou no meio: a coluna {tabela}.{coluna} tem valores e a tabela {destino} não cobre esses vínculos. A coluna não foi removida e a migração não foi repetida. {AvisoArquivoPreservado}";
        }

        return null;
    }

    private static async Task<bool> CopiaJaFeitaAsync(AppDbContext context, string tabela, CancellationToken cancellationToken)
    {
        var temp = "ef_temp_" + tabela;
        if (!await TabelaExisteAsync(context, temp, cancellationToken))
            return false;

        var linhasOriginais = await ContagemTabelaAsync(context, tabela, cancellationToken);
        var linhasCopia = await ContagemTabelaAsync(context, temp, cancellationToken);
        if (linhasOriginais == 0 && linhasCopia == 0)
            return true;

        var comparacao = await CompararAsync(context, tabela, temp, cancellationToken);
        return comparacao.Identicas;
    }

    private static async Task ConcluirParaFrenteAsync(
        AppDbContext context,
        IReadOnlyList<ComandoSql> comandos,
        CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        var fks = MapaFks(comandos);
        var jaTrocadas = new HashSet<int>();
        try
        {
            for (var i = 0; i < comandos.Count; i++)
            {
                if (jaTrocadas.Contains(i))
                    continue;

                var comando = comandos[i];
                if (comando.Tipo == TipoComando.Pragma)
                    continue;

                if (comando.Tipo == TipoComando.ApagarTabela)
                {
                    if (await DevePularAsync(conexao, comando, fks, cancellationToken))
                        continue;

                    var renomear = IndiceDoRename(comandos, comando.Tabela, i);
                    if (renomear < 0)
                        throw new InvalidOperationException($"A migration AddCartoes apaga {comando.Tabela} sem o RENAME da cópia.");

                    await TrocarNaMesmaTransacaoAsync(conexao, comando, comandos[renomear], cancellationToken);
                    jaTrocadas.Add(renomear);
                    continue;
                }

                if (comando.Tipo == TipoComando.RenomearTemporaria
                    && await DeveDescartarCopiaRedundanteAsync(conexao, comando, fks, cancellationToken))
                {
                    await DescartarCopiaAsync(conexao, comando.Tabela, cancellationToken);
                    continue;
                }

                if (await DevePularAsync(conexao, comando, fks, cancellationToken))
                    continue;

                await ExecutarAsync(conexao, null, comando.Sql, cancellationToken);
            }
        }
        finally
        {
            await ExecutarAsync(conexao, null, "PRAGMA foreign_keys = ON", cancellationToken);
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static int IndiceDoRename(IReadOnlyList<ComandoSql> comandos, string tabela, int aPartirDe)
    {
        var temp = "ef_temp_" + tabela;
        for (var i = aPartirDe + 1; i < comandos.Count; i++)
        {
            var comando = comandos[i];
            if (comando.Tipo == TipoComando.RenomearTemporaria && comando.Tabela == temp && comando.Extra == tabela)
                return i;
        }

        return -1;
    }

    private static async Task<bool> DeveDescartarCopiaRedundanteAsync(
        DbConnection conexao,
        ComandoSql renomear,
        IReadOnlyDictionary<string, string[]> fks,
        CancellationToken cancellationToken)
    {
        var temporaria = renomear.Tabela;
        var destino = renomear.Extra!;
        if (!await TabelaExisteAsync(conexao, null, temporaria, cancellationToken)
            || !await TabelaExisteAsync(conexao, null, destino, cancellationToken))
            return false;

        if (!await ReconstruidaAsync(conexao, destino, fks, cancellationToken))
            return false;

        var comparacao = await CompararAsync(conexao, destino, temporaria, cancellationToken);
        if (!comparacao.CopiaRedundante)
            throw new InvalidOperationException(
                $"A cópia {temporaria} e a tabela {destino} não conferem. O RENAME não rodou e a cópia não foi apagada.");

        return true;
    }

    private static async Task DescartarCopiaAsync(DbConnection conexao, string temporaria, CancellationToken cancellationToken)
    {
        ExigirIdentificador(temporaria);
        await ExecutarAsync(conexao, null, "PRAGMA foreign_keys = OFF", cancellationToken);
        try
        {
            await ExecutarAsync(conexao, null, $"DROP TABLE \"{temporaria}\"", cancellationToken);
        }
        finally
        {
            await ExecutarAsync(conexao, null, "PRAGMA foreign_keys = ON", cancellationToken);
        }
    }

    private static async Task TrocarNaMesmaTransacaoAsync(
        DbConnection conexao,
        ComandoSql drop,
        ComandoSql renomear,
        CancellationToken cancellationToken)
    {
        await ExecutarAsync(conexao, null, "PRAGMA foreign_keys = OFF", cancellationToken);
        try
        {
            var transacao = await conexao.BeginTransactionAsync(cancellationToken);
            try
            {
                await ExecutarAsync(conexao, transacao, drop.Sql, cancellationToken);
                var gancho = DepoisDoDropAntesDoRename;
                if (gancho is not null)
                    await gancho(drop.Tabela, cancellationToken);

                await ExecutarAsync(conexao, transacao, renomear.Sql, cancellationToken);
                await transacao.CommitAsync(cancellationToken);
            }
            catch
            {
                try
                {
                    await transacao.RollbackAsync(cancellationToken);
                }
                catch (Exception)
                {
                    // A transação já pode ter sido desfeita pelo SQLite.
                }

                throw;
            }
            finally
            {
                await transacao.DisposeAsync();
            }
        }
        finally
        {
            await ExecutarAsync(conexao, null, "PRAGMA foreign_keys = ON", cancellationToken);
        }
    }

    private static async Task<bool> DevePularAsync(
        DbConnection conexao,
        ComandoSql comando,
        IReadOnlyDictionary<string, string[]> fks,
        CancellationToken cancellationToken)
    {
        switch (comando.Tipo)
        {
            case TipoComando.AdicionarColuna:
                if (!await TabelaExisteAsync(conexao, null, comando.Tabela, cancellationToken))
                    return true;

                return await ColunaExisteAsync(conexao, null, comando.Tabela, comando.Extra!, cancellationToken);

            case TipoComando.CriarTabela:
                if (await TabelaExisteAsync(conexao, null, comando.Tabela, cancellationToken))
                    return true;

                if (comando.Tabela.StartsWith("ef_temp_", StringComparison.Ordinal))
                {
                    var original = comando.Tabela["ef_temp_".Length..];
                    if (await ReconstruidaAsync(conexao, original, fks, cancellationToken))
                        return true;
                }

                return false;

            case TipoComando.CriarIndice:
                if (await IndiceExisteAsync(conexao, comando.Extra!, cancellationToken))
                    return true;

                return !await TabelaExisteAsync(conexao, null, comando.Tabela, cancellationToken);

            case TipoComando.CopiarParaTemporaria:
            {
                if (!await TabelaExisteAsync(conexao, null, comando.Tabela, cancellationToken)
                    || !await TabelaExisteAsync(conexao, null, comando.Extra!, cancellationToken))
                    return true;

                var naCopia = await ContagemAsync(conexao, null, comando.Tabela, cancellationToken);
                var naOrigem = await ContagemAsync(conexao, null, comando.Extra!, cancellationToken);
                if (naCopia == 0 && naOrigem == 0)
                    return true;

                var original = comando.Extra!;
                if (await ReconstruidaAsync(conexao, original, fks, cancellationToken))
                {
                    if (naCopia == 0)
                        return true;

                    var copiaDaReconstruida = await CompararAsync(conexao, original, comando.Tabela, cancellationToken);
                    if (copiaDaReconstruida.CopiaRedundante)
                        return true;

                    throw new InvalidOperationException(
                        $"A cópia {comando.Tabela} e a tabela {original} não conferem. A cópia não foi refeita nem apagada.");
                }

                if (naCopia == 0)
                    return false;

                var jaCopiada = await CompararAsync(conexao, original, comando.Tabela, cancellationToken);
                if (jaCopiada.Identicas)
                    return true;

                throw new InvalidOperationException(
                    $"A cópia {comando.Tabela} e {original} não têm o mesmo conteúdo. A cópia não foi refeita.");
            }

            case TipoComando.ApagarTabela:
            {
                if (!await TabelaExisteAsync(conexao, null, comando.Tabela, cancellationToken))
                    return true;

                if (await ReconstruidaAsync(conexao, comando.Tabela, fks, cancellationToken))
                    return true;

                var temp = "ef_temp_" + comando.Tabela;
                if (!await TabelaExisteAsync(conexao, null, temp, cancellationToken))
                    throw new InvalidOperationException(
                        $"A tabela {comando.Tabela} seria apagada sem a cópia ef_temp. O DROP não rodou.");

                var podeApagar = await CompararAsync(conexao, comando.Tabela, temp, cancellationToken);
                if (!podeApagar.Identicas)
                    throw new InvalidOperationException(
                        $"A tabela {comando.Tabela} e a cópia ef_temp não têm o mesmo conteúdo. O DROP não rodou.");

                return false;
            }

            case TipoComando.RenomearTemporaria:
                var temporaria = comando.Tabela;
                var destino = comando.Extra!;
                var temTemporaria = await TabelaExisteAsync(conexao, null, temporaria, cancellationToken);
                var temDestino = await TabelaExisteAsync(conexao, null, destino, cancellationToken);
                if (!temTemporaria && temDestino)
                    return true;

                if (temTemporaria && temDestino)
                    throw new InvalidOperationException(
                        $"A cópia {temporaria} e a tabela {destino} existem ao mesmo tempo. O RENAME não rodou.");

                if (!temTemporaria)
                    throw new InvalidOperationException(
                        $"A cópia {temporaria} não existe para restaurar {destino}. O RENAME não rodou.");

                return false;

            default:
                return false;
        }
    }

    private static async Task<bool> EstaCompletaAsync(
        AppDbContext context,
        IReadOnlyList<ComandoSql> comandos,
        CancellationToken cancellationToken)
    {
        if (await TabelaExisteAsync(context, "ef_temp_Transacoes", cancellationToken)
            || await TabelaExisteAsync(context, "ef_temp_ComprasCesta", cancellationToken))
            return false;

        foreach (var comando in comandos)
        {
            switch (comando.Tipo)
            {
                case TipoComando.AdicionarColuna:
                    if (!await ColunaExisteAsync(context, comando.Tabela, comando.Extra!, cancellationToken))
                        return false;
                    break;
                case TipoComando.CriarTabela when !comando.Tabela.StartsWith("ef_temp_", StringComparison.Ordinal):
                    if (!await TabelaExisteAsync(context, comando.Tabela, cancellationToken))
                        return false;
                    break;
                case TipoComando.CriarIndice:
                    if (!await IndiceExisteAsync(context, comando.Extra!, cancellationToken))
                        return false;
                    break;
            }
        }

        var fks = MapaFks(comandos);
        foreach (var tabela in fks.Keys)
        {
            if (!await ReconstruidaAsync(context, tabela, fks, cancellationToken))
                return false;
        }

        return fks.Count > 0;
    }

    private static async Task<bool> ReconstruidaAsync(
        AppDbContext context,
        string tabela,
        IReadOnlyDictionary<string, string[]> fks,
        CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            return await ReconstruidaAsync(conexao, tabela, fks, cancellationToken);
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<bool> ReconstruidaAsync(
        DbConnection conexao,
        string tabela,
        IReadOnlyDictionary<string, string[]> fks,
        CancellationToken cancellationToken)
    {
        if (!fks.TryGetValue(tabela, out var esperadas) || esperadas is not { Length: > 0 })
            return false;

        var sql = await LerEscalarAsync(
            conexao,
            null,
            "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $n",
            cancellationToken,
            ("$n", tabela));
        return sql is not null && esperadas.All(fk => sql.Contains(fk, StringComparison.Ordinal));
    }

    private static Dictionary<string, string[]> MapaFks(IReadOnlyList<ComandoSql> comandos)
    {
        var mapa = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var comando in comandos)
        {
            if (comando.Tipo != TipoComando.CriarTabela || !comando.Tabela.StartsWith("ef_temp_", StringComparison.Ordinal))
                continue;

            var original = comando.Tabela["ef_temp_".Length..];
            mapa[original] = ExtrairFks(comando.Sql);
        }

        return mapa;
    }

    private static string[] ExtrairFks(string sql)
    {
        var nomes = new List<string>();
        const string marca = "CONSTRAINT \"";
        var indice = 0;
        while (true)
        {
            var pos = sql.IndexOf(marca, indice, StringComparison.Ordinal);
            if (pos < 0)
                break;

            var nome = LerCitado(sql, pos + "CONSTRAINT ".Length);
            var inicioCorpo = pos + marca.Length + nome.Length;
            var proximo = sql.IndexOf(marca, inicioCorpo, StringComparison.Ordinal);
            var corpo = proximo < 0 ? sql[inicioCorpo..] : sql[inicioCorpo..proximo];
            if (corpo.Contains("FOREIGN KEY", StringComparison.Ordinal))
                nomes.Add(nome);

            indice = inicioCorpo;
        }

        return nomes.ToArray();
    }

    private readonly record struct ComparacaoTabelas(bool Comparavel, bool Identicas, bool CopiaRedundante);

    private static async Task<ComparacaoTabelas> CompararAsync(
        AppDbContext context,
        string original,
        string copia,
        CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            return await CompararAsync(conexao, original, copia, cancellationToken);
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<ComparacaoTabelas> CompararAsync(
        DbConnection conexao,
        string original,
        string copia,
        CancellationToken cancellationToken)
    {
        var colunasOriginais = await ColunasAsync(conexao, original, cancellationToken);
        var colunasCopia = await ColunasAsync(conexao, copia, cancellationToken);
        if (!colunasOriginais.Contains("Id") || !colunasCopia.Contains("Id"))
            return new ComparacaoTabelas(false, false, false);

        var comuns = colunasOriginais.Intersect(colunasCopia, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var soNaCopia = colunasCopia.Except(colunasOriginais, StringComparer.Ordinal).ToList();
        var soNaOriginal = colunasOriginais.Except(colunasCopia, StringComparer.Ordinal).ToList();
        var divergentes = await ContarDivergentesAsync(conexao, original, copia, comuns, cancellationToken);
        var idsSoNaCopia = await ContarIdsSemParAsync(conexao, copia, original, cancellationToken);
        var idsSoNaOriginal = await ContarIdsSemParAsync(conexao, original, copia, cancellationToken);
        var extraComValor = await ColunaExtraComValorAsync(conexao, copia, soNaCopia, cancellationToken);
        var identicas = soNaCopia.Count == 0
            && soNaOriginal.Count == 0
            && divergentes == 0
            && idsSoNaCopia == 0
            && idsSoNaOriginal == 0
            && await ContagemAsync(conexao, null, original, cancellationToken) == await ContagemAsync(conexao, null, copia, cancellationToken);
        var redundante = idsSoNaCopia == 0 && divergentes == 0 && !extraComValor;
        return new ComparacaoTabelas(true, identicas, redundante);
    }

    private static async Task<List<string>> ColunasAsync(DbConnection conexao, string tabela, CancellationToken cancellationToken)
    {
        var colunas = new List<string>();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT name FROM pragma_table_info($t) ORDER BY name";
        Adicionar(comando, "$t", tabela);
        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
            colunas.Add(leitor.GetString(0));

        return colunas;
    }

    private static async Task<long> ContarDivergentesAsync(
        DbConnection conexao,
        string original,
        string copia,
        IReadOnlyList<string> colunas,
        CancellationToken cancellationToken)
    {
        foreach (var coluna in colunas)
            ExigirIdentificador(coluna);

        ExigirIdentificador(original);
        ExigirIdentificador(copia);
        var iguais = string.Join(" AND ", colunas.Select(coluna => $"o.\"{coluna}\" IS c.\"{coluna}\""));
        var sql = $"""
            SELECT COUNT(*)
            FROM "{copia}" AS c
            INNER JOIN "{original}" AS o ON c."Id" = o."Id"
            WHERE NOT ({iguais})
            """;
        var texto = await LerEscalarAsync(conexao, null, sql, cancellationToken);
        return long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor : 0;
    }

    private static async Task<long> ContarIdsSemParAsync(
        DbConnection conexao,
        string origem,
        string destino,
        CancellationToken cancellationToken)
    {
        ExigirIdentificador(origem);
        ExigirIdentificador(destino);
        var texto = await LerEscalarAsync(
            conexao,
            null,
            $"SELECT COUNT(*) FROM \"{origem}\" WHERE \"Id\" NOT IN (SELECT \"Id\" FROM \"{destino}\")",
            cancellationToken);
        return long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor : 0;
    }

    private static async Task<bool> ColunaExtraComValorAsync(
        DbConnection conexao,
        string tabela,
        IReadOnlyList<string> colunas,
        CancellationToken cancellationToken)
    {
        ExigirIdentificador(tabela);
        foreach (var coluna in colunas)
        {
            ExigirIdentificador(coluna);
            var texto = await LerEscalarAsync(
                conexao,
                null,
                $"SELECT COUNT(*) FROM \"{tabela}\" WHERE \"{coluna}\" IS NOT NULL",
                cancellationToken);
            if (long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) && valor > 0)
                return true;
        }

        return false;
    }

    private static async Task<bool> HaVinculoQuebradoAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var total = await EscalarAsync(context, "SELECT COUNT(*) FROM pragma_foreign_key_check", cancellationToken);
        return total > 0;
    }

    private static IEnumerable<string> TabelasReconstruidas(IReadOnlyList<ComandoSql> comandos) =>
        comandos.Where(comando => comando.Tipo == TipoComando.ApagarTabela).Select(comando => comando.Tabela).Distinct(StringComparer.Ordinal);

    private static IReadOnlyList<ComandoSql> CriarComandos()
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var contexto = new AppDbContext(opcoes);
        var assembly = contexto.GetService<IMigrationsAssembly>();
        var gerador = contexto.GetService<IMigrationsSqlGenerator>();
        var inicializador = contexto.GetService<IModelRuntimeInitializer>();
        var migration = assembly.CreateMigration(assembly.Migrations[MigrationId], "Microsoft.EntityFrameworkCore.Sqlite");
        var modelo = inicializador.Initialize(migration.TargetModel, designTime: true);
        var comandos = gerador.Generate(migration.UpOperations, modelo);
        return comandos.Select(comando => Classificar(comando.CommandText)).ToList();
    }

    private static ComandoSql Classificar(string sql)
    {
        var texto = sql.Trim();
        if (texto.StartsWith("PRAGMA ", StringComparison.Ordinal))
            return new ComandoSql(texto, TipoComando.Pragma, "", null);

        if (texto.StartsWith("ALTER TABLE \"", StringComparison.Ordinal))
        {
            var tabela = LerCitado(texto, "ALTER TABLE \"".Length - 1);
            var adicionar = texto.IndexOf(" ADD \"", StringComparison.Ordinal);
            if (adicionar >= 0)
            {
                var coluna = LerCitado(texto, adicionar + " ADD ".Length);
                ExigirIdentificador(tabela);
                ExigirIdentificador(coluna);
                return new ComandoSql(texto, TipoComando.AdicionarColuna, tabela, coluna);
            }

            var renomear = texto.IndexOf(" RENAME TO \"", StringComparison.Ordinal);
            if (renomear >= 0)
            {
                var destino = LerCitado(texto, renomear + " RENAME TO ".Length);
                ExigirIdentificador(tabela);
                ExigirIdentificador(destino);
                return new ComandoSql(texto, TipoComando.RenomearTemporaria, tabela, destino);
            }
        }

        if (texto.StartsWith("DROP TABLE \"", StringComparison.Ordinal))
        {
            var tabela = LerCitado(texto, "DROP TABLE \"".Length - 1);
            ExigirIdentificador(tabela);
            return new ComandoSql(texto, TipoComando.ApagarTabela, tabela, null);
        }

        if (texto.StartsWith("CREATE TABLE \"", StringComparison.Ordinal))
        {
            var tabela = LerCitado(texto, "CREATE TABLE \"".Length - 1);
            ExigirIdentificador(tabela);
            return new ComandoSql(texto, TipoComando.CriarTabela, tabela, null);
        }

        if (texto.StartsWith("CREATE UNIQUE INDEX \"", StringComparison.Ordinal)
            || texto.StartsWith("CREATE INDEX \"", StringComparison.Ordinal))
        {
            var aspas = texto.IndexOf('"', StringComparison.Ordinal);
            var indice = LerCitado(texto, aspas);
            var on = texto.IndexOf(" ON \"", StringComparison.Ordinal);
            if (on < 0)
                throw new InvalidOperationException("CREATE INDEX sem tabela em AddCartoes.");

            var tabela = LerCitado(texto, on + " ON ".Length);
            ExigirIdentificador(indice);
            ExigirIdentificador(tabela);
            return new ComandoSql(texto, TipoComando.CriarIndice, tabela, indice);
        }

        if (texto.StartsWith("INSERT INTO \"", StringComparison.Ordinal))
        {
            var destino = LerCitado(texto, "INSERT INTO \"".Length - 1);
            var from = texto.LastIndexOf("FROM \"", StringComparison.Ordinal);
            if (from < 0)
                throw new InvalidOperationException("INSERT sem origem em AddCartoes.");

            var origem = LerCitado(texto, from + "FROM ".Length);
            ExigirIdentificador(destino);
            ExigirIdentificador(origem);
            return new ComandoSql(texto, TipoComando.CopiarParaTemporaria, destino, origem);
        }

        throw new InvalidOperationException("Comando inesperado da migration AddCartoes: " + PrimeiraLinha(texto));
    }

    private static string LerCitado(string texto, int indiceAspas)
    {
        if (indiceAspas < 0 || indiceAspas >= texto.Length || texto[indiceAspas] != '"')
            throw new InvalidOperationException("Identificador ausente na migration AddCartoes.");

        var fim = texto.IndexOf('"', indiceAspas + 1);
        if (fim < 0)
            throw new InvalidOperationException("Identificador sem fechamento na migration AddCartoes.");

        return texto[(indiceAspas + 1)..fim];
    }

    private static void ExigirIdentificador(string nome)
    {
        if (nome.Length == 0 || nome.Any(caractere => !char.IsAsciiLetterOrDigit(caractere) && caractere != '_'))
            throw new InvalidOperationException("Identificador recusado na migration AddCartoes.");
    }

    private static string PrimeiraLinha(string texto)
    {
        var quebra = texto.IndexOf('\n', StringComparison.Ordinal);
        var linha = (quebra < 0 ? texto : texto[..quebra]).Trim();
        return linha.Length <= 80 ? linha : linha[..80];
    }

    private static string VersaoDoEf()
    {
        var informacional = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informacional))
        {
            var mais = informacional.IndexOf('+', StringComparison.Ordinal);
            var versao = mais >= 0 ? informacional[..mais] : informacional;
            if (!string.IsNullOrWhiteSpace(versao))
                return versao;
        }

        var numero = typeof(DbContext).Assembly.GetName().Version;
        if (numero is not null)
            return $"{numero.Major}.{numero.Minor}.{numero.Build}";

        throw new InvalidOperationException("Não foi possível ler a versão do EF para o histórico.");
    }

    private static async Task InserirHistoricoAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var versao = await LerEscalarAsync(
            context,
            "SELECT ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1",
            cancellationToken);
        if (string.IsNullOrWhiteSpace(versao))
            versao = VersaoDoEf();

        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            await using var comando = conexao.CreateCommand();
            comando.CommandText = """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ($id, $versao)
                """;
            var id = comando.CreateParameter();
            id.ParameterName = "$id";
            id.Value = MigrationId;
            comando.Parameters.Add(id);
            var produto = comando.CreateParameter();
            produto.ParameterName = "$versao";
            produto.Value = versao;
            comando.Parameters.Add(produto);
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<bool> HistoricoContemAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var total = await EscalarAsync(
            context,
            "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = $id",
            cancellationToken,
            ("$id", MigrationId));
        return total > 0;
    }

    private static async Task<long> ValoresSemDestinoAsync(
        AppDbContext context,
        string tabela,
        string coluna,
        string destino,
        CancellationToken cancellationToken)
    {
        ExigirIdentificador(tabela);
        ExigirIdentificador(coluna);
        ExigirIdentificador(destino);
        if (!await TabelaExisteAsync(context, destino, cancellationToken))
            return await ContagemTabelaAsync(context, tabela, cancellationToken, $"\"{coluna}\" IS NOT NULL");

        return await EscalarAsync(
            context,
            $"SELECT COUNT(*) FROM \"{tabela}\" WHERE \"{coluna}\" IS NOT NULL AND \"{coluna}\" NOT IN (SELECT \"Id\" FROM \"{destino}\")",
            cancellationToken);
    }

    private static Task<long> ContagemTabelaAsync(AppDbContext context, string tabela, CancellationToken cancellationToken, string? filtro = null)
    {
        ExigirIdentificador(tabela);
        var sql = filtro is null
            ? $"SELECT COUNT(*) FROM \"{tabela}\""
            : $"SELECT COUNT(*) FROM \"{tabela}\" WHERE {filtro}";
        return EscalarAsync(context, sql, cancellationToken);
    }

    private static async Task<bool> TabelaExisteAsync(AppDbContext context, string tabela, CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            return await TabelaExisteAsync(conexao, null, tabela, cancellationToken);
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<bool> TabelaExisteAsync(DbConnection conexao, DbTransaction? transacao, string tabela, CancellationToken cancellationToken)
    {
        var valor = await LerEscalarAsync(
            conexao,
            transacao,
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $n",
            cancellationToken,
            ("$n", tabela));
        return valor is not null;
    }

    private static async Task<bool> IndiceExisteAsync(AppDbContext context, string indice, CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            return await IndiceExisteAsync(conexao, indice, cancellationToken);
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<bool> IndiceExisteAsync(DbConnection conexao, string indice, CancellationToken cancellationToken)
    {
        var valor = await LerEscalarAsync(
            conexao,
            null,
            "SELECT 1 FROM sqlite_master WHERE type = 'index' AND name = $n",
            cancellationToken,
            ("$n", indice));
        return valor is not null;
    }

    private static async Task<bool> ColunaExisteAsync(AppDbContext context, string tabela, string coluna, CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            return await ColunaExisteAsync(conexao, null, tabela, coluna, cancellationToken);
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<bool> ColunaExisteAsync(
        DbConnection conexao,
        DbTransaction? transacao,
        string tabela,
        string coluna,
        CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = "SELECT 1 FROM pragma_table_info($tabela) WHERE name = $coluna";
        Adicionar(comando, "$tabela", tabela);
        Adicionar(comando, "$coluna", coluna);
        var valor = await comando.ExecuteScalarAsync(cancellationToken);
        return valor is not null and not DBNull;
    }

    private static async Task<HashSet<string>> NomesAsync(AppDbContext context, string sql, CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            var nomes = new HashSet<string>(StringComparer.Ordinal);
            await using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
            while (await leitor.ReadAsync(cancellationToken))
                nomes.Add(leitor.GetString(0));
            return nomes;
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<long> ContagemAsync(DbConnection conexao, DbTransaction? transacao, string tabela, CancellationToken cancellationToken)
    {
        ExigirIdentificador(tabela);
        var texto = await LerEscalarAsync(conexao, transacao, $"SELECT COUNT(*) FROM \"{tabela}\"", cancellationToken);
        return long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor : 0;
    }

    private static async Task ExecutarAsync(DbConnection conexao, DbTransaction? transacao, string sql, CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> EscalarAsync(
        AppDbContext context,
        string sql,
        CancellationToken cancellationToken,
        params (string Nome, string Valor)[] parametros)
    {
        var texto = await LerEscalarAsync(context, sql, cancellationToken, parametros);
        return long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor : 0;
    }

    private static async Task<string?> LerEscalarAsync(
        AppDbContext context,
        string sql,
        CancellationToken cancellationToken,
        params (string Nome, string Valor)[] parametros)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            return await LerEscalarAsync(conexao, null, sql, cancellationToken, parametros);
        }
        catch (DbException)
        {
            return null;
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<string?> LerEscalarAsync(
        DbConnection conexao,
        DbTransaction? transacao,
        string sql,
        CancellationToken cancellationToken,
        params (string Nome, string Valor)[] parametros)
    {
        await using var comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = sql;
        foreach (var (nome, valor) in parametros)
            Adicionar(comando, nome, valor);

        var resultado = await comando.ExecuteScalarAsync(cancellationToken);
        return resultado is null or DBNull ? null : Convert.ToString(resultado, CultureInfo.InvariantCulture);
    }

    private static void Adicionar(DbCommand comando, string nome, string valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }

    private enum TipoComando
    {
        AdicionarColuna,
        CriarTabela,
        CriarIndice,
        CopiarParaTemporaria,
        Pragma,
        ApagarTabela,
        RenomearTemporaria
    }

    private readonly record struct ComandoSql(string Sql, TipoComando Tipo, string Tabela, string? Extra);
}

public readonly record struct ResultadoReconciliacao(bool Alterou, bool Bloqueado, string? Motivo)
{
    public static ResultadoReconciliacao Nada { get; } = new(false, false, null);
}
