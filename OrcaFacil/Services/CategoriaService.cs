using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Exportacao;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;
using OrcaFacil.Services.Interfaces;
using Serilog;

namespace OrcaFacil.Services;

public class CategoriaService : ICategoriaService
{
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly CategoriaCadastroService _cadastro;

    public CategoriaService(ICategoriaRepository categoriaRepository)
    {
        _categoriaRepository = categoriaRepository;
        _cadastro = new CategoriaCadastroService(categoriaRepository);
    }

    public async Task<List<Categoria>> ListarAsync(bool somenteAtivas = true)
        => somenteAtivas ? await _categoriaRepository.GetAtivasAsync() : await _categoriaRepository.GetAllAsync();

    public Task<Categoria?> ObterAsync(int id) => _categoriaRepository.GetByIdAsync(id);

    public Task<Categoria> CriarAsync(CategoriaFormModel dto)
        => ExecutarAsync(() => _cadastro.CriarAsync(dto));

    public Task AtualizarAsync(int id, CategoriaFormModel dto)
        => ExecutarAsync(() => _cadastro.AtualizarAsync(id, dto));

    public Task RemoverAsync(int id) => ExecutarAsync(() => _cadastro.RemoverAsync(id));

    private static Task ExecutarAsync(Func<Task> acao)
        => ExecutarAsync(async () =>
        {
            await acao();
            return true;
        });

    private static async Task<T> ExecutarAsync<T>(Func<Task<T>> acao)
    {
        try
        {
            return await acao();
        }
        catch (DbUpdateException ex)
        {
            Log.Error(
                "Falha ao gravar a categoria. Tipo: {Tipo}",
                VarreduraPlanilhaExportada.TipoDaFalha(ex));
            throw FalhaDeGravacao.ParaOUsuario(ex, FalhaDeGravacao.MensagemCategoria);
        }
    }
}
