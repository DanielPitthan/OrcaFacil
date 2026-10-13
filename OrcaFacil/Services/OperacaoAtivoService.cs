using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class OperacaoAtivoService : IOperacaoAtivoService
{
    private readonly IOperacaoAtivoRepository _operacoes;
    private readonly IPerfilContext _perfil;
    private readonly OperacaoCadastroService _cadastro;

    public OperacaoAtivoService(
        IOperacaoAtivoRepository operacoes,
        IAtivoFinanceiroRepository ativos,
        IPerfilContext perfil)
    {
        _operacoes = operacoes;
        _perfil = perfil;
        _cadastro = new OperacaoCadastroService(operacoes, ativos);
    }

    public Task<List<PosicaoAtivoDto>> ListarPosicaoAsync()
        => _cadastro.ListarPosicaoAsync(MembroId());

    public Task<List<OperacaoAtivo>> ListarPorAtivoAsync(int ativoId)
        => _operacoes.ListarPorAtivoAsync(ativoId, MembroId());

    public Task SalvarAsync(OperacaoAtivoFormModel modelo)
        => _cadastro.SalvarAsync(modelo, MembroId());

    public Task ExcluirAsync(int id)
        => _cadastro.ExcluirAsync(id, MembroId());

    private int MembroId()
        => _perfil.Atual?.Id ?? throw new InvalidOperationException("Nenhum perfil ativo.");
}
