namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Abre o espelho com a senha atual. Se ela for recusada e ainda houver senha
/// anterior retida, tenta essa segunda senha uma única vez.
/// </summary>
public static class LeituraComSenhaRetida
{
    public static Task<LeituraEspelho> LerAsync(
        string caminho,
        IProvedorSenhaEspelho senha,
        VersaoSchemaAplicacao versao,
        CancellationToken cancellationToken = default)
        => LerAsync(caminho, senha, versao, cancellationToken, abrir: null);

    internal static async Task<LeituraEspelho> LerAsync(
        string caminho,
        IProvedorSenhaEspelho senha,
        VersaoSchemaAplicacao versao,
        CancellationToken cancellationToken,
        Func<string, string, VersaoSchemaAplicacao, CancellationToken, Task<LeituraEspelho>>? abrir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        ArgumentNullException.ThrowIfNull(senha);
        ArgumentNullException.ThrowIfNull(versao);
        abrir ??= AbrirDeVerdade;

        var atual = senha.Obter();
        if (string.IsNullOrEmpty(atual))
            return LeituraEspelho.Ilegivel("Senha do espelho ausente.");

        var primeira = await TentarAsync(caminho, atual, versao, cancellationToken, abrir);
        if (primeira.Leitura.Legivel)
        {
            AdocaoSenhaLeituraNaoConfirmada.AoLeituraFuncionar(senha, atual);
            return primeira.Leitura;
        }

        if (!primeira.SenhaRecusada)
            return primeira.Leitura;

        var anterior = (senha as ISenhaEspelhoRetida)?.ObterAnterior();
        if (string.IsNullOrEmpty(anterior))
            return primeira.Leitura;

        var segunda = await TentarAsync(caminho, anterior, versao, cancellationToken, abrir);
        if (segunda.Leitura.Legivel)
            AdocaoSenhaLeituraNaoConfirmada.AoLeituraFuncionar(senha, anterior);

        return segunda.Leitura;
    }

    private static async Task<LeituraEspelho> AbrirDeVerdade(
        string caminho,
        string senha,
        VersaoSchemaAplicacao versao,
        CancellationToken cancellationToken)
    {
        using var snapshot = await LerEspelho.AbrirAsync(caminho, senha, versao, cancellationToken);
        return LeituraEspelho.Ok(snapshot.Metadados.VersaoSchema);
    }

    private static async Task<Tentativa> TentarAsync(
        string caminho,
        string senha,
        VersaoSchemaAplicacao versao,
        CancellationToken cancellationToken,
        Func<string, string, VersaoSchemaAplicacao, CancellationToken, Task<LeituraEspelho>> abrir)
    {
        try
        {
            return new Tentativa(await abrir(caminho, senha, versao, cancellationToken), false);
        }
        catch (SenhaIncorretaException ex)
        {
            return new Tentativa(LeituraEspelho.Ilegivel(ex.Message), true);
        }
        catch (SchemaMaisNovoQueOAppException ex)
        {
            return new Tentativa(LeituraEspelho.Ok(ex.VersaoEspelho), false);
        }
        catch (FalhaEspelhoException ex)
        {
            return new Tentativa(LeituraEspelho.Ilegivel(ex.Message), false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new Tentativa(LeituraEspelho.Ilegivel("Espelho ilegível."), false);
        }
    }

    private readonly record struct Tentativa(LeituraEspelho Leitura, bool SenhaRecusada);
}
