namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Senha já informada por quem chama. Null enquanto o usuário não digitou.
/// Não registre o retorno, não o persista em log e não o coloque em mensagem de erro.
/// </summary>
public interface IProvedorSenhaEspelho
{
    string? Obter();
}

/// <summary>
/// <see cref="IEspelhoDados"/> em cima do arquivo cifrado.
/// Sem senha, ou com arquivo inválido, a leitura é ilegível e não traz versão.
/// Schema mais novo que o app continua legível: a matriz pede atualização em vez de tratar o arquivo como lixo.
/// </summary>
/// <remarks>
/// A leitura descriptografa para um temporário, valida e apaga esse temporário antes de voltar.
/// Não copia o espelho por cima do banco vivo, não apaga o espelho e não semeia.
/// Restauração que falha continua sendo decisão do <see cref="CoordenadorArmazenamento"/>:
/// esta classe não implementa <see cref="IRestauracaoEspelho"/>.
/// </remarks>
public sealed class EspelhoArquivo : IEspelhoDados
{
    private readonly string _caminho;
    private readonly IProvedorSenhaEspelho _senha;
    private readonly VersaoSchemaAplicacao _versao;

    public EspelhoArquivo(string caminhoEspelho, IProvedorSenhaEspelho senha, VersaoSchemaAplicacao versao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoEspelho);
        _caminho = caminhoEspelho;
        _senha = senha ?? throw new ArgumentNullException(nameof(senha));
        _versao = versao ?? throw new ArgumentNullException(nameof(versao));
    }

    public bool Existe()
    {
        try
        {
            return File.Exists(_caminho);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task<LeituraEspelho> LerAsync(CancellationToken cancellationToken = default)
    {
        if (!Existe())
            return LeituraEspelho.Ilegivel("Espelho ausente.");

        var senha = _senha.Obter();
        if (string.IsNullOrEmpty(senha))
            return LeituraEspelho.Ilegivel("Senha do espelho ausente.");

        try
        {
            using var snapshot = await LerEspelho.AbrirAsync(_caminho, senha, _versao, cancellationToken);
            return LeituraEspelho.Ok(snapshot.Metadados.VersaoSchema);
        }
        catch (SchemaMaisNovoQueOAppException ex)
        {
            return LeituraEspelho.Ok(ex.VersaoEspelho);
        }
        catch (FalhaEspelhoException ex)
        {
            return LeituraEspelho.Ilegivel(ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return LeituraEspelho.Ilegivel("Espelho ilegível.");
        }
    }

    public override string ToString() => "EspelhoArquivo";
}
