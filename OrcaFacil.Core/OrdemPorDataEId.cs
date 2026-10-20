namespace OrcaFacil.Core;

/// <summary>
/// Ordem da renda variável: a data (sem hora) e, no mesmo dia, o Id.
/// Id 0 ainda não foi gravado e fica por último.
/// A renda fixa desempata o mesmo dia com a aplicação antes do resgate.
/// </summary>
public static class OrdemPorDataEId
{
    public static IOrderedEnumerable<T> Ordenar<T>(IEnumerable<T> itens, Func<T, DateTime> data, Func<T, int> id)
        => itens.OrderBy(item => data(item).Date).ThenBy(item => Chave(id(item)));

    public static int Comparar(DateTime data, int id, DateTime outraData, int outroId)
    {
        var porData = data.Date.CompareTo(outraData.Date);
        return porData != 0 ? porData : Chave(id).CompareTo(Chave(outroId));
    }

    /// <summary>Id gravado fica na frente. Zero é a operação nova, ainda sem chave.</summary>
    public static int Chave(int id) => id == 0 ? int.MaxValue : id;
}
