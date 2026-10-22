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

/// <summary>
/// Senha anterior retida só em memória, enquanto a leitura do arquivo final não foi confirmada.
/// Não persiste e não entra em <see cref="object.ToString"/>.
/// Enquanto as duas senhas falharem, as duas continuam retidas e cada leitura tenta a atual
/// e, em seguida, a anterior uma única vez. Quando uma leitura funciona, fica só a senha
/// que abriu: se foi a anterior, ela passa a ser a atual e a outra é descartada.
/// </summary>
public interface ISenhaEspelhoRetida
{
    string? ObterAnterior();

    void ConfirmarLeitura(string senhaQueAbriu);
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
/// <see cref="ToString"/> não devolve a senha. A senha anterior, quando retida, também
/// fica só aqui e é tentada a cada leitura enquanto as duas falharem.
/// </summary>
public sealed class ProvedorSenhaEspelhoMemoria : IProvedorSenhaEspelho, IDefinicaoSenhaEspelho, ISenhaEspelhoRetida
{
    private readonly object _trava = new();
    private string? _senha;
    private string? _anterior;
    private bool _leituraConfirmada = true;

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

    public string? ObterAnterior()
    {
        lock (_trava)
            return _leituraConfirmada ? null : _anterior;
    }

    public ResultadoDefinicaoSenha Definir(string? senha, string? confirmacao)
        => Guardar(senha, confirmacao, reterAnterior: false);

    /// <summary>
    /// A senha nova entra e a que já estava fica só em memória até uma leitura funcionar.
    /// Enquanto as duas falharem, as duas continuam aqui e são tentadas a cada leitura.
    /// </summary>
    public ResultadoDefinicaoSenha DefinirRetendoAnterior(string? senha, string? confirmacao)
        => Guardar(senha, confirmacao, reterAnterior: true);

    public void ConfirmarLeitura(string senhaQueAbriu)
    {
        if (string.IsNullOrEmpty(senhaQueAbriu))
            return;

        lock (_trava)
        {
            _senha = senhaQueAbriu;
            _anterior = null;
            _leituraConfirmada = true;
        }
    }

    public void Esquecer()
    {
        lock (_trava)
        {
            _senha = null;
            _anterior = null;
            _leituraConfirmada = true;
        }
    }

    public override string ToString() => "ProvedorSenhaEspelhoMemoria";

    private ResultadoDefinicaoSenha Guardar(string? senha, string? confirmacao, bool reterAnterior)
    {
        if (!string.Equals(senha, confirmacao, StringComparison.Ordinal))
        {
            return ResultadoDefinicaoSenha.Recusada(
                "As senhas não coincidem. A senha desta sessão não mudou.");
        }

        if (!RegraSenhaEspelho.AtendeForcaMinima(senha))
            return ResultadoDefinicaoSenha.Recusada(RegraSenhaEspelho.Requisitos);

        lock (_trava)
        {
            if (reterAnterior)
            {
                _anterior = !string.IsNullOrEmpty(_senha) && !string.Equals(_senha, senha, StringComparison.Ordinal)
                    ? _senha
                    : null;
                _leituraConfirmada = false;
            }
            else
            {
                _anterior = null;
                _leituraConfirmada = true;
            }

            _senha = senha;
        }

        return ResultadoDefinicaoSenha.Ok();
    }
}
