using System.Text.RegularExpressions;

namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// Falha explícita ao usar a pasta do espelho. A mensagem sai daqui e não repete
/// exceção, URI nem caminho.
/// </summary>
public enum CodigoFalhaPastaEspelho
{
    Nenhuma = 0,
    PastaNaoEscolhida,
    PermissaoRevogada,
    PastaApagada,
    SemEspaco,
    ProvedorSemTroca,
    ArquivoAusente,
    Cancelado,
    Ocupado,
    FalhaDeEscrita,
    FalhaDeLeitura,
    SemPermissaoDeEscrita,
    NomeRecusado,
    EspelhoPreservadoForaDoNome,
    TrocaNaoConfirmada,
    Indisponivel
}

public static class MensagensPastaEspelho
{
    public static string Para(CodigoFalhaPastaEspelho codigo) => codigo switch
    {
        CodigoFalhaPastaEspelho.Nenhuma => "",
        CodigoFalhaPastaEspelho.PastaNaoEscolhida =>
            "Nenhuma pasta foi escolhida para o espelho.",
        CodigoFalhaPastaEspelho.PermissaoRevogada =>
            "A permissão da pasta do espelho foi revogada. Escolha a pasta de novo. Nenhum arquivo foi apagado.",
        CodigoFalhaPastaEspelho.PastaApagada =>
            "A pasta do espelho não está mais disponível. Ela pode ter sido apagada ou movida. Nenhum arquivo foi apagado.",
        CodigoFalhaPastaEspelho.SemEspaco =>
            "Não há espaço para gravar o espelho. O arquivo anterior foi mantido.",
        CodigoFalhaPastaEspelho.ProvedorSemTroca =>
            "Esta pasta não permite substituir o espelho com segurança. O arquivo anterior foi mantido. Use uma pasta no armazenamento do aparelho.",
        CodigoFalhaPastaEspelho.ArquivoAusente =>
            "Não há espelho nesta pasta.",
        CodigoFalhaPastaEspelho.Cancelado =>
            "A escolha foi cancelada. Nada foi alterado.",
        CodigoFalhaPastaEspelho.Ocupado =>
            "Já existe uma escolha de pasta em andamento.",
        CodigoFalhaPastaEspelho.FalhaDeEscrita =>
            "Não foi possível gravar o espelho. O arquivo anterior foi mantido.",
        CodigoFalhaPastaEspelho.FalhaDeLeitura =>
            "Não foi possível ler o espelho. Nenhum arquivo foi apagado.",
        CodigoFalhaPastaEspelho.SemPermissaoDeEscrita =>
            "A pasta foi aberta sem permissão de escrita. Nada foi gravado.",
        CodigoFalhaPastaEspelho.NomeRecusado =>
            "A operação tentou alterar um arquivo que não pertence à troca do espelho. Nada foi apagado.",
        CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome =>
            "A troca não terminou. O espelho anterior continua na pasta, fora do nome definitivo, e não foi apagado.",
        CodigoFalhaPastaEspelho.TrocaNaoConfirmada =>
            "A troca do espelho não pôde ser confirmada. Nenhum outro arquivo foi apagado.",
        CodigoFalhaPastaEspelho.Indisponivel =>
            "Não foi possível acessar a pasta do espelho. Nenhum arquivo foi apagado.",
        _ => "Não foi possível acessar a pasta do espelho. Nenhum arquivo foi apagado."
    };
}

/// <summary>Sinal já sem URI. O classificador não devolve o texto da exceção.</summary>
public readonly record struct SinalFalhaPasta(
    string? NomeTipo,
    string? Mensagem,
    bool? PermissaoAindaPersistida,
    bool? DocumentoEncontrado)
{
    public static SinalFalhaPasta De(Exception excecao, bool? permissaoAindaPersistida = null, bool? documentoEncontrado = null)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        return new SinalFalhaPasta(Nomes(excecao), Textos(excecao), permissaoAindaPersistida, documentoEncontrado);
    }

    private static string Nomes(Exception excecao)
    {
        var nomes = new List<string>();
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
            nomes.Add(atual.GetType().Name);

        return string.Join(' ', nomes);
    }

    private static string Textos(Exception excecao)
    {
        var textos = new List<string>();
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(atual.Message))
                textos.Add(atual.Message);
        }

        return string.Join(' ', textos);
    }
}

public static class ClassificadorFalhaPastaEspelho
{
    public static CodigoFalhaPastaEspelho Classificar(SinalFalhaPasta sinal)
    {
        if (sinal.PermissaoAindaPersistida == false)
            return CodigoFalhaPastaEspelho.PermissaoRevogada;

        var texto = TextoClassificavelPasta.Montar(sinal.NomeTipo, sinal.Mensagem);
        if (Contem(texto, "SecurityException") || Contem(texto, "Permission Denial") || Contem(texto, "Permission denied"))
            return CodigoFalhaPastaEspelho.PermissaoRevogada;

        if (Contem(texto, "ENOSPC") || Contem(texto, "No space left") || Contem(texto, "not enough space") || Contem(texto, "Disk full"))
            return CodigoFalhaPastaEspelho.SemEspaco;

        if (sinal.DocumentoEncontrado == false)
            return CodigoFalhaPastaEspelho.PastaApagada;

        if (Contem(texto, "FileNotFoundException") || Contem(texto, "No such file") || Contem(texto, "ENOENT") || Contem(texto, "StaleDataException"))
            return CodigoFalhaPastaEspelho.PastaApagada;

        return CodigoFalhaPastaEspelho.Indisponivel;
    }

    private static bool Contem(string texto, string trecho)
        => texto.Contains(trecho, StringComparison.OrdinalIgnoreCase);
}

public static partial class TextoClassificavelPasta
{
    public static string Montar(string? nomeTipo, string? mensagem)
        => RemoverUris(string.Concat(nomeTipo, " ", mensagem).Trim());

    public static string RemoverUris(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
            return "";

        return UriNoTexto().Replace(texto, "[uri]");
    }

    [GeneratedRegex(@"(?i)\b(?:content|file):[^\s""']+", RegexOptions.CultureInvariant)]
    private static partial Regex UriNoTexto();
}

public sealed record DiagnosticoPastaEspelho
{
    public DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho codigo, bool arquivoNoNomeFinal)
    {
        if (arquivoNoNomeFinal && codigo != CodigoFalhaPastaEspelho.Nenhuma)
            throw new ArgumentException("Só dá para afirmar que o arquivo está na pasta quando o acesso foi livre.");

        Codigo = codigo;
        ArquivoNoNomeFinal = arquivoNoNomeFinal;
    }

    public CodigoFalhaPastaEspelho Codigo { get; }

    public bool ArquivoNoNomeFinal { get; }

    public bool AcessoLivre => Codigo == CodigoFalhaPastaEspelho.Nenhuma;
}

public sealed record ResultadoOperacaoPasta
{
    public ResultadoOperacaoPasta(bool sucesso, CodigoFalhaPastaEspelho codigo, string mensagem)
    {
        if (sucesso != (codigo == CodigoFalhaPastaEspelho.Nenhuma))
            throw new ArgumentException("Sucesso e código não combinam.");
        if (sucesso && mensagem.Length != 0)
            throw new ArgumentException("Sucesso não leva mensagem.");
        if (!sucesso && string.IsNullOrWhiteSpace(mensagem))
            throw new ArgumentException("Falha precisa de mensagem.");

        Sucesso = sucesso;
        Codigo = codigo;
        Mensagem = mensagem;
    }

    public bool Sucesso { get; }

    public CodigoFalhaPastaEspelho Codigo { get; }

    public string Mensagem { get; }

    public static ResultadoOperacaoPasta Ok() => new(true, CodigoFalhaPastaEspelho.Nenhuma, "");

    public static ResultadoOperacaoPasta Falha(CodigoFalhaPastaEspelho codigo)
    {
        if (codigo == CodigoFalhaPastaEspelho.Nenhuma)
            throw new ArgumentException("Falha precisa de um código.", nameof(codigo));

        return new ResultadoOperacaoPasta(false, codigo, MensagensPastaEspelho.Para(codigo));
    }

    public override string ToString() => Sucesso ? "ResultadoOperacaoPasta" : $"ResultadoOperacaoPasta:{Codigo}";
}

public sealed record ResultadoRenomeacaoVolume
{
    public ResultadoRenomeacaoVolume(CodigoFalhaPastaEspelho codigo, bool destinoJaExistia = false)
    {
        if (codigo == CodigoFalhaPastaEspelho.Nenhuma && destinoJaExistia)
            throw new ArgumentException("Renomeação concluída não tem destino prévio.");

        Codigo = codigo;
        DestinoJaExistia = destinoJaExistia;
    }

    public CodigoFalhaPastaEspelho Codigo { get; }

    public bool DestinoJaExistia { get; }

    public bool Sucesso => Codigo == CodigoFalhaPastaEspelho.Nenhuma;

    public static ResultadoRenomeacaoVolume Ok() => new(CodigoFalhaPastaEspelho.Nenhuma);

    public static ResultadoRenomeacaoVolume Falha(CodigoFalhaPastaEspelho codigo, bool destinoJaExistia = false)
        => new(codigo, destinoJaExistia);
}

public sealed record ResultadoEscolhaPasta
{
    public ResultadoEscolhaPasta(bool sucesso, CodigoFalhaPastaEspelho codigo, string mensagem)
    {
        if (sucesso != (codigo == CodigoFalhaPastaEspelho.Nenhuma))
            throw new ArgumentException("Sucesso e código não combinam.");
        if (!sucesso && string.IsNullOrWhiteSpace(mensagem))
            throw new ArgumentException("Falha precisa de mensagem.");

        Sucesso = sucesso;
        Codigo = codigo;
        Mensagem = mensagem;
    }

    public bool Sucesso { get; }

    public CodigoFalhaPastaEspelho Codigo { get; }

    public string Mensagem { get; }

    public static ResultadoEscolhaPasta Ok() => new(true, CodigoFalhaPastaEspelho.Nenhuma, "");

    public static ResultadoEscolhaPasta Falha(CodigoFalhaPastaEspelho codigo)
        => new(false, codigo, MensagensPastaEspelho.Para(codigo));

    public override string ToString() => Sucesso ? "ResultadoEscolhaPasta" : $"ResultadoEscolhaPasta:{Codigo}";
}
