namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// Texto da pasta na tela. Não recebe a URI e <see cref="ResumoPastaEspelho.ToString"/> não a devolveria.
/// </summary>
public static class RotuloPastaEspelho
{
    public static ResumoPastaEspelho Descrever(DiagnosticoPastaEspelho diagnostico)
    {
        ArgumentNullException.ThrowIfNull(diagnostico);
        if (!diagnostico.AcessoLivre)
        {
            var valor = diagnostico.Codigo == CodigoFalhaPastaEspelho.PastaNaoEscolhida
                ? "Não escolhida"
                : "Indisponível";
            return new ResumoPastaEspelho(valor, MensagensPastaEspelho.Para(diagnostico.Codigo));
        }

        return diagnostico.ArquivoNoNomeFinal
            ? new ResumoPastaEspelho("Escolhida", "Há um espelho nesta pasta.")
            : new ResumoPastaEspelho("Escolhida", "Ainda não há espelho nesta pasta.");
    }
}

public sealed class ResumoPastaEspelho
{
    public ResumoPastaEspelho(string valor, string detalhe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);
        ArgumentException.ThrowIfNullOrWhiteSpace(detalhe);
        Valor = valor;
        Detalhe = detalhe;
    }

    public string Valor { get; }

    public string Detalhe { get; }

    public override string ToString() => "ResumoPastaEspelho";
}
