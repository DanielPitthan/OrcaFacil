namespace OrcaFacil.Core.Tema;

/// <summary>Leitura e gravação de preferências em texto. O app usa o Preferences do MAUI; o teste usa memória.</summary>
public interface IPreferenciasSimples
{
    bool Contem(string chave);
    string Obter(string chave, string padrao);
    void Definir(string chave, string valor);
    void Limpar();
}
