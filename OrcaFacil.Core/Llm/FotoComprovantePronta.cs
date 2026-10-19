namespace OrcaFacil.Core.Llm;

/// <summary>
/// JPEG em memória pronto para o provedor, ou o motivo de não enviar.
/// Não carrega caminho, chave nem o tamanho em claro no <see cref="ToString"/>.
/// </summary>
public sealed record FotoComprovantePronta(byte[]? Jpeg, bool PedirOutraFoto, bool Cancelada)
{
    public static FotoComprovantePronta ComJpeg(byte[] jpeg) => new(jpeg, false, false);

    public static FotoComprovantePronta PedindoOutra() => new(null, true, false);

    public static FotoComprovantePronta SemFoto() => new(null, false, true);

    public static FotoComprovantePronta Ilegivel() => new(null, false, false);

    public override string ToString() =>
        PedirOutraFoto ? "PedirOutraFoto" :
        Cancelada ? "Cancelada" :
        Jpeg is null ? "FotoInvalida" :
        "FotoComprovantePronta";
}
