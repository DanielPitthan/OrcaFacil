using Microsoft.EntityFrameworkCore;

namespace OrcaFacil.Core.Lancamentos;

/// <summary>
/// Texto mostrado quando o banco recusa a gravação. Não inclui Message nem stack.
/// </summary>
public static class FalhaDeGravacao
{
    public const string MensagemLancamento = "Não foi possível gravar o lançamento.";

    public const string MensagemCategoria = "Não foi possível gravar a categoria.";

    public static InvalidOperationException ParaOUsuario(DbUpdateException excecao, string mensagem)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        ArgumentException.ThrowIfNullOrWhiteSpace(mensagem);
        return new InvalidOperationException(mensagem);
    }
}
