using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// O que foi lido do banco interno, sem reinterpretar o SQLite.
/// O relatório vem de <see cref="VerificadorIntegridadeBanco"/>.
/// </summary>
public sealed record LeituraBancoVivo
{
    public LeituraBancoVivo(bool existe, RelatorioIntegridadeBanco? relatorio, bool leituraFalhou)
    {
        if (!existe)
        {
            if (relatorio is not null || leituraFalhou)
                throw new ArgumentException("Banco ausente não tem relatório nem falha de leitura.");
        }
        else if (leituraFalhou)
        {
            if (relatorio is not null)
                throw new ArgumentException("Leitura que falhou não traz relatório.");
        }
        else if (relatorio is null)
        {
            throw new ArgumentException("Banco existente exige o relatório do VerificadorIntegridadeBanco.");
        }

        Existe = existe;
        Relatorio = relatorio;
        LeituraFalhou = leituraFalhou;
    }

    public bool Existe { get; }

    public RelatorioIntegridadeBanco? Relatorio { get; }

    public bool LeituraFalhou { get; }

    public static LeituraBancoVivo Ausente() => new(false, null, false);

    public static LeituraBancoVivo Lida(RelatorioIntegridadeBanco relatorio) => new(true, relatorio, false);

    public static LeituraBancoVivo Ilegivel() => new(true, null, true);
}

/// <summary>
/// Traduz o <see cref="RelatorioIntegridadeBanco"/> para a situação da matriz.
/// Não abre arquivo, não executa SQL, não migra e não apaga.
/// </summary>
/// <remarks>
/// Ordem, de cima para baixo:
/// ausente; leitura impossível; <see cref="RelatorioIntegridadeBanco.Integro"/>;
/// migration desconhecida no histórico (app velho demais — não tratar como
/// inconsistência restaurável, senão um espelho antigo seria copiado por cima);
/// migration pela metade ou histórico com objeto faltando;
/// só migrations conhecidas ainda fora do histórico (banco antigo coerente);
/// qualquer outra divergência.
/// Um objeto criado à mão, sem a migration no histórico, aparece como migration
/// ausente. A migração então falha e o coordenador abre a recuperação, sem semear
/// e sem apagar. Separar esse caso de um banco antigo coerente exigiria reler o
/// SQLite, e quem lê é o <see cref="VerificadorIntegridadeBanco"/>.
/// </remarks>
public static class ClassificacaoBancoVivo
{
    public static SituacaoBancoVivo Classificar(LeituraBancoVivo leitura)
    {
        ArgumentNullException.ThrowIfNull(leitura);

        if (!leitura.Existe)
            return SituacaoBancoVivo.Ausente;

        if (leitura.LeituraFalhou)
            return SituacaoBancoVivo.Inconsistente;

        var relatorio = leitura.Relatorio!;
        if (relatorio.Integro)
            return SituacaoBancoVivo.Integro;

        if (relatorio.MigrationsDesconhecidasNoHistorico.Count > 0)
            return SituacaoBancoVivo.SchemaMaisNovoQueOApp;

        if (relatorio.MigrationsPelaMetade.Count > 0
            || relatorio.MigrationsNoHistoricoComObjetoFaltando.Count > 0)
            return SituacaoBancoVivo.Inconsistente;

        if (relatorio.MigrationsAusentesNoHistorico.Count > 0)
            return SituacaoBancoVivo.Desatualizado;

        return SituacaoBancoVivo.Inconsistente;
    }
}
