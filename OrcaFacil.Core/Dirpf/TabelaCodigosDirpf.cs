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

    public CodigoDirpf Exigir(TipoFichaDirpf ficha, TipoAtivo tipo, DateTime data)
    {
        var vigente = Vigente(ficha, tipo, data);
        if (vigente is null)
            throw new InvalidOperationException(
                $"Não há código DIRPF vigente em {data.Date:dd/MM/yyyy} para {ficha} e {tipo}.");

        return vigente;
    }

    public CodigoDirpf? Vigente(TipoFichaDirpf ficha, TipoAtivo tipo, DateTime data)
    {
        var dia = data.Date;
        return _codigos
            .Where(codigo => codigo.Ficha == ficha
                && codigo.TipoAtivo == tipo
                && codigo.VigenciaInicio.Date <= dia
                && (codigo.VigenciaFim is null || codigo.VigenciaFim.Value.Date >= dia))
            .OrderByDescending(codigo => codigo.VigenciaInicio)
            .ThenByDescending(codigo => codigo.Id)
            .FirstOrDefault();
    }
}
