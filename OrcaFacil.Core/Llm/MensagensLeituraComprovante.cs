using System.Globalization;

namespace OrcaFacil.Core.Llm;

public enum SituacaoLeituraComprovante
{
    Carregando,
    SemUrlModeloOuChave,
    SemRede,
    TempoEsgotado,
    NaoAutorizado,
    SemPermissao,
    LimiteProvedor,
    RespostaForaDoSchema,
    IlegivelOuSemItens,
    ModeloSemVisao,
    PedidoRecusado,
    Falha,
    Pronta
}

/// <summary>
/// Frases da leitura na Cesta. A frase não carrega chave, corpo, imagem nem valor em reais.
/// O diagnóstico, quando existe, já vem sanitizado.
/// </summary>
public sealed record ExibicaoLeituraComprovante
{
    public required SituacaoLeituraComprovante Situacao { get; init; }
    public required string Mensagem { get; init; }
    public string? Resumo { get; init; }
    public bool LevaParaInteligenciaArtificial { get; init; }
    public bool PodeTentarDeNovo { get; init; }
    public DiagnosticoResposta? Diagnostico { get; init; }

    public override string ToString() => $"ExibicaoLeitura({Situacao})";
}

/// <summary>Uma aba de Configurações. A ordem da tabela é a ordem na tela; o atalho usa a consulta, não um índice fixo.</summary>
public readonly record struct AbaConfiguracao(string Consulta, string Titulo);

/// <summary>
/// Traduz o resultado da chamada e o comprovante normalizado para a frase da tela.
/// O comprovante em si fica na memória da página. Esta etapa não grava compra.
/// </summary>
public static class MensagensLeituraComprovante
{
    public const string TextoCarregando = "Lendo o comprovante…";

    public const string TextoSemUrlModeloOuChave =
        "Falta a URL, o modelo ou a chave. Abra a aba Inteligência artificial.";

    public const string TextoIlegivelOuSemItens =
        "O comprovante está ilegível ou não tem itens.";

    public const string TextoPreviaASeguir = "A prévia editável vem a seguir.";

    public const string TextoAvisoPrivacidade =
        "Aviso, seus dados serão enviados para um provedor externo.";

    public const string TextoFotoInvalida = "Não foi possível ler essa foto.";

    public const string TextoFotoGrandeDemais = "A foto ficou grande demais. Escolha outra.";

    public const string TextoFormatoNaoConvertido =
        "Não foi possível converter essa foto para JPEG. A leitura não foi enviada.";

    public static string TextoFotoAcimaDoTeto =>
        $"A foto passa de {PreparoFotoComprovante.TetoBytesAntesDoDecode / (1024 * 1024)} MB. A leitura não foi enviada.";

    public const string ConsultaAba = "ia";

    public const string RotaInteligencia = "/configuracoes?aba=ia";

    private static readonly AbaConfiguracao[] LinhasAbas =
    [
        new("notificacoes", "Notificações"),
        new("armazenamento", "Armazenamento"),
        new(ConsultaAba, "Inteligência artificial")
    ];

    public static IReadOnlyList<AbaConfiguracao> Abas => LinhasAbas;

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Posição da consulta na tabela informada. A mesma consulta muda de lugar se a ordem mudar.
    /// Consulta ausente ou desconhecida devolve -1.
    /// </summary>
    public static int IndiceNaTabela(IReadOnlyList<AbaConfiguracao> abas, string? consulta)
    {
        ArgumentNullException.ThrowIfNull(abas);
        if (string.IsNullOrWhiteSpace(consulta))
            return -1;

        for (var i = 0; i < abas.Count; i++)
        {
            if (string.Equals(abas[i].Consulta, consulta, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Índice da aba de inteligência artificial quando a consulta é <see cref="ConsultaAba"/>.
    /// Outra consulta não troca de aba.
    /// </summary>
    public static int? IndiceDoAtalho(string? consulta) =>
        IndiceDoAtalho(LinhasAbas, consulta);

    public static int? IndiceDoAtalho(IReadOnlyList<AbaConfiguracao> abas, string? consulta)
    {
        var indice = IndiceNaTabela(abas, consulta);
        if (indice < 0)
            return null;
        if (!string.Equals(abas[indice].Consulta, ConsultaAba, StringComparison.OrdinalIgnoreCase))
            return null;

        return indice;
    }

    public static ExibicaoLeituraComprovante Carregando() =>
        new()
        {
            Situacao = SituacaoLeituraComprovante.Carregando,
            Mensagem = TextoCarregando
        };

    public static ExibicaoLeituraComprovante SemConfiguracao() =>
        Erro(
            SituacaoLeituraComprovante.SemUrlModeloOuChave,
            TextoSemUrlModeloOuChave,
            levaParaInteligencia: true);

    public static ExibicaoLeituraComprovante FotoInvalida() =>
        Erro(SituacaoLeituraComprovante.Falha, TextoFotoInvalida, levaParaInteligencia: false);

    public static ExibicaoLeituraComprovante FotoGrandeDemais() =>
        Erro(SituacaoLeituraComprovante.Falha, TextoFotoGrandeDemais, levaParaInteligencia: false);

    public static ExibicaoLeituraComprovante FormatoNaoConvertido() =>
        Erro(SituacaoLeituraComprovante.Falha, TextoFormatoNaoConvertido, levaParaInteligencia: false);

    public static ExibicaoLeituraComprovante FotoAcimaDoTeto() =>
        Erro(SituacaoLeituraComprovante.Falha, TextoFotoAcimaDoTeto, levaParaInteligencia: false);

    /// <summary>
    /// URL https sem usuário nem parâmetros, modelo e chave preenchidos.
    /// Não guarda a chave: só responde se os três estão prontos.
    /// </summary>
    public static bool TemUrlModeloEChave(string? url, string? modelo, string? chave) =>
        PedidoMensagem.CriarUri(url) is not null &&
        !string.IsNullOrWhiteSpace(modelo) &&
        !string.IsNullOrWhiteSpace(chave);

    /// <summary>
    /// Nulo quando a leitura pode começar. Chave vazia ou só com espaços pede a chave.
    /// Sem URL ou modelo, a frase genérica. Não guarda a chave.
    /// </summary>
    public static ExibicaoLeituraComprovante? ImpedimentoParaLer(string? url, string? modelo, string? chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            return De(
                ResultadoMensagem.Falha(
                    TipoResultadoMensagem.PedidoInvalido,
                    ResultadoMensagem.TextoChaveNaoInformada),
                null);
        }

        if (!TemUrlModeloEChave(url, modelo, chave))
            return SemConfiguracao();

        return null;
    }

    public static ExibicaoLeituraComprovante De(ResultadoMensagem resultado, ComprovanteNormalizado? normalizado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        switch (resultado.Tipo)
        {
            case TipoResultadoMensagem.Ok:
                return DeSucesso(normalizado);
            case TipoResultadoMensagem.NaoAutorizado
                when resultado.Mensagem == ResultadoMensagem.TextoChaveNaoInformada:
            case TipoResultadoMensagem.PedidoInvalido
                when resultado.Mensagem == ResultadoMensagem.TextoChaveNaoInformada:
                return Erro(
                    SituacaoLeituraComprovante.SemUrlModeloOuChave,
                    ResultadoMensagem.TextoChaveNaoInformada,
                    true,
                    resultado.Diagnostico);
            case TipoResultadoMensagem.NaoAutorizado:
                return Erro(SituacaoLeituraComprovante.NaoAutorizado, ResultadoMensagem.TextoNaoAutorizado, false, resultado.Diagnostico);
            case TipoResultadoMensagem.SemPermissao:
                return Erro(SituacaoLeituraComprovante.SemPermissao, ResultadoMensagem.TextoSemPermissao, false, resultado.Diagnostico);
            case TipoResultadoMensagem.Limite:
            case TipoResultadoMensagem.Sobrecarga:
                return Erro(SituacaoLeituraComprovante.LimiteProvedor, ResultadoMensagem.TextoLimite, false, resultado.Diagnostico);
            case TipoResultadoMensagem.Timeout:
                return Erro(SituacaoLeituraComprovante.TempoEsgotado, ResultadoMensagem.TextoTimeout, false, resultado.Diagnostico);
            case TipoResultadoMensagem.SemRede:
                return Erro(SituacaoLeituraComprovante.SemRede, ResultadoMensagem.TextoSemRede, false, resultado.Diagnostico);
            case TipoResultadoMensagem.RespostaInvalida
                when resultado.Mensagem == ResultadoMensagem.TextoRespostaCortada:
                return Erro(
                    SituacaoLeituraComprovante.RespostaForaDoSchema,
                    ResultadoMensagem.TextoRespostaCortada,
                    false,
                    resultado.Diagnostico);
            case TipoResultadoMensagem.RespostaInvalida:
                return Erro(
                    SituacaoLeituraComprovante.RespostaForaDoSchema,
                    ResultadoMensagem.TextoRespostaInvalida,
                    false,
                    resultado.Diagnostico);
            case TipoResultadoMensagem.ModeloSemVisao:
                return Erro(
                    SituacaoLeituraComprovante.ModeloSemVisao,
                    ResultadoMensagem.TextoModeloSemVisao,
                    true,
                    resultado.Diagnostico);
            case TipoResultadoMensagem.PedidoInvalido
                when resultado.Mensagem is ResultadoMensagem.TextoUrlNaoInformada
                    or ResultadoMensagem.TextoUrlInvalida
                    or ResultadoMensagem.TextoModeloNaoInformado:
                return SemConfiguracao();
            case TipoResultadoMensagem.PedidoInvalido:
                return Erro(
                    SituacaoLeituraComprovante.PedidoRecusado,
                    ResultadoMensagem.TextoPedidoInvalido,
                    false,
                    resultado.Diagnostico);
            default:
                return Erro(SituacaoLeituraComprovante.Falha, ResultadoMensagem.TextoFalha, false, resultado.Diagnostico);
        }
    }

    public static string Resumo(ComprovanteNormalizado comprovante)
    {
        ArgumentNullException.ThrowIfNull(comprovante);
        var quantidade = comprovante.Linhas.Count;
        var itens = quantidade == 1 ? "1 item" : $"{quantidade} itens";
        return $"{itens}. {TextoDoTotal(comprovante)} {TextoPreviaASeguir}";
    }

    private static ExibicaoLeituraComprovante DeSucesso(ComprovanteNormalizado? normalizado)
    {
        if (normalizado is null)
            return Erro(SituacaoLeituraComprovante.RespostaForaDoSchema, ResultadoMensagem.TextoRespostaInvalida, false);

        if (!normalizado.Legivel || normalizado.Linhas.Count == 0)
            return Erro(SituacaoLeituraComprovante.IlegivelOuSemItens, TextoIlegivelOuSemItens, false);

        return new ExibicaoLeituraComprovante
        {
            Situacao = SituacaoLeituraComprovante.Pronta,
            Mensagem = TextoPreviaASeguir,
            Resumo = Resumo(normalizado)
        };
    }

    private static string TextoDoTotal(ComprovanteNormalizado comprovante)
    {
        if (comprovante.TotalCupom is decimal cupom && comprovante.TotalDivergente)
        {
            return $"Total do cupom {Moeda(cupom)}. Soma das linhas {Moeda(comprovante.SomaLinhas)}.";
        }

        var total = comprovante.TotalCupom ?? comprovante.SomaLinhas;
        return $"Total {Moeda(total)}.";
    }

    private static string Moeda(decimal valor) => valor.ToString("C", Cultura);

    private static ExibicaoLeituraComprovante Erro(
        SituacaoLeituraComprovante situacao,
        string mensagem,
        bool levaParaInteligencia,
        DiagnosticoResposta? diagnostico = null) =>
        new()
        {
            Situacao = situacao,
            Mensagem = mensagem,
            LevaParaInteligenciaArtificial = levaParaInteligencia,
            PodeTentarDeNovo = true,
            Diagnostico = diagnostico
        };
}
