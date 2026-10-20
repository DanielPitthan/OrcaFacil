namespace OrcaFacil.Core.Ativos;

/// <summary>
/// Posição na ordem data e, no mesmo dia, Id.
/// O cálculo mora em <see cref="CalculadoraResultadoRealizado"/>, o mesmo da tela e da importação.
/// </summary>
public static class PosicaoCalculator
{
    public static PosicaoCalculada Calcular(IEnumerable<MovimentoPosicao> operacoes)
        => CalculadoraResultadoRealizado.Calcular(operacoes).Posicao;
}
