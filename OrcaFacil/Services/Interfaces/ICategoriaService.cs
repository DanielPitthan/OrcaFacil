using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Services.Interfaces;

public interface ICategoriaService
{
    Task<List<Categoria>> ListarAsync(bool somenteAtivas = true);
    Task<Categoria?> ObterAsync(int id);
    Task<Categoria> CriarAsync(CategoriaFormModel dto);
    Task AtualizarAsync(int id, CategoriaFormModel dto);
    Task RemoverAsync(int id);
}
