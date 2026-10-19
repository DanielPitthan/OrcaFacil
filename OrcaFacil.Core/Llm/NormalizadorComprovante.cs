using System.Globalization;
using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Casa a resposta lida com o catálogo. Não escolhe sozinho quando mais de um
/// produto casa, não soma preço que não é positivo e não descarta item.
/// Quantidade e unidade ficam só como texto de apoio.
/// </summary>
public static class NormalizadorComprovante
{
    public const int TamanhoMaximoNome = 80;

    public const string PrefixoNaoIdentificado = "Não identificado: ";

    public const string MarcaConfirmacao = "a confirmar";

    public const string AvisoLeituraIncerta = "leitura incerta";

    public const string MotivoPrecoInvalido = "O preço precisa ser maior que zero.";

    public const string MotivoProdutoInativo = "O produto está inativo.";

    public const string MotivoAmbiguo = "Mais de um produto do catálogo casa com esse nome.";

    public const string MotivoItemAusente = "O item veio vazio.";

    public const string MotivoSomaExcedida = "A soma dos preços não cabe.";

    /// <summary>
    /// Único interruptor da leitura incerta. Falso volta a permitir gravar
    /// cupom ilegível sem a confirmação da prévia.
    /// </summary>
    public const bool ExigirConfirmacaoLeituraIncerta = true;

    public static ComprovanteNormalizado Normalizar(
        ComprovanteLido comprovante,
        IReadOnlyList<ProdutoCesta> catalogo,
        bool leituraIncertaConfirmada = false)
    {
        ArgumentNullException.ThrowIfNull(comprovante);
        ArgumentNullException.ThrowIfNull(catalogo);

        var indice = CatalogoNomes.Criar(catalogo);
        var ordem = new List<string>();
        var porChave = new Dictionary<string, LinhaEmEdicao>(StringComparer.OrdinalIgnoreCase);
        var avulsos = 0;

        foreach (var item in comprovante.Itens ?? [])
        {
            var linha = item is null ? Ausente() : Classificar(item, indice);
            var chave = linha.Separada ? $"x:{avulsos++}" : linha.Chave;

            if (!linha.Separada && porChave.TryGetValue(chave, out var existente))
            {
                Juntar(existente, linha);
                continue;
            }

            porChave.Add(chave, linha);
            ordem.Add(chave);
        }

        var linhas = ordem.Select(chave => porChave[chave].Concluir()).ToList();
        var (soma, somaExcedida) = Somar(linhas);

        return new ComprovanteNormalizado
        {
            Loja = comprovante.Loja,
            Data = comprovante.Data,
            Legivel = comprovante.Legivel,
            LeituraIncertaConfirmada = leituraIncertaConfirmada,
            TotalCupom = comprovante.TotalCupom is decimal total
                ? CestaDespesaRegras.Dinheiro(total)
                : null,
            SomaLinhas = soma,
            SomaExcedida = somaExcedida,
            Linhas = linhas
        };
    }

    private static void Juntar(LinhaEmEdicao existente, LinhaEmEdicao nova)
    {
        if (!TentarSomar(existente.Preco, nova.Preco, out var soma))
        {
            existente.SomaExcedida = true;
            Adicionar(existente.Avisos, MotivoSomaExcedida);
        }
        else
        {
            existente.Preco = soma;
        }

        if (nova.NomeExcedeLimite)
            existente.NomeExcedeLimite = true;
        if (nova.Revisar)
            existente.Revisar = true;
    }

    private static (decimal Soma, bool Excedida) Somar(List<LinhaComprovanteNormalizada> linhas)
    {
        decimal soma = 0;
        foreach (var linha in linhas)
        {
            if (!ContaNaSoma(linha))
                continue;

            if (!TentarSomar(soma, linha.Preco, out soma))
                return (soma, true);
        }

        return (soma, false);
    }

    private static bool ContaNaSoma(LinhaComprovanteNormalizada linha) =>
        !linha.PrecoInvalido && !linha.SomaExcedida && linha.Preco > 0;

    private static bool TentarSomar(decimal atual, decimal extra, out decimal soma)
    {
        try
        {
            soma = CestaDespesaRegras.Dinheiro(atual + extra);
            return true;
        }
        catch (OverflowException)
        {
            soma = atual;
            return false;
        }
    }

    private static LinhaEmEdicao Classificar(ItemComprovanteLido item, CatalogoNomes catalogo)
    {
        var descricao = (item.DescricaoLida ?? string.Empty).Trim();
        var produto = (item.Produto ?? string.Empty).Trim();
        // O preço da linha já vem com o desconto aplicado.
        var preco = CestaDespesaRegras.Dinheiro(item.Preco);
        var precoInvalido = preco <= 0;

        if (produto.Length > 0)
        {
            var peloProduto = catalogo.Buscar(produto);
            if (peloProduto.Ambiguo)
                return Ambiguo(item, descricao, produto, preco, precoInvalido);

            if (peloProduto.Unico is ProdutoCesta casado)
                return DoCatalogo(item, descricao, produto, casado, preco, precoInvalido);

            var pelaDescricao = catalogo.Buscar(descricao);
            if (pelaDescricao.Unico is ProdutoCesta unico)
                return DoCatalogo(item, descricao, descricao, unico, preco, precoInvalido, casadoPelaDescricao: true);

            // Descrição ambígua ou sem candidato: nome novo, com o texto lido. Quem confirma é o usuário.
            return NomeNovo(item, descricao, produto, preco, precoInvalido);
        }

        var match = catalogo.Buscar(descricao);
        if (match.Ambiguo)
            return Ambiguo(item, descricao, descricao, preco, precoInvalido);

        if (match.Unico is ProdutoCesta encontrado)
            return DoCatalogo(item, descricao, descricao, encontrado, preco, precoInvalido);

        return NaoIdentificada(item, descricao, preco, precoInvalido);
    }

    private static LinhaEmEdicao DoCatalogo(
        ItemComprovanteLido item,
        string descricao,
        string nomeLido,
        ProdutoCesta produto,
        decimal preco,
        bool precoInvalido,
        bool casadoPelaDescricao = false)
    {
        var avisos = new List<string>();
        if (precoInvalido)
            avisos.Add(MotivoPrecoInvalido);
        if (!produto.Ativo)
            avisos.Add(MotivoProdutoInativo);

        var semCategoria = produto.CategoriaId <= 0;
        return new LinhaEmEdicao
        {
            Chave = $"p:{produto.Id}",
            DescricaoLida = descricao,
            Quantidade = Apoio(item.Quantidade),
            Unidade = Apoio(item.Unidade),
            Preco = preco,
            PrecoInvalido = precoInvalido,
            Separada = precoInvalido,
            Reconhecido = true,
            Revisar = !item.Reconhecido,
            ProdutoInativo = !produto.Ativo,
            CasadoPelaDescricao = casadoPelaDescricao,
            NomeExcedeLimite = nomeLido.Length > TamanhoMaximoNome,
            ProdutoCestaId = produto.Id,
            CategoriaId = semCategoria ? null : produto.CategoriaId,
            CategoriaFaltando = semCategoria,
            Nome = produto.Nome.Trim(),
            NomeLido = nomeLido,
            Avisos = avisos
        };
    }

    private static LinhaEmEdicao NomeNovo(
        ItemComprovanteLido item,
        string descricao,
        string produto,
        decimal preco,
        bool precoInvalido)
    {
        var avisos = new List<string>();
        if (precoInvalido)
            avisos.Add(MotivoPrecoInvalido);

        return new LinhaEmEdicao
        {
            Chave = "n:" + produto,
            DescricaoLida = descricao,
            Quantidade = Apoio(item.Quantidade),
            Unidade = Apoio(item.Unidade),
            Preco = preco,
            PrecoInvalido = precoInvalido,
            Separada = precoInvalido,
            Reconhecido = true,
            NomeNovo = true,
            CategoriaFaltando = true,
            NomeExcedeLimite = produto.Length > TamanhoMaximoNome,
            Nome = Cortar(produto),
            NomeLido = produto,
            Avisos = avisos
        };
    }

    private static LinhaEmEdicao NaoIdentificada(
        ItemComprovanteLido item,
        string descricao,
        decimal preco,
        bool precoInvalido)
    {
        var avisos = new List<string>();
        if (precoInvalido)
            avisos.Add(MotivoPrecoInvalido);

        return new LinhaEmEdicao
        {
            Chave = "u:" + descricao,
            DescricaoLida = descricao,
            Quantidade = Apoio(item.Quantidade),
            Unidade = Apoio(item.Unidade),
            Preco = preco,
            PrecoInvalido = precoInvalido,
            Separada = precoInvalido,
            NaoIdentificado = true,
            CategoriaFaltando = true,
            NomeExcedeLimite = descricao.Length > TamanhoMaximoNome,
            Nome = Cortar(descricao),
            NomeLido = descricao,
            Avisos = avisos
        };
    }

    private static LinhaEmEdicao Ambiguo(
        ItemComprovanteLido item,
        string descricao,
        string nomeLido,
        decimal preco,
        bool precoInvalido)
    {
        var avisos = new List<string> { MotivoAmbiguo };
        if (precoInvalido)
            avisos.Add(MotivoPrecoInvalido);

        return new LinhaEmEdicao
        {
            Chave = "a:" + nomeLido,
            DescricaoLida = descricao,
            Quantidade = Apoio(item.Quantidade),
            Unidade = Apoio(item.Unidade),
            Preco = preco,
            PrecoInvalido = precoInvalido,
            Separada = precoInvalido,
            Ambiguo = true,
            CategoriaFaltando = true,
            NomeExcedeLimite = nomeLido.Length > TamanhoMaximoNome,
            Nome = Cortar(nomeLido),
            NomeLido = nomeLido,
            Avisos = avisos
        };
    }

    private static LinhaEmEdicao Ausente() => new()
    {
        Chave = "ausente",
        DescricaoLida = string.Empty,
        Preco = 0,
        PrecoInvalido = true,
        Separada = true,
        ItemAusente = true,
        CategoriaFaltando = true,
        Nome = string.Empty,
        NomeLido = string.Empty,
        Avisos = [MotivoItemAusente]
    };

    private static void Adicionar(List<string> avisos, string aviso)
    {
        if (!avisos.Contains(aviso))
            avisos.Add(aviso);
    }

    private static string? Apoio(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        return texto.Trim();
    }

    public static string Cortar(string nome)
    {
        if (nome.Length <= TamanhoMaximoNome)
            return nome;

        var limites = StringInfo.ParseCombiningCharacters(nome);
        var corte = 0;
        for (var i = 0; i < limites.Length; i++)
        {
            var fim = i + 1 < limites.Length ? limites[i + 1] : nome.Length;
            if (fim > TamanhoMaximoNome)
                break;

            corte = fim;
        }

        return nome[..corte];
    }

    private readonly struct BuscaCatalogo
    {
        public ProdutoCesta? Unico { get; init; }
        public bool Ambiguo { get; init; }
    }

    private sealed class CatalogoNomes
    {
        private readonly Dictionary<string, List<ProdutoCesta>> _porNome;

        private CatalogoNomes(Dictionary<string, List<ProdutoCesta>> porNome) => _porNome = porNome;

        public static CatalogoNomes Criar(IReadOnlyList<ProdutoCesta> catalogo)
        {
            var porNome = new Dictionary<string, List<ProdutoCesta>>(StringComparer.OrdinalIgnoreCase);
            foreach (var produto in catalogo)
            {
                if (produto is null || string.IsNullOrWhiteSpace(produto.Nome))
                    continue;

                var nome = produto.Nome.Trim();
                if (!porNome.TryGetValue(nome, out var lista))
                {
                    lista = [];
                    porNome.Add(nome, lista);
                }

                lista.Add(produto);
            }

            return new CatalogoNomes(porNome);
        }

        public BuscaCatalogo Buscar(string nome)
        {
            if (nome.Length == 0 || !_porNome.TryGetValue(nome, out var lista) || lista.Count == 0)
                return default;

            if (lista.Count == 1)
                return new BuscaCatalogo { Unico = lista[0] };

            return new BuscaCatalogo { Ambiguo = true };
        }
    }

    private sealed class LinhaEmEdicao
    {
        public required string Chave { get; init; }
        public required string DescricaoLida { get; init; }
        public string? Quantidade { get; init; }
        public string? Unidade { get; init; }
        public decimal Preco { get; set; }
        public bool PrecoInvalido { get; init; }
        public bool Separada { get; init; }
        public bool SomaExcedida { get; set; }
        public bool Reconhecido { get; init; }
        public bool Revisar { get; set; }
        public bool NaoIdentificado { get; init; }
        public bool NomeNovo { get; init; }
        public bool Ambiguo { get; init; }
        public bool ProdutoInativo { get; init; }
        public bool ItemAusente { get; init; }
        public bool CasadoPelaDescricao { get; init; }
        public bool CategoriaFaltando { get; init; }
        public bool NomeExcedeLimite { get; set; }
        public int? ProdutoCestaId { get; init; }
        public int? CategoriaId { get; init; }
        public required string Nome { get; init; }
        public required string NomeLido { get; init; }
        public List<string> Avisos { get; init; } = [];

        public LinhaComprovanteNormalizada Concluir() => new()
        {
            DescricaoLida = DescricaoLida,
            Quantidade = Quantidade,
            Unidade = Unidade,
            Preco = Preco,
            PrecoInvalido = PrecoInvalido,
            SomaExcedida = SomaExcedida,
            Reconhecido = Reconhecido,
            Revisar = Revisar,
            NaoIdentificado = NaoIdentificado,
            NomeNovo = NomeNovo,
            Ambiguo = Ambiguo,
            ProdutoInativo = ProdutoInativo,
            ItemAusente = ItemAusente,
            CasadoPelaDescricao = CasadoPelaDescricao,
            CategoriaFaltando = CategoriaFaltando,
            NomeExcedeLimite = NomeExcedeLimite,
            ProdutoCestaId = ProdutoCestaId,
            CategoriaId = CategoriaId,
            Nome = Nome,
            NomeLido = NomeLido,
            Avisos = Avisos
        };
    }
}

/// <summary>
/// Leitura pronta para a prévia. <see cref="TotalCupom"/> e <see cref="SomaLinhas"/>
/// saem juntos para a tela mostrar a divergência. Preço não positivo fica de fora da soma.
/// </summary>
public sealed record ComprovanteNormalizado
{
    public string? Loja { get; init; }
    public DateOnly? Data { get; init; }
    public bool Legivel { get; init; }
    public bool LeituraIncertaConfirmada { get; init; }
    public decimal? TotalCupom { get; init; }
    public decimal SomaLinhas { get; init; }
    public bool SomaExcedida { get; init; }
    public IReadOnlyList<LinhaComprovanteNormalizada> Linhas { get; init; } = [];

    public bool TotalDivergente => TotalCupom is decimal total && total != SomaLinhas;

    public string? Aviso
    {
        get
        {
            var avisos = new List<string>();
            if (NormalizadorComprovante.ExigirConfirmacaoLeituraIncerta && !Legivel && !LeituraIncertaConfirmada)
                avisos.Add(NormalizadorComprovante.AvisoLeituraIncerta);
            if (SomaExcedida)
                avisos.Add(NormalizadorComprovante.MotivoSomaExcedida);

            return avisos.Count == 0 ? null : string.Join(" ", avisos);
        }
    }

    /// <summary>
    /// Só grava com toda linha pronta. Cupom ilegível espera a confirmação da prévia.
    /// </summary>
    public bool PodeGravar =>
        Aviso is null && Linhas.Count > 0 && Linhas.All(linha => linha.ProntaParaGravar);

    public ComprovanteNormalizado ConfirmarLeituraIncerta() =>
        this with { LeituraIncertaConfirmada = true };

    public override string ToString() => "ComprovanteNormalizado";
}

/// <summary>
/// Uma linha do comprovante. <see cref="Rotulo"/> é só exibição.
/// O que a prévia grava é <see cref="Nome"/>, sem o prefixo de não identificado.
/// </summary>
public sealed record LinhaComprovanteNormalizada
{
    public string DescricaoLida { get; init; } = string.Empty;

    public string? Quantidade { get; init; }

    public string? Unidade { get; init; }

    public decimal Preco { get; init; }

    public bool PrecoInvalido { get; init; }

    public bool SomaExcedida { get; init; }

    public bool Reconhecido { get; init; }

    /// <summary>Casou com o catálogo mesmo com <c>reconhecido</c> falso. Não bloqueia a gravação.</summary>
    public bool Revisar { get; init; }

    public bool NaoIdentificado { get; init; }

    public bool NomeNovo { get; init; }

    public bool Ambiguo { get; init; }

    public bool ProdutoInativo { get; init; }

    public bool ItemAusente { get; init; }

    /// <summary>
    /// A 2ª tentativa, pela descrição, achou um único produto.
    /// O nome gravado continua sendo o do catálogo (<see cref="Nome"/>), nunca o <see cref="Rotulo"/>.
    /// </summary>
    public bool CasadoPelaDescricao { get; init; }

    public bool CategoriaFaltando { get; init; }

    public bool NomeExcedeLimite { get; init; }

    public int? ProdutoCestaId { get; init; }

    public int? CategoriaId { get; init; }

    /// <summary>Nome do catálogo, nome novo cortado em 80, ou a descrição cortada se não identificada. Sem prefixo.</summary>
    public string Nome { get; init; } = string.Empty;

    /// <summary>Texto inteiro comparado ao catálogo, antes do corte de 80. Também é a chave do nome novo.</summary>
    public string NomeLido { get; init; } = string.Empty;

    public IReadOnlyList<string> Avisos { get; init; } = [];

    public string? Motivo => Avisos.Count == 0 ? null : string.Join(" ", Avisos);

    public string? Confirmacao => Revisar ? NormalizadorComprovante.MarcaConfirmacao : null;

    public bool ProntaParaGravar =>
        ProdutoCestaId is > 0
        && !CategoriaFaltando
        && !ProdutoInativo
        && !Ambiguo
        && !PrecoInvalido
        && !SomaExcedida
        && !ItemAusente
        && !NomeNovo
        && !NaoIdentificado
        && Preco > 0;

    public string? TextoApoio
    {
        get
        {
            if (Quantidade is null && Unidade is null)
                return null;

            if (Quantidade is null)
                return Unidade;

            if (Unidade is null)
                return Quantidade;

            return $"{Quantidade} {Unidade}";
        }
    }

    public string Rotulo =>
        NaoIdentificado ? NormalizadorComprovante.PrefixoNaoIdentificado + DescricaoLida : Nome;

    public override string ToString() => "LinhaComprovanteNormalizada";
}
