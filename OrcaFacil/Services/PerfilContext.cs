using Microsoft.Extensions.Logging;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class PerfilContext : IPerfilContext
{
    private readonly IMembroFamiliaRepository _membroFamiliaRepository;
    private readonly ILogger<PerfilContext> _logger;

    public MembroFamilia? Atual { get; private set; }

    public event Action? OnMudou;

    public PerfilContext(IMembroFamiliaRepository membroFamiliaRepository, ILogger<PerfilContext> logger)
    {
        _membroFamiliaRepository = membroFamiliaRepository;
        _logger = logger;
    }

    public async Task InicializarAsync()
    {
        var ativos = await _membroFamiliaRepository.GetAtivosAsync();
        Atual = ativos.OrderBy(m => m.Id).FirstOrDefault();

        if (Atual is null)
        {
            _logger.LogWarning("Nenhum perfil ativo encontrado; criando perfil padrão.");
            Atual = new MembroFamilia { Nome = "Usuário" };
            await _membroFamiliaRepository.AddAsync(Atual);
        }

        OnMudou?.Invoke();
    }

    public async Task SalvarAsync(string nome, string? apelido, string? fotoPath)
    {
        if (Atual is null)
            await InicializarAsync();

        var membro = await _membroFamiliaRepository.GetByIdAsync(Atual!.Id)
            ?? throw new InvalidOperationException("Perfil não encontrado.");

        membro.Nome = nome.Trim();
        membro.Apelido = string.IsNullOrWhiteSpace(apelido) ? null : apelido.Trim();
        membro.FotoPath = fotoPath;
        await _membroFamiliaRepository.UpdateAsync(membro);

        Atual = membro;
        OnMudou?.Invoke();
    }
}
