using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class AtivoService : IAtivoService
{
    private readonly IAtivoFinanceiroRepository _ativos;
    private readonly AtivoCadastroService _cadastro;

    public AtivoService(
        IAtivoFinanceiroRepository ativos,
        IOperacaoAtivoRepository operacoes,
        IDbContextFactory<AppDbContext> contexto)
    {
        _ativos = ativos;
        _cadastro = new AtivoCadastroService(ativos, operacoes, contexto);
    }

    public Task<List<AtivoFinanceiro>> ListarAsync(bool somenteAtivos = false) => _ativos.ListarAsync(somenteAtivos);

    public Task<AtivoFinanceiro> SalvarAsync(AtivoFormModel modelo) => _cadastro.SalvarAsync(modelo);

    public Task ExcluirAsync(int id) => _cadastro.ExcluirAsync(id);

    public Task InativarAsync(int id) => _cadastro.InativarAsync(id);
}
