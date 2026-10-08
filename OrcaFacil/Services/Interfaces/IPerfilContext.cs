using OrcaFacil.Core.Entities;

namespace OrcaFacil.Services.Interfaces;

/// <summary>Mantém o perfil único do usuário (MembroFamilia) durante a sessão do app.</summary>
public interface IPerfilContext
{
    MembroFamilia? Atual { get; }
    event Action? OnMudou;
    Task InicializarAsync();
    Task SalvarAsync(string nome, string? apelido, string? fotoPath);
}
