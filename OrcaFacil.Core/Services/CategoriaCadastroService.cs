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
    /// Nome ativo continua único. Inativa do mesmo tipo é reativada no mesmo Id.
    /// Tipo diferente vira outra linha; a inativa e os lançamentos dela ficam como estão.
    /// </summary>
    public async Task<Categoria> CriarAsync(CategoriaFormModel dto)
    {
        var homonimas = await _categoriaRepository.ListarPorNomeAsync(dto.Nome);
        if (homonimas.Any(categoria => categoria.Ativo))
            throw new InvalidOperationException(MensagemNomeRepetido);

        var mesmoTipo = homonimas.FirstOrDefault(categoria => categoria.TipoPadrao == dto.TipoPadrao);
        if (mesmoTipo is not null)
        {
            await ReativarAsync(mesmoTipo, dto);
            return mesmoTipo;
        }

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

    private async Task ReativarAsync(Categoria categoria, CategoriaFormModel dto)
    {
        Aplicar(categoria, dto);
        if (!categoria.PermiteItensFatura && await _categoriaRepository.PossuiItensFaturaAsync(categoria.Id))
            categoria.PermiteItensFatura = true;

        categoria.Ativo = true;
        await _categoriaRepository.UpdateAsync(categoria);
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
