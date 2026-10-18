using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace OrcaFacil.Core.Data;

public enum FaseInicializacao
{
    Pendente,
    EmAndamento,
    Pronta,
    Falhou
}

/// <summary>O que a primeira tela faz com o estado da abertura. Usado por Routes e pela tela de recuperação.</summary>
public static class DecisaoAbertura
{
    public static bool MostrarAplicativo(FaseInicializacao fase) => fase == FaseInicializacao.Pronta;

    public static bool DeveIniciar(FaseInicializacao fase) => fase == FaseInicializacao.Pendente;

    public static bool MostrarFalha(FaseInicializacao fase) => fase == FaseInicializacao.Falhou;
}

public static class PoliticaSeed
{
    public static bool DevePopular(ResultadoPreparoBanco resultado, bool haMembro) =>
        !resultado.ExigeRecuperacao && !haMembro;
}

public static class FalhasInicializacao
{
    public static bool ImpedeAbertura(Exception ex)
    {
        for (var atual = ex; atual is not null; atual = atual.InnerException)
        {
            if (atual is DbException or DbUpdateException)
                return true;
        }

        return false;
    }

    public static string Resumir(Exception ex)
    {
        var mensagens = new List<string>();
        for (var atual = ex; atual is not null && mensagens.Count < 3; atual = atual.InnerException)
        {
            if (string.IsNullOrWhiteSpace(atual.Message))
                continue;

            var texto = atual.Message.Trim();
            if (mensagens.Count > 0 && string.Equals(mensagens[^1], texto, StringComparison.Ordinal))
                continue;

            mensagens.Add(texto);
        }

        var resumo = mensagens.Count == 0 ? ex.GetType().Name : string.Join(" — ", mensagens);
        const int limite = 500;
        return resumo.Length <= limite ? resumo : resumo[..limite] + "…";
    }
}
