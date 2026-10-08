using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class CategoriaService : ICategoriaService
{
    private readonly ICategoriaRepository _categoriaRepository;

    public CategoriaService(ICategoriaRepository categoriaRepository)
    {
        _categoriaRepository = categoriaRepository;
    }

    public async Task<List<Categoria>> ListarAsync(bool somenteAtivas = true)
        => somenteAtivas ? await _categoriaRepository.GetAtivasAsync() : await _categoriaRepository.GetAllAsync();

    public Task<Categoria?> ObterAsync(int id) => _categoriaRepository.GetByIdAsync(id);

    public async Task<Categoria> CriarAsync(CategoriaFormModel dto)
    {
        if (await _categoriaRepository.ExisteNomeAsync(dto.Nome))
            throw new InvalidOperationException("Já existe uma categoria com esse nome.");

        var categoria = new Categoria
        {
            Nome = dto.Nome,
            Icone = dto.Icone,
            CorHex = dto.CorHex,
            TipoPadrao = dto.TipoPadrao,
            Ativo = true
        };

        await _categoriaRepository.AddAsync(categoria);
        return categoria;
    }

    public async Task AtualizarAsync(int id, CategoriaFormModel dto)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Categoria não encontrada.");

        if (await _categoriaRepository.ExisteNomeAsync(dto.Nome, id))
            throw new InvalidOperationException("Já existe uma categoria com esse nome.");

        categoria.Nome = dto.Nome;
        categoria.Icone = dto.Icone;
        categoria.CorHex = dto.CorHex;
        categoria.TipoPadrao = dto.TipoPadrao;

        await _categoriaRepository.UpdateAsync(categoria);
    }

    public async Task RemoverAsync(int id)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Categoria não encontrada.");

        var possuiTransacoes = await _categoriaRepository.PossuiTransacoesAsync(id);

        if (categoria.EhPadraoDoSistema || possuiTransacoes)
        {
            // Categorias padrão ou em uso não podem ser excluídas — apenas desativadas.
            categoria.Ativo = false;
            await _categoriaRepository.UpdateAsync(categoria);
            return;
        }

        await _categoriaRepository.DeleteAsync(categoria);
    }
}
