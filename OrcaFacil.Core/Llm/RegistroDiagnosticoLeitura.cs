namespace OrcaFacil.Core.Llm;

/// <summary>
/// Entrega o texto já sanitizado para o logger que a tela já usa.
/// O cliente HTTP não grava log: quem registra é o painel, com este mesmo texto.
/// </summary>
public static class RegistroDiagnosticoLeitura
{
    public static void Registrar(DiagnosticoResposta diagnostico, Action<string> registrar)
    {
        ArgumentNullException.ThrowIfNull(diagnostico);
        ArgumentNullException.ThrowIfNull(registrar);
        registrar(diagnostico.Texto);
    }
}
