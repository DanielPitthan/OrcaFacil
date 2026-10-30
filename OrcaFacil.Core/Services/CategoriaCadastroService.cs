using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Core.Services;

public class CategoriaCadastroService
{
    public const string MensagemNomeRepetido = "Já existe uma categoria com esse nome.";

    private readonly ICategoriaRepository _categoriaRepository;

    public CategoriaCadastroService(ICategoriaRepository categoriaRepository)
    {
        _categoriaRepository = categoriaRepository;
    }

    /// <summary>
    /// Se o nome é de uma categoria inativa, reativa essa linha. O índice único de nome
    /// também vale para inativa, então uma segunda linha com o mesmo nome não grava.
    /// </summary>
    public async Task<Categoria> CriarAsync(CategoriaFormModel dto)
    {
        var existente = await _categoriaRepository.GetByNomeAsync(dto.Nome);
        if (existente is { Ativo: false })
        {
            Aplicar(existente, dto);
            existente.Ativo = true;
            await _categoriaRepository.UpdateAsync(existente);
            return existente;
        }

        if (existente is not null)
            throw new InvalidOperationException(MensagemNomeRepetido);

        var categoria = new Categoria { Ativo = true };
        Aplicar(categoria, dto);
        await _categoriaRepository.AddAsync(categoria);
        return categoria;
    }

    public async Task AtualizarAsync(int id, CategoriaFormModel dto)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Categoria não encontrada.");

        if (await _categoriaRepository.ExisteNomeAsync(dto.Nome, id))
            throw new InvalidOperationException(MensagemNomeRepetido);

        Aplicar(categoria, dto);
        await _categoriaRepository.UpdateAsync(categoria);
    }

    public async Task RemoverAsync(int id)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Categoria não encontrada.");

        var possuiTransacoes = await _categoriaRepository.PossuiTransacoesAsync(id);

        if (categoria.EhPadraoDoSistema || possuiTransacoes)
        {
            categoria.Ativo = false;
            await _categoriaRepository.UpdateAsync(categoria);
            return;
        }

        await _categoriaRepository.DeleteAsync(categoria);
    }

    private static void Aplicar(Categoria categoria, CategoriaFormModel dto)
    {
        categoria.Nome = dto.Nome;
        categoria.Icone = dto.Icone;
        categoria.CorHex = dto.CorHex;
        categoria.TipoPadrao = dto.TipoPadrao;
        categoria.PermiteItensFatura = dto.TipoPadrao == TipoTransacao.Despesa && dto.PermiteItensFatura;
    }
}
