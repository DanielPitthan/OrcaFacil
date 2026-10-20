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
    private readonly ResultadoRealizadoService _resultado;

    public OperacaoAtivoService(
        IOperacaoAtivoRepository operacoes,
        IPerfilContext perfil,
        ResultadoRealizadoService resultado)
    {
        _operacoes = operacoes;
        _perfil = perfil;
        _cadastro = new OperacaoCadastroService(operacoes);
        _resultado = resultado;
    }

    public Task<List<PosicaoAtivoDto>> ListarPosicaoAsync()
        => _cadastro.ListarPosicaoAsync(MembroId());

    public Task<List<OperacaoAtivo>> ListarPorAtivoAsync(int ativoId)
        => _operacoes.ListarPorAtivoAsync(ativoId, MembroId());

    public Task SalvarAsync(OperacaoAtivoFormModel modelo)
        => _resultado.SalvarAsync(modelo, MembroId());

    public Task ExcluirAsync(int id)
        => _resultado.ExcluirAsync(id, MembroId());

    private int MembroId()
        => _perfil.Atual?.Id ?? throw new InvalidOperationException("Nenhum perfil ativo.");
}
