namespace OrcaFacil.Core.Data;

public interface IRegistroLog
{
    void Informacao(string mensagem, object?[] argumentos);

    void Erro(Exception? excecao, string mensagem, object?[] argumentos);
}

/// <summary>
/// Texto do log de integridade. O mesmo método vale para banco íntegro, inconsistente ou ilegível.
/// </summary>
public static class RegistroPreparoBanco
{
    public static void Registrar(ResultadoPreparoBanco resultado, IRegistroLog log)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(log);

        var relatorio = resultado.Relatorio;
        try
        {
            if (resultado.CaminhoBackup is not null)
            {
                log.Informacao(
                    "Backup consistente antes da migração, sem apagar os anteriores: {Backup}.",
                    [resultado.CaminhoBackup]);
            }

            if (!resultado.ExigeRecuperacao)
            {
                log.Informacao(
                    "Integridade do orcafacil.db ok. Tabelas: {Tabelas}. Migrations no histórico: {Historico}.",
                    [Juntar(relatorio.TabelasExistentes), Juntar(relatorio.MigrationsNoHistorico)]);
                return;
            }

            log.Erro(
                resultado.FalhaMigrate,
                "Integridade do orcafacil.db falhou. O check não alterou o arquivo e nada foi apagado. Existe: {Tabelas}. Faltam tabelas: {TabelasFaltando}. Faltam colunas: {Colunas}. Faltam índices: {Indices}. Histórico: {Historico}. Histórico sem: {Ausentes}. Histórico desconhecido: {Desconhecidas}. Migration pela metade: {PelaMetade}. Histórico com objeto faltando: {HistoricoIncompleto}. Próxima migration não registrada: {Pendente}.",
                [
                    Juntar(relatorio.TabelasExistentes),
                    Juntar(relatorio.TabelasFaltando),
                    Juntar(relatorio.ColunasFaltando),
                    Juntar(relatorio.IndicesFaltando),
                    Juntar(relatorio.MigrationsNoHistorico),
                    Juntar(relatorio.MigrationsAusentesNoHistorico),
                    Juntar(relatorio.MigrationsDesconhecidasNoHistorico),
                    Juntar(relatorio.MigrationsPelaMetade),
                    Juntar(relatorio.MigrationsNoHistoricoComObjetoFaltando),
                    relatorio.PrimeiraMigrationNaoRegistrada ?? "(nenhuma)"
                ]);
        }
        catch (Exception logEx)
        {
            System.Diagnostics.Debug.WriteLine(logEx);
        }
    }

    private static string Juntar(IReadOnlyList<string> itens) =>
        itens.Count == 0 ? "(nenhum)" : string.Join(", ", itens);
}
