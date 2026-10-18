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
/// Ordem: ausente; leitura que lançou; relatório de arquivo ilegível;
/// íntegro; migration desconhecida; SQLite vazio (sem tabelas e sem histórico);
/// AddCartoes pela metade (vai para o reconciliador); outra migration pela metade
/// ou histórico com objeto faltando; migrations conhecidas ainda pendentes num
/// schema que já existe; qualquer outra divergência.
/// Arquivo ilegível nunca é <see cref="SituacaoBancoVivo.Desatualizado"/> nem
/// <see cref="SituacaoBancoVivo.Vazio"/>.
/// <see cref="SituacaoBancoVivo.SemPerfil"/> não sai da estrutura: o coordenador aplica
/// <see cref="PoliticaSeed"/> quando o relatório já é íntegro e não há membro.
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
        if (MarcaArquivoIlegivel(relatorio))
            return SituacaoBancoVivo.Inconsistente;

        if (relatorio.Integro)
            return SituacaoBancoVivo.Integro;

        if (relatorio.MigrationsDesconhecidasNoHistorico.Count > 0)
            return SituacaoBancoVivo.SchemaMaisNovoQueOApp;

        if (EhVazio(relatorio))
            return SituacaoBancoVivo.Vazio;

        if (relatorio.MigrationsPelaMetade.Contains(ReconciliadorAddCartoes.MigrationId))
            return SituacaoBancoVivo.ParcialAddCartoes;

        if (relatorio.MigrationsPelaMetade.Count > 0
            || relatorio.MigrationsNoHistoricoComObjetoFaltando.Count > 0)
            return SituacaoBancoVivo.Inconsistente;

        if (relatorio.MigrationsAusentesNoHistorico.Count > 0)
            return SituacaoBancoVivo.Desatualizado;

        return SituacaoBancoVivo.Inconsistente;
    }

    /// <summary>
    /// Schema íntegro sem membro vira <see cref="SituacaoBancoVivo.SemPerfil"/>.
    /// As outras situações ficam como estão: banco vazio, inconsistente ou mais novo não é promovido.
    /// </summary>
    public static SituacaoBancoVivo AplicarPoliticaSeed(
        SituacaoBancoVivo situacao,
        RelatorioIntegridadeBanco? relatorio,
        bool haMembro)
    {
        if (situacao != SituacaoBancoVivo.Integro || relatorio is null)
            return situacao;

        return PoliticaSeed.DevePopular(new ResultadoPreparoBanco(null, relatorio), haMembro)
            ? SituacaoBancoVivo.SemPerfil
            : situacao;
    }

    /// <summary>
    /// Relatório do <see cref="VerificadorIntegridadeBanco.RelatorioIlegivel"/>
    /// ou o marcador de <see cref="RelatorioIntegridadeBanco.IlegivelMinimo"/>.
    /// </summary>
    public static bool MarcaArquivoIlegivel(RelatorioIntegridadeBanco relatorio)
    {
        ArgumentNullException.ThrowIfNull(relatorio);
        if (relatorio.ArquivoIlegivel)
            return true;

        return TemMarcador(relatorio.TabelasFaltando)
            || TemMarcador(relatorio.ColunasFaltando)
            || TemMarcador(relatorio.IndicesFaltando)
            || TemMarcador(relatorio.MigrationsAusentesNoHistorico);
    }

    private static bool EhVazio(RelatorioIntegridadeBanco relatorio) =>
        relatorio.TabelasExistentes.Count == 0
        && relatorio.MigrationsNoHistorico.Count == 0
        && relatorio.MigrationsDesconhecidasNoHistorico.Count == 0
        && relatorio.MigrationsPelaMetade.Count == 0
        && relatorio.MigrationsNoHistoricoComObjetoFaltando.Count == 0;

    private static bool TemMarcador(IReadOnlyList<string> itens)
    {
        foreach (var item in itens)
        {
            if (item.Contains("ilegível", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
