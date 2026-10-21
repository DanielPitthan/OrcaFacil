using System.Globalization;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.B3;

public enum StatusLinhaImportacaoB3
{
    Nova,
    DuplicataImportada,
    PossivelDuplicataManual,
    MesFechado,
    SemPosicao,
    Informativa,
    Erro
}

public enum DestinoImportacaoB3
{
    Nenhum,
    OperacaoAtivo,
    Provento,
    RendaFixa
}

/// <summary>O que a gravação precisa para chamar o serviço do destino, sem reler o arquivo.</summary>
public sealed class ComandoImportacaoB3
{
    public required DestinoImportacaoB3 Destino { get; init; }
    public required string Hash { get; init; }
    public required DateTime Data { get; init; }
    public required string Ticker { get; init; }
    public required string Nome { get; init; }
    public required string Instituicao { get; init; }
    public required string Corretora { get; init; }
    public TipoAtivo TipoAtivo { get; init; }
    public TipoOperacao TipoOperacao { get; init; }
    public TipoProvento TipoProvento { get; init; }
    public TipoOperacaoRendaFixa TipoRendaFixa { get; init; }
    public TipoTituloRendaFixa TipoTitulo { get; init; }
    public string? Emissor { get; init; }
    public int? AnoVencimento { get; init; }
    public decimal Quantidade { get; init; }
    public decimal PrecoUnitario { get; init; }
    public decimal Valor { get; init; }
}

public sealed class LinhaPreviaExtratoB3
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public int Numero { get; init; }
    public StatusLinhaImportacaoB3 Status { get; set; }
    public bool Marcada { get; set; }
    public bool PodeMarcar { get; set; }
    public string Titulo { get; init; } = "";
    public string Subtitulo { get; set; } = "";
    public string ValorTexto { get; init; } = "";
    public decimal Valor { get; init; }
    public string Motivo { get; set; } = "";
    public string Icone { get; init; } = "📄";
    public bool EhCredito { get; init; }
    public ComandoImportacaoB3? Comando { get; init; }

    public string RotuloStatus => Status switch
    {
        StatusLinhaImportacaoB3.Nova => "Nova",
        StatusLinhaImportacaoB3.DuplicataImportada => "Já importada",
        StatusLinhaImportacaoB3.PossivelDuplicataManual => "Possível duplicata",
        StatusLinhaImportacaoB3.MesFechado => "Mês fechado",
        StatusLinhaImportacaoB3.SemPosicao => "Sem posição",
        StatusLinhaImportacaoB3.Informativa => "Informativa",
        StatusLinhaImportacaoB3.Erro => "Erro",
        _ => Status.ToString()
    };

    public static string FormatarValor(decimal? valor)
        => valor?.ToString("C", PtBr) ?? "—";
}

public sealed class PreviaExtratoB3
{
    /// <summary>
    /// Texto da tela. Continua na liquidação: a data mostrada é <c>OperacaoAtivo.Data</c>.
    /// O pregão da #104 não entra neste aviso.
    /// </summary>
    public const string Aviso =
        "A data do extrato é a da liquidação e vale como data da operação. O cálculo usa o Valor da Operação. Custos entram zerados.";

    public required IReadOnlyList<LinhaPreviaExtratoB3> Linhas { get; init; }

    public int Contar(StatusLinhaImportacaoB3 status)
        => Linhas.Count(linha => linha.Status == status);

    public int Marcadas => Linhas.Count(linha => linha.Marcada && linha.PodeMarcar);
}
