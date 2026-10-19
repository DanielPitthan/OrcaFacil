using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Llm;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Core.Services;

public class CestaCadastroService
{
    public const string MensagemNomeProdutoRepetido = "Já existe um produto com esse nome.";

    public const string MensagemProdutoRepetidoNaCompra = "Cada produto só pode aparecer uma vez na compra.";

    private readonly ICestaRepository _cestas;
    private readonly ICartaoRepository? _cartoes;

    public CestaCadastroService(ICestaRepository cestas, ICartaoRepository? cartoes = null)
    {
        _cestas = cestas;
        _cartoes = cartoes;
    }

    public Task<List<ProdutoCesta>> ListarProdutosAsync(bool somenteAtivos) => _cestas.ListarProdutosAsync(somenteAtivos);

    public async Task<ProdutoCesta> SalvarProdutoAsync(ProdutoCestaFormModel modelo)
    {
        var nome = NormalizarNome(modelo.Nome, "Informe o nome do produto.");
        if (modelo.CategoriaId <= 0)
            throw new InvalidOperationException("Selecione a categoria do produto.");

        if (await _cestas.ExisteNomeProdutoAsync(nome, modelo.Id))
            throw new InvalidOperationException(MensagemNomeProdutoRepetido);

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

    public async Task<ResultadoImportacaoProdutos> ImportarProdutosAsync(
        IReadOnlyList<ProdutoCestaCsv.Linha> linhas,
        IReadOnlyList<Categoria> categorias,
        int? categoriaPadraoId)
    {
        var mapa = categorias
            .Where(c => !string.IsNullOrWhiteSpace(c.Nome))
            .GroupBy(c => c.Nome.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var erros = new List<string>();
        var importados = 0;
        var ignorados = 0;
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var linha in linhas)
        {
            var categoriaId = categoriaPadraoId;
            if (!string.IsNullOrWhiteSpace(linha.Categoria))
            {
                if (!mapa.TryGetValue(linha.Categoria.Trim(), out var encontrada))
                {
                    erros.Add($"Linha {linha.Numero}: a categoria \"{linha.Categoria}\" não existe.");
                    continue;
                }

                categoriaId = encontrada;
            }

            if (categoriaId is null or <= 0)
            {
                erros.Add($"Linha {linha.Numero}: informe a categoria de {linha.Nome}.");
                continue;
            }

            if (!vistos.Add(linha.Nome) || await _cestas.ExisteNomeProdutoAsync(linha.Nome))
            {
                ignorados++;
                continue;
            }

            await _cestas.AdicionarProdutoAsync(new ProdutoCesta { Nome = linha.Nome, CategoriaId = categoriaId.Value });
            importados++;
        }

        return new ResultadoImportacaoProdutos(importados, ignorados, erros);
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

        var produtos = (await _cestas.ListarProdutosAsync(false)).OrderBy(p => p.Id).ToList();
        var porId = produtos.ToDictionary(p => p.Id);
        var porChave = new Dictionary<string, ProdutoCesta>(StringComparer.Ordinal);
        foreach (var cadastrado in produtos)
        {
            var chaveCadastrada = NormalizadorComprovante.TextoComparacao(cadastrado.Nome);
            if (chaveCadastrada.Length > 0)
                porChave.TryAdd(chaveCadastrada, cadastrado);
        }

        var definicoes = modelo.ProdutosNovos
            .GroupBy(p => p.Chave, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var resolvidos = new List<ItemResolvido>();
        foreach (var item in modelo.Itens)
        {
            var preco = CestaDespesaRegras.Dinheiro(item.Preco);
            if (!string.IsNullOrEmpty(item.ProdutoNovoChave))
            {
                if (!definicoes.TryGetValue(item.ProdutoNovoChave, out var definicao))
                    throw new InvalidOperationException("Um dos produtos da compra não existe mais.");

                var nome = definicao.Nome.Trim();
                if (string.IsNullOrWhiteSpace(nome))
                    throw new InvalidOperationException("Informe o nome do produto.");

                if (definicao.CategoriaId <= 0)
                    throw new InvalidOperationException("Selecione a categoria do produto.");

                var chave = NormalizadorComprovante.TextoComparacao(nome);
                if (porChave.TryGetValue(chave, out var existente) && existente.Id > 0)
                {
                    if (!existente.Ativo && modelo.Id is null)
                        throw new InvalidOperationException($"O produto {existente.Nome} está inativo.");

                    if (preco <= 0)
                        throw new InvalidOperationException($"Informe o preço de {existente.Nome}.");

                    resolvidos.Add(new ItemResolvido
                    {
                        Identidade = "id:" + existente.Id,
                        Preco = preco,
                        Nome = existente.Nome,
                        CategoriaId = existente.CategoriaId,
                        ProdutoCestaId = existente.Id
                    });
                    continue;
                }

                if (preco <= 0)
                    throw new InvalidOperationException($"Informe o preço de {nome}.");

                porChave[chave] = new ProdutoCesta { Nome = nome, CategoriaId = definicao.CategoriaId };
                resolvidos.Add(new ItemResolvido
                {
                    Identidade = "novo:" + chave,
                    Preco = preco,
                    Nome = nome,
                    CategoriaId = definicao.CategoriaId,
                    Novo = new ProdutoNovoCompra
                    {
                        Chave = chave,
                        Nome = nome,
                        CategoriaId = definicao.CategoriaId
                    }
                });
                continue;
            }

            if (!porId.TryGetValue(item.ProdutoCestaId, out var produto))
                throw new InvalidOperationException("Um dos produtos da compra não existe mais.");

            if (!produto.Ativo && modelo.Id is null)
                throw new InvalidOperationException($"O produto {produto.Nome} está inativo.");

            if (preco <= 0)
                throw new InvalidOperationException($"Informe o preço de {produto.Nome}.");

            resolvidos.Add(new ItemResolvido
            {
                Identidade = "id:" + produto.Id,
                Preco = preco,
                Nome = produto.Nome,
                CategoriaId = produto.CategoriaId,
                ProdutoCestaId = produto.Id
            });
        }

        // CompraCestaItem não tem quantidade, e o índice (CompraCestaId, ProdutoCestaId) é único.
        // As linhas continuam separadas na prévia; aqui o preço gravado é a soma dos totais.
        var fundidos = new List<ItemResolvido>();
        foreach (var grupo in resolvidos.GroupBy(item => item.Identidade, StringComparer.Ordinal))
        {
            var primeiro = grupo.First();
            decimal total = 0;
            foreach (var item in grupo)
                total = CestaDespesaRegras.Dinheiro(total + item.Preco);

            primeiro.Preco = total;
            fundidos.Add(primeiro);
        }

        if (fundidos.GroupBy(item => item.Identidade, StringComparer.Ordinal).Any(grupo => grupo.Count() > 1))
            throw new InvalidOperationException(MensagemProdutoRepetidoNaCompra);

        var precos = fundidos
            .Select(item => new ItemPrecoCategoria { Nome = item.Nome, CategoriaId = item.CategoriaId, Preco = item.Preco })
            .ToList();
        var vinculos = new List<ProdutoNovoCompra>();
        for (var indice = 0; indice < fundidos.Count; indice++)
        {
            if (fundidos[indice].Novo is not ProdutoNovoCompra novo)
                continue;

            novo.IndiceItem = indice;
            vinculos.Add(novo);
        }

        var beneficio = CestaDespesaRegras.Dinheiro(modelo.ValorBeneficio);
        var tipo = beneficio > 0 ? modelo.TipoCartaoBeneficio : null;
        int? cartaoBeneficioId = null;

        if (beneficio > 0)
        {
            if (modelo.CartaoBeneficioId is int cbId && cbId > 0)
            {
                if (_cartoes is null)
                    throw new InvalidOperationException("Repositório de cartões não configurado.");

                var cartaoBeneficio = await _cartoes.ObterAsync(cbId)
                    ?? throw new InvalidOperationException("Selecione o cartão de benefício.");
                if (cartaoBeneficio.Tipo != TipoCartao.Beneficio)
                    throw new InvalidOperationException("O cartão selecionado não é um cartão de benefício.");

                var totalRecargas = await _cartoes.ObterTotalRecargasAsync(cbId);
                var totalUsado = await _cartoes.ObterTotalUsadoBeneficioAsync(cbId, modelo.Id);
                var saldoDisponivel = CestaDespesaRegras.Dinheiro(totalRecargas - totalUsado);

                if (beneficio > saldoDisponivel)
                    throw new InvalidOperationException($"Saldo insuficiente no cartão {cartaoBeneficio.Nome} (disponível: {saldoDisponivel:C}). Recarregue o cartão antes de usar esse valor.");

                cartaoBeneficioId = cbId;
                tipo = cartaoBeneficio.TipoBeneficio ?? tipo ?? TipoCartaoBeneficio.ValeAlimentacao;
            }
            else if (_cartoes is not null)
            {
                throw new InvalidOperationException("Selecione o cartão de benefício.");
            }
        }

        var categoriaCestaId = await _cestas.ObterOuCriarCategoriaCestaAsync();
        var montada = CestaDespesaRegras.Montar(
            local.Nome, precos, beneficio, tipo, categoriaCestaId, modelo.ValorDinheiro, modelo.ValorCartaoCredito);

        int? cartaoCreditoId = null;
        if (montada.Cartao > 0)
        {
            if (modelo.CartaoCreditoId is int ccId && ccId > 0)
            {
                if (_cartoes is null)
                    throw new InvalidOperationException("Repositório de cartões não configurado.");

                var cartaoCredito = await _cartoes.ObterAsync(ccId)
                    ?? throw new InvalidOperationException("Selecione o cartão de crédito.");
                if (cartaoCredito.Tipo != TipoCartao.Credito)
                    throw new InvalidOperationException("O cartão selecionado não é um cartão de crédito.");

                cartaoCreditoId = ccId;
            }
            else if (_cartoes is not null)
            {
                throw new InvalidOperationException("Selecione o cartão de crédito.");
            }
        }

        var itensDespesa = montada.Itens.Select((item, indice) => new ItemLancamento
        {
            Descricao = item.Nome,
            Valor = item.Valor,
            Natureza = NaturezaItemLancamento.Avulsa,
            CategoriaId = item.CategoriaId,
            Ordem = indice
        }).ToList();

        if (montada.Gerar && itensDespesa.Count > 0)
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
            CartaoBeneficioId = cartaoBeneficioId,
            ValorDinheiro = montada.Dinheiro,
            ValorCartaoCredito = montada.Cartao,
            CartaoCreditoId = cartaoCreditoId,
            Itens = fundidos.Select((item, indice) => new CompraCestaItem
            {
                ProdutoCestaId = item.ProdutoCestaId,
                Preco = item.Preco,
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

        FaturaCreditoCesta? faturaCredito = null;
        if (montada.Cartao > 0 && cartaoCreditoId is int idCartaoCredito)
        {
            faturaCredito = new FaturaCreditoCesta(
                idCartaoCredito,
                modelo.FaturaTransacaoId,
                montada.Cartao,
                montada.Descricao,
                categoriaCestaId,
                data,
                membroFamiliaId);
        }

        try
        {
            return await _cestas.SalvarCompraAsync(compra, despesa, faturaCredito, vinculos);
        }
        catch (DbUpdateException ex) when (NomeDeProdutoRepetido(ex))
        {
            throw new InvalidOperationException(MensagemNomeProdutoRepetido);
        }
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

    private static bool NomeDeProdutoRepetido(DbUpdateException ex)
    {
        for (Exception? atual = ex; atual is not null; atual = atual.InnerException)
        {
            if (atual.Message.Contains("ProdutosCesta.Nome", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private sealed class ItemResolvido
    {
        public required string Identidade { get; init; }

        public decimal Preco { get; set; }

        public required string Nome { get; init; }

        public required int CategoriaId { get; init; }

        public int ProdutoCestaId { get; init; }

        public ProdutoNovoCompra? Novo { get; init; }
    }
}
