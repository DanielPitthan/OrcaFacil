using OrcaFacil.Core.Tema;

namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// URI da pasta escolhida no SAF. Fica nas preferências. Quem implementa o Android
/// não registra essa URI em log: <see cref="DescricaoParaLog"/> e <see cref="ToString"/>
/// não a devolvem.
/// </summary>
public interface IMemoriaPastaEspelho
{
    bool PossuiEscolha { get; }

    string? LerUri();

    void Gravar(string uri);

    void Esquecer();

    string DescricaoParaLog();
}

public static class UriEscolhaPasta
{
    public const string ChavePreferencia = "Espelho.Pasta.Uri";

    public static bool EhUriDeConteudo(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        if (!uri.StartsWith("content:", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var caractere in uri)
        {
            if (char.IsControl(caractere))
                return false;
        }

        return true;
    }
}

public sealed class MemoriaPastaEspelho : IMemoriaPastaEspelho
{
    private readonly IPreferenciasSimples _preferencias;

    public MemoriaPastaEspelho(IPreferenciasSimples preferencias)
    {
        _preferencias = preferencias ?? throw new ArgumentNullException(nameof(preferencias));
    }

    public bool PossuiEscolha => UriEscolhaPasta.EhUriDeConteudo(LerBruto());

    public string? LerUri()
    {
        var bruto = LerBruto();
        return UriEscolhaPasta.EhUriDeConteudo(bruto) ? bruto : null;
    }

    public void Gravar(string uri)
    {
        if (!UriEscolhaPasta.EhUriDeConteudo(uri))
            throw new ArgumentException("A pasta do espelho precisa de uma URI de conteúdo.", nameof(uri));

        _preferencias.Definir(UriEscolhaPasta.ChavePreferencia, uri);
    }

    public void Esquecer()
    {
        if (!_preferencias.Contem(UriEscolhaPasta.ChavePreferencia))
            return;

        // A interface de preferências não remove uma chave. Vazio não é URI de conteúdo,
        // então a escolha deixa de existir e a URI anterior não permanece.
        _preferencias.Definir(UriEscolhaPasta.ChavePreferencia, "");
    }

    public string DescricaoParaLog()
        => PossuiEscolha ? "pasta do espelho: escolhida" : "pasta do espelho: não escolhida";

    public override string ToString() => "MemoriaPastaEspelho";

    private string? LerBruto()
    {
        if (!_preferencias.Contem(UriEscolhaPasta.ChavePreferencia))
            return null;

        var valor = _preferencias.Obter(UriEscolhaPasta.ChavePreferencia, "");
        return valor.Length == 0 ? null : valor;
    }
}

/// <summary>
/// Decide se a URI entra nas preferências. Cancelamento, falta de escrita ou
/// permissão que não persistiu mantêm a escolha anterior.
/// </summary>
public static class AplicacaoEscolhaPasta
{
    public static CodigoFalhaPastaEspelho Decidir(
        bool usuarioConfirmou,
        bool concedeuLeitura,
        bool concedeuEscrita,
        bool permissaoPersistida)
    {
        if (!usuarioConfirmou)
            return CodigoFalhaPastaEspelho.Cancelado;

        if (!concedeuEscrita || !concedeuLeitura)
            return CodigoFalhaPastaEspelho.SemPermissaoDeEscrita;

        if (!permissaoPersistida)
            return CodigoFalhaPastaEspelho.PermissaoRevogada;

        return CodigoFalhaPastaEspelho.Nenhuma;
    }

    public static CodigoFalhaPastaEspelho Aplicar(
        IMemoriaPastaEspelho memoria,
        bool usuarioConfirmou,
        bool concedeuLeitura,
        bool concedeuEscrita,
        bool permissaoPersistida,
        string? uri)
    {
        ArgumentNullException.ThrowIfNull(memoria);
        var codigo = Decidir(usuarioConfirmou, concedeuLeitura, concedeuEscrita, permissaoPersistida);
        if (codigo != CodigoFalhaPastaEspelho.Nenhuma)
            return codigo;

        if (!UriEscolhaPasta.EhUriDeConteudo(uri))
            return CodigoFalhaPastaEspelho.Indisponivel;

        memoria.Gravar(uri!);
        return CodigoFalhaPastaEspelho.Nenhuma;
    }
}
