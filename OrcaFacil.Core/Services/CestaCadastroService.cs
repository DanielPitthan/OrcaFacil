using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Core.Services;

public class CestaCadastroService
{
    private readonly ICestaRepository _cestas;

    public CestaCadastroService(ICestaRepository cestas)
    {
        _cestas = cestas;
    }

    public Task<List<ProdutoCesta>> ListarProdutosAsync(bool somenteAtivos) => _cestas.ListarProdutosAsync(somenteAtivos);

    public async Task<ProdutoCesta> SalvarProdutoAsync(ProdutoCestaFormModel modelo)
    {
        var nome = NormalizarNome(modelo.Nome, "Informe o nome do produto.");
        if (modelo.CategoriaId <= 0)
            throw new InvalidOperationException("Selecione a categoria do produto.");

        if (await _cestas.ExisteNomeProdutoAsync(nome, modelo.Id))
            throw new InvalidOperationException("Já existe um produto com esse nome.");

        if (modelo.Id is null)
        {
            var novo = new ProdutoCesta { Nome = nome, CategoriaId = modelo.CategoriaId };
            await _cestas.AdicionarProdutoAsync(novo);
            return novo;
        }

        var produto = await _cestas.ObterProdutoAsync(modelo.Id.Value)
            ?? throw new InvalidOperationException("Produto não encontrado.");

        produto.Nome = nome;
        produto.CategoriaId = modelo.CategoriaId;
        produto.Ativo = modelo.Ativo;
        await _cestas.AtualizarProdutoAsync(produto);
        return produto;
    }

    public async Task ExcluirProdutoAsync(int id)
    {
        var produto = await _cestas.ObterProdutoAsync(id)
            ?? throw new InvalidOperationException("Produto não encontrado.");

        if (await _cestas.ProdutoPossuiComprasAsync(id))
            throw new InvalidOperationException("Este produto já entrou em compras e não pode ser excluído. Inative-o para ocultá-lo das próximas cestas.");

        await _cestas.ExcluirProdutoAsync(produto);
    }

    public async Task InativarProdutoAsync(int id)
    {
        var produto = await _cestas.ObterProdutoAsync(id)
            ?? throw new InvalidOperationException("Produto não encontrado.");

        produto.Ativo = false;
        await _cestas.AtualizarProdutoAsync(produto);
    }

    public Task<List<LocalCompra>> ListarLocaisAsync(bool somenteAtivos) => _cestas.ListarLocaisAsync(somenteAtivos);

    public async Task<LocalCompra> SalvarLocalAsync(LocalCompraFormModel modelo)
    {
        var nome = NormalizarNome(modelo.Nome, "Informe o nome do local.");
        if (await _cestas.ExisteNomeLocalAsync(nome, modelo.Id))
            throw new InvalidOperationException("Já existe um local com esse nome.");

        if (modelo.Id is null)
        {
            var novo = new LocalCompra { Nome = nome };
            await _cestas.AdicionarLocalAsync(novo);
            return novo;
        }

        var local = await _cestas.ObterLocalAsync(modelo.Id.Value)
            ?? throw new InvalidOperationException("Local não encontrado.");

        local.Nome = nome;
        local.Ativo = modelo.Ativo;
        await _cestas.AtualizarLocalAsync(local);
        return local;
    }

    public async Task ExcluirLocalAsync(int id)
    {
        var local = await _cestas.ObterLocalAsync(id)
            ?? throw new InvalidOperationException("Local não encontrado.");

        if (await _cestas.LocalPossuiComprasAsync(id))
            throw new InvalidOperationException("Este local já tem compras e não pode ser excluído. Inative-o para ocultá-lo das próximas cestas.");

        await _cestas.ExcluirLocalAsync(local);
    }

    public async Task InativarLocalAsync(int id)
    {
        var local = await _cestas.ObterLocalAsync(id)
            ?? throw new InvalidOperationException("Local não encontrado.");

        local.Ativo = false;
        await _cestas.AtualizarLocalAsync(local);
    }

    public Task<List<CompraCesta>> ListarComprasAsync(int? mes, int? ano) => _cestas.ListarComprasAsync(mes, ano);

    public Task<CompraCesta?> ObterCompraAsync(int id) => _cestas.ObterCompraAsync(id);

    public async Task<CompraCesta> SalvarCompraAsync(CompraCestaFormModel modelo, int membroFamiliaId)
    {
        if (modelo.Data == default)
            throw new InvalidOperationException("Informe a data da compra.");

        var local = await _cestas.ObterLocalAsync(modelo.LocalCompraId)
            ?? throw new InvalidOperationException("Selecione o local da compra.");

        if (!local.Ativo && modelo.Id is null)
            throw new InvalidOperationException("Este local está inativo.");

        if (modelo.Itens.Count == 0)
            throw new InvalidOperationException("Inclua ao menos um produto na compra.");

        if (modelo.Itens.GroupBy(i => i.ProdutoCestaId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Cada produto só pode aparecer uma vez na compra.");

        var produtos = (await _cestas.ListarProdutosAsync(false)).ToDictionary(p => p.Id);
        var precos = new List<ItemPrecoCategoria>();
        foreach (var item in modelo.Itens)
        {
            if (!produtos.TryGetValue(item.ProdutoCestaId, out var produto))
                throw new InvalidOperationException("Um dos produtos da compra não existe mais.");

            if (!produto.Ativo && modelo.Id is null)
                throw new InvalidOperationException($"O produto {produto.Nome} está inativo.");

            var preco = CestaDespesaRegras.Dinheiro(item.Preco);
            if (preco <= 0)
                throw new InvalidOperationException($"Informe o preço de {produto.Nome}.");

            precos.Add(new ItemPrecoCategoria { Nome = produto.Nome, CategoriaId = produto.CategoriaId, Preco = preco });
        }

        var beneficio = CestaDespesaRegras.Dinheiro(modelo.ValorBeneficio);
        var tipo = beneficio > 0 ? modelo.TipoCartaoBeneficio : null;
        var categoriaCestaId = await _cestas.ObterOuCriarCategoriaCestaAsync();
        var montada = CestaDespesaRegras.Montar(
            local.Nome, precos, beneficio, tipo, categoriaCestaId, modelo.ValorDinheiro, modelo.ValorCartaoCredito);

        var itensDespesa = montada.Itens.Select((item, indice) => new ItemLancamento
        {
            Descricao = item.Nome,
            Valor = item.Valor,
            Natureza = NaturezaItemLancamento.Avulsa,
            CategoriaId = item.CategoriaId,
            Ordem = indice
        }).ToList();

        if (itensDespesa.Count > 0)
            ItensFaturaRegras.Validar(montada.Valor, categoriaCestaId, permiteItens: true, TipoTransacao.Despesa, itensDespesa);

        var data = modelo.Data.Date;
        var compra = new CompraCesta
        {
            Id = modelo.Id ?? 0,
            LocalCompraId = local.Id,
            Data = data,
            Mes = data.Month,
            Ano = data.Year,
            ValorBeneficio = beneficio,
            TipoCartaoBeneficio = tipo,
            ValorDinheiro = montada.Dinheiro,
            ValorCartaoCredito = montada.Cartao,
            Itens = modelo.Itens.Select((item, indice) => new CompraCestaItem
            {
                ProdutoCestaId = item.ProdutoCestaId,
                Preco = CestaDespesaRegras.Dinheiro(item.Preco),
                Ordem = indice
            }).ToList()
        };

        Transacao? despesa = null;
        if (montada.Gerar)
        {
            despesa = new Transacao
            {
                Descricao = montada.Descricao,
                Valor = montada.Valor,
                Data = data,
                Tipo = TipoTransacao.Despesa,
                Recorrencia = TipoRecorrencia.Unica,
                Pago = true,
                Observacao = montada.Observacao,
                CategoriaId = categoriaCestaId,
                MembroFamiliaId = membroFamiliaId,
                Itens = itensDespesa
            };
        }

        return await _cestas.SalvarCompraAsync(compra, despesa);
    }

    public Task ExcluirCompraAsync(int id) => _cestas.ExcluirCompraAsync(id);

    private static string NormalizarNome(string nome, string mensagemVazio)
    {
        var texto = nome.Trim();
        if (string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException(mensagemVazio);

        if (texto.Length > 80)
            throw new InvalidOperationException("O nome pode ter no máximo 80 caracteres.");

        return texto;
    }
}
