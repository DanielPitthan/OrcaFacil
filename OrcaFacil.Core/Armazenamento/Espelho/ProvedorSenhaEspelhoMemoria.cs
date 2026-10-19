namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Guarda a senha do espelho enquanto o processo está aberto.
/// Não grava em Preferences, SecureStorage, arquivo nem backup. Fechar o app esquece a senha.
/// </summary>
public interface IDefinicaoSenhaEspelho
{
    bool Informada { get; }

    ResultadoDefinicaoSenha Definir(string? senha, string? confirmacao);

    void Esquecer();
}

public sealed class ResultadoDefinicaoSenha
{
    private ResultadoDefinicaoSenha(bool aceita, string? motivo)
    {
        if (!aceita && string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Recusa precisa do motivo.", nameof(motivo));

        if (aceita && motivo is not null)
            throw new ArgumentException("Senha aceita não leva motivo.", nameof(motivo));

        Aceita = aceita;
        Motivo = motivo;
    }

    public bool Aceita { get; }

    public string? Motivo { get; }

    public static ResultadoDefinicaoSenha Ok() => new(true, null);

    public static ResultadoDefinicaoSenha Recusada(string motivo) => new(false, motivo);

    public override string ToString() => Aceita ? "ResultadoDefinicaoSenha" : "ResultadoDefinicaoSenha:recusada";
}

/// <summary>
/// Implementação de <see cref="IProvedorSenhaEspelho"/> que só existe em memória.
/// <see cref="ToString"/> não devolve a senha.
/// </summary>
public sealed class ProvedorSenhaEspelhoMemoria : IProvedorSenhaEspelho, IDefinicaoSenhaEspelho
{
    private readonly object _trava = new();
    private string? _senha;

    public bool Informada
    {
        get
        {
            lock (_trava)
                return !string.IsNullOrEmpty(_senha);
        }
    }

    public string? Obter()
    {
        lock (_trava)
            return _senha;
    }

    public ResultadoDefinicaoSenha Definir(string? senha, string? confirmacao)
    {
        if (!string.Equals(senha, confirmacao, StringComparison.Ordinal))
        {
            return ResultadoDefinicaoSenha.Recusada(
                "As senhas não coincidem. A senha desta sessão não mudou.");
        }

        if (!RegraSenhaEspelho.AtendeForcaMinima(senha))
            return ResultadoDefinicaoSenha.Recusada(RegraSenhaEspelho.Requisitos);

        lock (_trava)
            _senha = senha;

        return ResultadoDefinicaoSenha.Ok();
    }

    public void Esquecer()
    {
        lock (_trava)
            _senha = null;
    }

    public override string ToString() => "ProvedorSenhaEspelhoMemoria";
}
