using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class MetaService : IMetaService
{
    private readonly IMetaRepository _metaRepository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly ITransacaoService _transacaoService;

    public MetaService(
        IMetaRepository metaRepository,
        ICategoriaRepository categoriaRepository,
        ITransacaoService transacaoService)
    {
        _metaRepository = metaRepository;
        _categoriaRepository = categoriaRepository;
        _transacaoService = transacaoService;
    }

    public Task<List<Meta>> ListarAsync() => _metaRepository.GetAllAsync();

    public async Task<Meta> CriarAsync(MetaFormModel dto, int membroFamiliaId)
    {
        var meta = new Meta
        {
            Nome = dto.Nome,
            ValorAlvo = dto.ValorAlvo,
            ValorAtual = dto.ValorAtual,
            Prazo = dto.Prazo,
            Icone = dto.Icone,
            MembroFamiliaId = membroFamiliaId,
            Concluida = dto.ValorAtual >= dto.ValorAlvo
        };

        await _metaRepository.AddAsync(meta);
        return meta;
    }

    public async Task AtualizarAsync(int id, MetaFormModel dto)
    {
        var meta = await _metaRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Meta não encontrada.");

        meta.Nome = dto.Nome;
        meta.ValorAlvo = dto.ValorAlvo;
        meta.Prazo = dto.Prazo;
        meta.Icone = dto.Icone;
        meta.Concluida = meta.ValorAtual >= meta.ValorAlvo;

        await _metaRepository.UpdateAsync(meta);
    }

    public async Task ExcluirAsync(int id)
    {
        var meta = await _metaRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Meta não encontrada.");

        await _metaRepository.DeleteAsync(meta);
    }

    public async Task AdicionarContribuicaoAsync(int metaId, ContribuicaoMetaModel dto)
    {
        var meta = await _metaRepository.GetByIdAsync(metaId)
            ?? throw new InvalidOperationException("Meta não encontrada.");

        // O lançamento vem primeiro: se o mês estiver fechado, a meta não é alterada.
        if (dto.RegistrarComoLancamento)
        {
            await _transacaoService.CriarAsync(new TransacaoFormModel
            {
                Descricao = $"Contribuição — {meta.Nome}",
                Valor = dto.Valor,
                Data = DateTime.Today,
                Tipo = TipoTransacao.Despesa,
                Recorrencia = TipoRecorrencia.Unica,
                Observacao = "Contribuição para meta de economia",
                CategoriaId = await ObterOuCriarCategoriaInvestimentosAsync()
            });
        }

        meta.ValorAtual += dto.Valor;
        meta.Concluida = meta.ValorAtual >= meta.ValorAlvo;
        await _metaRepository.UpdateAsync(meta);
    }

    private async Task<int> ObterOuCriarCategoriaInvestimentosAsync()
    {
        var categorias = await _categoriaRepository.GetAtivasAsync();
        var investimentos = categorias.FirstOrDefault(c => c.Nome.Contains("Investimento", StringComparison.OrdinalIgnoreCase));
        if (investimentos is not null)
            return investimentos.Id;

        // Fallback: usa a primeira categoria ativa caso "Investimentos" não exista.
        var primeira = categorias.FirstOrDefault()
            ?? throw new InvalidOperationException("Nenhuma categoria cadastrada para associar a contribuição.");
        return primeira.Id;
    }
}
