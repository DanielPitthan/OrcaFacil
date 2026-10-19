using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Core.Exportacao;

/// <summary>
/// Preferências que podem ir para a planilha. Sem chave de API, sem URI de pasta e sem senha.
/// O Core não lê o armazenamento do aparelho: quem chama preenche só estes campos.
/// </summary>
public sealed class ConfiguracaoExportavel
{
    public TemaPreferido Tema { get; init; } = TemaPreferido.Automatico;

    public bool NotificacoesAtivas { get; init; }

    public bool SomAtivo { get; init; }

    public PrazoAviso PrazosAviso { get; init; }

    public string? Provedor { get; init; }

    public string? Url { get; init; }

    public string? Modelo { get; init; }
}
