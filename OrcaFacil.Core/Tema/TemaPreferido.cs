namespace OrcaFacil.Core.Tema;

/// <summary>
/// Preferência de tema gravada em <c>Config.Tema</c>.
/// <see cref="Automatico"/> é 0 para coincidir com <c>default</c> e com a chave ausente.
/// Claro e Escuro ficam explícitos: um inteiro já gravado continua o mesmo valor.
/// </summary>
public enum TemaPreferido
{
    Automatico = 0,
    Claro = 1,
    Escuro = 2
}
