using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dirpf;

/// <summary>Escolhe, para a data, a vigência mais recente do par ficha e tipo. Sem linha, a ficha de bens recusa.</summary>
public sealed class TabelaCodigosDirpf
{
    private readonly IReadOnlyList<CodigoDirpf> _codigos;

    public TabelaCodigosDirpf(IEnumerable<CodigoDirpf> codigos)
    {
        _codigos = codigos.ToList();
    }

    public CodigoDirpf Exigir(TipoFichaDirpf ficha, TipoAtivo tipo, DateTime data, string variante = "")
    {
        var vigente = Vigente(ficha, tipo, data, variante);
        if (vigente is null)
            throw new InvalidOperationException(
                variante.Length == 0
                    ? $"Não há código DIRPF vigente em {data.Date:dd/MM/yyyy} para {ficha} e {tipo}."
                    : $"Não há código DIRPF vigente em {data.Date:dd/MM/yyyy} para {ficha}, {tipo} e {variante}.");

        return vigente;
    }

    public CodigoDirpf? Vigente(TipoFichaDirpf ficha, TipoAtivo tipo, DateTime data, string variante = "")
    {
        var dia = data.Date;
        return _codigos
            .Where(codigo => codigo.Ficha == ficha
                && codigo.TipoAtivo == tipo
                && string.Equals(codigo.Variante ?? "", variante, StringComparison.Ordinal)
                && codigo.VigenciaInicio.Date <= dia
                && (codigo.VigenciaFim is null || codigo.VigenciaFim.Value.Date >= dia))
            .OrderByDescending(codigo => codigo.VigenciaInicio)
            .ThenByDescending(codigo => codigo.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// Código cuja chave é a variante, não o tipo do papel.
    /// JCP e renda fixa usam isto: a coluna TipoAtivo da semente só completa o índice.
    /// </summary>
    public CodigoDirpf ExigirVariante(TipoFichaDirpf ficha, string variante, DateTime data)
    {
        var dia = data.Date;
        var vigente = _codigos
            .Where(codigo => codigo.Ficha == ficha
                && string.Equals(codigo.Variante ?? "", variante, StringComparison.Ordinal)
                && codigo.VigenciaInicio.Date <= dia
                && (codigo.VigenciaFim is null || codigo.VigenciaFim.Value.Date >= dia))
            .OrderByDescending(codigo => codigo.VigenciaInicio)
            .ThenByDescending(codigo => codigo.Id)
            .FirstOrDefault();

        if (vigente is null)
            throw new InvalidOperationException(
                $"Não há código DIRPF vigente em {dia:dd/MM/yyyy} para {ficha} e {variante}.");

        return vigente;
    }
}
