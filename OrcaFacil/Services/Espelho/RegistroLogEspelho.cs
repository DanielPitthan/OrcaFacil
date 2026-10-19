using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Data;
using Serilog;

namespace OrcaFacil.Services.Espelho;

/// <summary>
/// Log do backup do espelho. A tela cria uma instância; este tipo não entra no registro
/// de DI, para o conflito com o coordenador continuar só na linha do provedor de senha.
/// A exceção não segue crua: o log recebe o tipo e a mensagem sem caminho nem URI.
/// </summary>
public sealed class RegistroLogEspelho : IRegistroLog
{
    public void Informacao(string mensagem, object?[] argumentos)
    {
        try
        {
            Log.Information(mensagem, argumentos);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public void Erro(Exception? excecao, string mensagem, object?[] argumentos)
    {
        try
        {
            Log.Error("{Registro}", TextoLogEspelho.Montar(mensagem, argumentos, excecao));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
