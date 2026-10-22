using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.B3;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.RendaFixa;

namespace OrcaFacil.Core.Services;

/// <summary>
/// Importa o extrato de movimentação da B3: lê o CSV, monta a prévia e grava
/// só as linhas marcadas, numa transação só. Compra e venda de renda variável
/// passam pelo resultado realizado, rendimento e JCP pelo provento, renda fixa
/// pelo serviço de renda fixa. Atualização fica informativa e não grava.
/// </summary>
public class ImportacaoExtratoB3Service
{
    public const string MensagemNadaGravado =
        "A prévia mudou: uma linha marcada não pode mais ser gravada. Nada foi gravado.";

    public const string MensagemFalha =
        "A gravação falhou no meio. Nada foi gravado.";

    public const string MensagemSemMarcacao = "Nenhuma linha marcada para gravar.";

    private readonly IParserExtratoB3 _parser;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ResultadoRealizadoService _resultado;
    private readonly ProventoService _provento;
    private readonly RendaFixaService _rendaFixa;

    public ImportacaoExtratoB3Service(
        IParserExtratoB3 parser,
        IDbContextFactory<AppDbContext> contextFactory,
        ResultadoRealizadoService resultado,
        ProventoService provento,
        RendaFixaService rendaFixa)
    {
        _parser = parser;
        _contextFactory = contextFactory;
        _resultado = resultado;
        _provento = provento;
        _rendaFixa = rendaFixa;
    }

    public ResultadoParseB3 Ler(Stream csv) => _parser.Ler(csv);

    public async Task<PreviaExtratoB3> PreverAsync(
        Stream csv,
        int membroFamiliaId,
        CancellationToken cancellationToken = default)
    {
        var lido = Ler(csv);
        return await ClassificarAsync(lido, desejos: null, membroFamiliaId, cancellationToken);
    }

    /// <summary>
    /// Recalcula posição, mês fechado e duplicata com as marcações atuais.
    /// Desmarcar uma compra pode deixar a venda seguinte sem posição.
    /// </summary>
    public async Task<PreviaExtratoB3> ReclassificarAsync(
        PreviaExtratoB3 previa,
        int membroFamiliaId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previa);
        var desejos = previa.Linhas
            .Where(linha => linha.PodeMarcar)
            .ToDictionary(linha => linha.Numero, linha => linha.Marcada);
        return await ClassificarListaAsync(previa.Linhas, desejos, membroFamiliaId, cancellationToken);
    }

    public async Task<ResultadoGravacaoB3> GravarAsync(
        PreviaExtratoB3 previa,
        int membroFamiliaId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previa);
        var atual = await ReclassificarAsync(previa, membroFamiliaId, cancellationToken);
        var pedidas = previa.Linhas.Where(linha => linha.Marcada).Select(linha => linha.Numero).ToHashSet();
        foreach (var numero in pedidas)
        {
            var linha = atual.Linhas.First(item => item.Numero == numero);
            if (!linha.PodeMarcar)
                throw new InvalidOperationException(MensagemNadaGravado);
        }

        var gravar = atual.Linhas
            .Where(linha => linha.Marcada && linha.PodeMarcar && linha.Comando is not null)
            .OrderBy(linha => linha.Comando!.Data)
            .ThenBy(linha => Prioridade(linha.Comando!))
            .ThenBy(linha => linha.Numero)
            .ToList();

        if (gravar.Count == 0)
            throw new InvalidOperationException(MensagemSemMarcacao);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transacao = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var maiorId = await context.Transacoes.MaxAsync(t => (int?)t.Id, cancellationToken) ?? 0;
            var ativos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var titulos = new Dictionary<(string Codigo, string Corretora), int>();
            foreach (var linha in gravar)
            {
                var comando = linha.Comando!;
                switch (comando.Destino)
                {
                    case DestinoImportacaoB3.OperacaoAtivo:
                        var ativoId = await GarantirAtivoAsync(context, ativos, comando, cancellationToken);
                        await _resultado.SalvarNoContextoAsync(context, new OperacaoAtivoFormModel
                        {
                            AtivoId = ativoId,
                            Tipo = comando.TipoOperacao,
                            // Liquidação do arquivo. O hash (#94) e o lançamento usam esta data.
                            // O pregão (D+2) sai da origem ExtratoB3 na gravação.
                            Data = comando.Data,
                            Quantidade = comando.Quantidade,
                            PrecoUnitario = comando.PrecoUnitario,
                            ValorOperacao = comando.Valor,
                            Custos = 0,
                            Corretora = comando.Corretora,
                            Origem = OrigemOperacaoAtivo.ExtratoB3,
                            HashImportacao = comando.Hash
                        }, membroFamiliaId, cancellationToken);
                        break;
                    case DestinoImportacaoB3.Provento:
                        var ativoProvento = await GarantirAtivoAsync(context, ativos, comando, cancellationToken);
                        await _provento.SalvarNoContextoAsync(context, new ProventoFormModel
                        {
                            AtivoId = ativoProvento,
                            Tipo = comando.TipoProvento,
                            DataPagamento = comando.Data,
                            ValorLiquido = comando.Valor,
                            Origem = OrigemProvento.ExtratoB3,
                            HashImportacao = comando.Hash
                        }, membroFamiliaId, cancellationToken);
                        break;
                    case DestinoImportacaoB3.RendaFixa:
                        var tituloId = await GarantirTituloAsync(context, titulos, comando, cancellationToken);
                        await _rendaFixa.SalvarNoContextoAsync(context, new OperacaoRendaFixaFormModel
                        {
                            TituloRendaFixaId = tituloId,
                            Data = comando.Data,
                            Tipo = comando.TipoRendaFixa,
                            Quantidade = comando.Quantidade,
                            PrecoUnitario = comando.PrecoUnitario,
                            Valor = comando.Valor,
                            Custos = 0,
                            IrRetido = 0,
                            Origem = OrigemOperacaoRendaFixa.ExtratoB3,
                            HashImportacao = comando.Hash
                        }, membroFamiliaId, cancellationToken);
                        break;
                }
            }

            await transacao.CommitAsync(cancellationToken);
            var datas = await context.Transacoes
                .Where(t => t.Id > maiorId)
                .Select(t => t.Data)
                .ToListAsync(cancellationToken);
            return ResultadoGravacaoB3.Criar(gravar.Count, datas);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw ex is InvalidOperationException
                ? ex
                : new InvalidOperationException(MensagemFalha, ex);
        }
    }

    public static string ApelidoInstituicao(string? instituicao)
    {
        var texto = (instituicao ?? "").ToUpperInvariant();
        if (texto.Contains("NIKOS", StringComparison.Ordinal))
            return "Nikos";
        if (texto.Contains("CLEAR", StringComparison.Ordinal))
            return "Clear";
        if (texto.Contains("ITAU", StringComparison.Ordinal) || texto.Contains("ITAÚ", StringComparison.Ordinal))
            return "Itaú";
        if (texto.Contains("XP", StringComparison.Ordinal))
            return "XP";

        var aparado = (instituicao ?? "").Trim();
        return aparado.Length <= 40 ? aparado : aparado[..40];
    }

    private async Task<PreviaExtratoB3> ClassificarAsync(
        ResultadoParseB3 lido,
        IReadOnlyDictionary<int, bool>? desejos,
        int membroFamiliaId,
        CancellationToken cancellationToken)
    {
        var linhas = new List<LinhaPreviaExtratoB3>();
        foreach (var erro in lido.Erros)
        {
            linhas.Add(new LinhaPreviaExtratoB3
            {
                Numero = erro.Linha,
                Status = StatusLinhaImportacaoB3.Erro,
                Titulo = "Linha " + erro.Linha.ToString(CultureInfo.InvariantCulture),
                Subtitulo = erro.Motivo,
                Motivo = erro.Motivo,
                ValorTexto = "—",
                Icone = "⚠️"
            });
        }

        var ocorrencias = HashLinhaExtratoB3.Ocorrencias(lido.Linhas);
        for (var i = 0; i < lido.Linhas.Count; i++)
            linhas.Add(MontarLinha(lido.Linhas[i], ocorrencias[i]));

        return await ClassificarListaAsync(linhas, desejos, membroFamiliaId, cancellationToken);
    }

    private async Task<PreviaExtratoB3> ClassificarListaAsync(
        IReadOnlyList<LinhaPreviaExtratoB3> origem,
        IReadOnlyDictionary<int, bool>? desejos,
        int membroFamiliaId,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var estado = await CarregarAsync(context, membroFamiliaId, cancellationToken);
        var linhas = origem
            .Select(Copiar)
            .OrderBy(linha => linha.Numero)
            .ToList();

        foreach (var linha in linhas.Where(linha => linha.Comando is not null))
        {
            if (estado.Hashes.Contains(linha.Comando!.Hash))
            {
                Aplicar(linha, StatusLinhaImportacaoB3.DuplicataImportada, false, false,
                    "Esta linha já foi importada.");
            }
        }

        var candidatas = linhas
            .Where(linha => linha.Comando is not null && linha.Status != StatusLinhaImportacaoB3.DuplicataImportada)
            .OrderBy(linha => linha.Comando!.Data)
            .ThenBy(linha => Prioridade(linha.Comando!))
            .ThenBy(linha => linha.Numero)
            .ToList();

        foreach (var linha in candidatas)
        {
            var comando = linha.Comando!;
            var (status, motivo) = ClassificarComando(comando, estado);
            var deseja = desejaMarcada(linha.Numero, status, desejos);
            var pode = status is StatusLinhaImportacaoB3.Nova or StatusLinhaImportacaoB3.PossivelDuplicataManual;
            Aplicar(linha, status, pode, pode && deseja, motivo);
            if (linha.Marcada)
                Confirmar(comando, estado);
        }

        return new PreviaExtratoB3 { Linhas = linhas };
    }

    private static bool desejaMarcada(
        int numero,
        StatusLinhaImportacaoB3 status,
        IReadOnlyDictionary<int, bool>? desejos)
    {
        if (status is not (StatusLinhaImportacaoB3.Nova or StatusLinhaImportacaoB3.PossivelDuplicataManual))
            return false;
        if (desejos is not null && desejos.TryGetValue(numero, out var desejo))
            return desejo;
        return status == StatusLinhaImportacaoB3.Nova;
    }

    private static (StatusLinhaImportacaoB3 Status, string Motivo) ClassificarComando(
        ComandoImportacaoB3 comando,
        EstadoImportacao estado)
    {
        if (comando.Destino == DestinoImportacaoB3.Provento)
        {
            if (PeriodoFechado(estado.Fechados, comando.Data))
                return (StatusLinhaImportacaoB3.MesFechado, "O mês do pagamento está fechado. A linha não será gravada.");
            if (EhManualProvento(estado, comando))
                return (StatusLinhaImportacaoB3.PossivelDuplicataManual, MotivoManualProvento);
            return (StatusLinhaImportacaoB3.Nova, "Será gravada.");
        }

        if (comando.Destino == DestinoImportacaoB3.OperacaoAtivo)
            return ClassificarVariavel(comando, estado);

        return ClassificarRendaFixa(comando, estado);
    }

    private static (StatusLinhaImportacaoB3 Status, string Motivo) ClassificarVariavel(
        ComandoImportacaoB3 comando,
        EstadoImportacao estado)
    {
        var carteira = estado.Ativo(comando.Ticker, comando.TipoAtivo);
        var id = estado.ProximoId;
        var movimento = new MovimentoPosicao
        {
            Id = id,
            Data = comando.Data,
            Tipo = comando.TipoOperacao,
            Quantidade = comando.Quantidade,
            PrecoUnitario = comando.PrecoUnitario,
            ValorOperacao = comando.Valor
        };
        var tentativa = carteira.Movimentos.Append(movimento);
        PosicaoComResultado depois;
        try
        {
            depois = CalculadoraResultadoRealizado.Calcular(
                tentativa,
                data => estado.Regras.Para(data, carteira.Tipo));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("excede", StringComparison.OrdinalIgnoreCase))
        {
            return (StatusLinhaImportacaoB3.SemPosicao, "Não há posição para esta venda.");
        }

        if (AlterariaMes(depois.Vendas.Select(v => (v.Id, v.Data, v.Resultado)), carteira.Lancamentos, estado.Fechados, id))
            return (StatusLinhaImportacaoB3.MesFechado, "A gravação alteraria um mês fechado. A linha não será gravada.");

        if (EhManualAtivo(estado, comando))
            return (StatusLinhaImportacaoB3.PossivelDuplicataManual, MotivoManual);

        return (StatusLinhaImportacaoB3.Nova, "Será gravada.");
    }

    private static (StatusLinhaImportacaoB3 Status, string Motivo) ClassificarRendaFixa(
        ComandoImportacaoB3 comando,
        EstadoImportacao estado)
    {
        var carteira = estado.Titulo(comando.Ticker, comando.Corretora);
        var id = estado.ProximoId;
        var movimento = new MovimentoRendaFixa
        {
            Id = id,
            Data = comando.Data,
            Tipo = comando.TipoRendaFixa,
            Quantidade = comando.Quantidade,
            Valor = comando.Valor,
            Codigo = comando.Ticker
        };
        PosicaoRendaFixaCalculada depois;
        try
        {
            depois = CalculadoraRendaFixa.Calcular(carteira.Movimentos.Append(movimento));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("excede", StringComparison.OrdinalIgnoreCase))
        {
            return (StatusLinhaImportacaoB3.SemPosicao, "Não há posição para este resgate.");
        }

        if (AlterariaMes(depois.Resgates.Select(r => (r.Id, r.Data, r.Resultado)), carteira.Lancamentos, estado.Fechados, id))
            return (StatusLinhaImportacaoB3.MesFechado, "A gravação alteraria um mês fechado. A linha não será gravada.");

        if (EhManualRendaFixa(estado, comando))
            return (StatusLinhaImportacaoB3.PossivelDuplicataManual, MotivoManual);

        return (StatusLinhaImportacaoB3.Nova, "Será gravada.");
    }

    private const string MotivoManual =
        "Há um lançamento manual com o mesmo ativo, data, quantidade e valor.";

    private const string MotivoManualProvento =
        "Há um lançamento manual com o mesmo ativo, data, tipo e valor.";

    private static bool AlterariaMes(
        IEnumerable<(int Id, DateTime Data, decimal Resultado)> depois,
        IReadOnlyDictionary<int, LancamentoConhecido> lancamentos,
        HashSet<(int Mes, int Ano)> fechados,
        int idNovo)
    {
        var vendas = depois.ToList();
        foreach (var venda in vendas)
        {
            if (venda.Id == idNovo)
            {
                if (venda.Resultado != 0 && PeriodoFechado(fechados, venda.Data))
                    return true;
                continue;
            }

            if (lancamentos.TryGetValue(venda.Id, out var lancamento))
            {
                if (!Diverge(lancamento, venda.Resultado, venda.Data))
                    continue;
                if (PeriodoFechado(fechados, lancamento.Data) || PeriodoFechado(fechados, venda.Data))
                    return true;
            }
            else if (venda.Resultado != 0 && PeriodoFechado(fechados, venda.Data))
            {
                return true;
            }
        }

        foreach (var (opId, lancamento) in lancamentos)
        {
            if (vendas.Any(venda => venda.Id == opId && venda.Resultado != 0))
                continue;
            if (PeriodoFechado(fechados, lancamento.Data))
                return true;
        }

        return false;
    }

    private static bool Diverge(LancamentoConhecido lancamento, decimal resultado, DateTime data)
    {
        if (resultado == 0m)
            return true;

        var tipo = resultado > 0m ? TipoTransacao.Receita : TipoTransacao.Despesa;
        return lancamento.Valor != Math.Abs(resultado)
            || lancamento.Tipo != tipo
            || lancamento.Data.Date != data.Date;
    }

    private static void Confirmar(ComandoImportacaoB3 comando, EstadoImportacao estado)
    {
        var id = estado.ProximoId++;
        if (comando.Destino == DestinoImportacaoB3.OperacaoAtivo)
        {
            estado.Ativo(comando.Ticker, comando.TipoAtivo).Movimentos.Add(new MovimentoPosicao
            {
                Id = id,
                Data = comando.Data,
                Tipo = comando.TipoOperacao,
                Quantidade = comando.Quantidade,
                PrecoUnitario = comando.PrecoUnitario,
                ValorOperacao = comando.Valor
            });
            return;
        }

        if (comando.Destino == DestinoImportacaoB3.RendaFixa)
        {
            estado.Titulo(comando.Ticker, comando.Corretora).Movimentos.Add(new MovimentoRendaFixa
            {
                Id = id,
                Data = comando.Data,
                Tipo = comando.TipoRendaFixa,
                Quantidade = comando.Quantidade,
                Valor = comando.Valor,
                Codigo = comando.Ticker
            });
        }
    }

    private static bool EhManualAtivo(EstadoImportacao estado, ComandoImportacaoB3 comando)
        => estado.ManuaisAtivo.Any(manual =>
            manual.Ticker.Equals(comando.Ticker, StringComparison.OrdinalIgnoreCase)
            && manual.Data.Date == comando.Data.Date
            && manual.Tipo == comando.TipoOperacao
            && manual.Quantidade == comando.Quantidade
            && manual.Valor == comando.Valor);

    private static bool EhManualProvento(EstadoImportacao estado, ComandoImportacaoB3 comando)
        => estado.ManuaisProvento.Any(manual =>
            manual.Ticker.Equals(comando.Ticker, StringComparison.OrdinalIgnoreCase)
            && manual.Data.Date == comando.Data.Date
            && manual.Tipo == comando.TipoProvento
            && manual.Valor == comando.Valor);

    private static bool EhManualRendaFixa(EstadoImportacao estado, ComandoImportacaoB3 comando)
        => estado.ManuaisRendaFixa.Any(manual =>
            manual.Codigo.Equals(comando.Ticker, StringComparison.OrdinalIgnoreCase)
            && manual.Corretora.Equals(comando.Corretora, StringComparison.OrdinalIgnoreCase)
            && manual.Data.Date == comando.Data.Date
            && manual.Tipo == comando.TipoRendaFixa
            && manual.Quantidade == comando.Quantidade
            && manual.Valor == comando.Valor);

    private static void Aplicar(
        LinhaPreviaExtratoB3 linha,
        StatusLinhaImportacaoB3 status,
        bool podeMarcar,
        bool marcada,
        string motivo)
    {
        linha.Status = status;
        linha.PodeMarcar = podeMarcar;
        linha.Marcada = marcada;
        linha.Motivo = motivo;
        var quando = linha.Comando?.Data.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"));
        var instituicao = linha.Comando?.Instituicao;
        linha.Subtitulo = string.Join(" · ", new[] { quando, instituicao, motivo }
            .Where(parte => !string.IsNullOrWhiteSpace(parte)));
    }

    private static LinhaPreviaExtratoB3 Copiar(LinhaPreviaExtratoB3 linha) => new()
    {
        Numero = linha.Numero,
        Status = linha.Status,
        Marcada = linha.Marcada,
        PodeMarcar = linha.PodeMarcar,
        Titulo = linha.Titulo,
        Subtitulo = linha.Subtitulo,
        ValorTexto = linha.ValorTexto,
        Valor = linha.Valor,
        Motivo = linha.Motivo,
        Icone = linha.Icone,
        EhCredito = linha.EhCredito,
        Comando = linha.Comando
    };

    private static LinhaPreviaExtratoB3 MontarLinha(LinhaExtratoB3 linha, int ocorrencia)
    {
        var comando = MontarComando(linha, ocorrencia);
        var informativa = comando is null;
        return new LinhaPreviaExtratoB3
        {
            Numero = linha.Linha,
            Status = informativa ? StatusLinhaImportacaoB3.Informativa : StatusLinhaImportacaoB3.Nova,
            Titulo = string.IsNullOrWhiteSpace(linha.Ticker) ? linha.ProdutoDescricao : linha.Ticker,
            ValorTexto = LinhaPreviaExtratoB3.FormatarValor(linha.ValorOperacao),
            Valor = linha.ValorOperacao ?? 0m,
            Motivo = informativa ? "Esta movimentação não é gravada." : "",
            Subtitulo = informativa
                ? string.Join(" · ", new[]
                {
                    linha.Data.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")),
                    linha.Instituicao,
                    "Esta movimentação não é gravada."
                }.Where(parte => !string.IsNullOrWhiteSpace(parte)))
                : "",
            Icone = Icone(linha, comando),
            EhCredito = linha.Direcao == DirecaoB3.Credito,
            Comando = comando
        };
    }

    private static ComandoImportacaoB3? MontarComando(LinhaExtratoB3 linha, int ocorrencia)
    {
        var destino = DestinoDe(linha);
        if (destino is null)
            return null;

        var quantidade = linha.Quantidade ?? 0m;
        var valor = linha.ValorOperacao ?? 0m;
        if (quantidade <= 0 || valor <= 0)
            return null;

        var preco = linha.PrecoUnitario is > 0 ? linha.PrecoUnitario.Value : valor / quantidade;
        var corretora = ApelidoInstituicao(linha.Instituicao);
        var data = linha.Data.ToDateTime(TimeOnly.MinValue);
        var nome = NomeDoProduto(linha);
        var tipoTitulo = TipoTituloRendaFixa.Outro;
        string? emissor = null;
        int? ano = null;
        if (destino == DestinoImportacaoB3.RendaFixa)
        {
            var dados = DadosDoTitulo(linha, corretora);
            tipoTitulo = dados.Tipo;
            emissor = dados.Emissor;
            ano = dados.Ano;
            nome = dados.Nome;
        }

        return new ComandoImportacaoB3
        {
            Destino = destino.Value,
            Hash = HashLinhaExtratoB3.Calcular(linha, ocorrencia),
            Data = data,
            Ticker = linha.Ticker,
            Nome = nome,
            Instituicao = linha.Instituicao,
            Corretora = corretora,
            // ETF permanece Acao. É decisão: ClasseAtivoB3 não separa ETF.
            TipoAtivo = linha.Classe == ClasseAtivoB3.FII ? TipoAtivo.Fii : TipoAtivo.Acao,
            TipoOperacao = linha.Tipo == TipoOperacaoB3.Venda ? TipoOperacao.Venda : TipoOperacao.Compra,
            TipoProvento = linha.Tipo == TipoOperacaoB3.Jcp ? TipoProvento.Jcp : TipoProvento.Rendimento,
            TipoRendaFixa = linha.Tipo is TipoOperacaoB3.Venda or TipoOperacaoB3.ResgateAntecipado
                ? TipoOperacaoRendaFixa.Resgate
                : TipoOperacaoRendaFixa.Aplicacao,
            TipoTitulo = tipoTitulo,
            Emissor = emissor,
            AnoVencimento = ano,
            Quantidade = quantidade,
            PrecoUnitario = preco,
            Valor = valor
        };
    }

    private static DestinoImportacaoB3? DestinoDe(LinhaExtratoB3 linha) => linha.Tipo switch
    {
        TipoOperacaoB3.Atualizacao or TipoOperacaoB3.Ignorada or TipoOperacaoB3.TransferenciaLiquidacao => null,
        TipoOperacaoB3.Rendimento or TipoOperacaoB3.Jcp => DestinoImportacaoB3.Provento,
        TipoOperacaoB3.Aplicacao or TipoOperacaoB3.ResgateAntecipado => DestinoImportacaoB3.RendaFixa,
        TipoOperacaoB3.Compra or TipoOperacaoB3.Venda when linha.Classe == ClasseAtivoB3.RendaFixa
            => DestinoImportacaoB3.RendaFixa,
        TipoOperacaoB3.Compra or TipoOperacaoB3.Venda => DestinoImportacaoB3.OperacaoAtivo,
        _ => null
    };

    private static string NomeDoProduto(LinhaExtratoB3 linha)
    {
        if (linha.Classe == ClasseAtivoB3.RendaFixa)
            return DadosDoTitulo(linha, ApelidoInstituicao(linha.Instituicao)).Nome;

        var texto = linha.ProdutoDescricao.Trim();
        const string Separador = " - ";
        var indice = texto.IndexOf(Separador, StringComparison.Ordinal);
        if (indice > 0 && texto[..indice].Trim().Equals(linha.Ticker, StringComparison.OrdinalIgnoreCase))
            texto = texto[(indice + Separador.Length)..].Trim();
        if (texto.Length == 0)
            texto = linha.Ticker;
        return texto.Length <= 80 ? texto : texto[..80];
    }

    private static (TipoTituloRendaFixa Tipo, string? Emissor, int? Ano, string Nome) DadosDoTitulo(
        LinhaExtratoB3 linha,
        string corretora)
    {
        var partes = linha.ProdutoDescricao.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (EhPrefixo(partes, "CDB"))
            return TituloBancario(TipoTituloRendaFixa.CDB, partes, linha, corretora);
        if (EhPrefixo(partes, "LCI"))
            return TituloBancario(TipoTituloRendaFixa.LCI, partes, linha, corretora);
        if (EhPrefixo(partes, "LCA"))
            return TituloBancario(TipoTituloRendaFixa.LCA, partes, linha, corretora);

        var titulo = Limitar(linha.Ticker, 80);
        var tipo = titulo.Contains("PREFIXADO", StringComparison.Ordinal) ? TipoTituloRendaFixa.TesouroPrefixado
            : titulo.Contains("IPCA", StringComparison.Ordinal) ? TipoTituloRendaFixa.TesouroIPCA
            : titulo.Contains("SELIC", StringComparison.Ordinal) ? TipoTituloRendaFixa.TesouroSelic
            : TipoTituloRendaFixa.Outro;
        int? ano = null;
        var ultimo = titulo.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (ultimo is not null && int.TryParse(ultimo, NumberStyles.None, CultureInfo.InvariantCulture, out var anoLido)
            && anoLido is >= 2000 and <= 2100)
            ano = anoLido;
        return (tipo, null, ano, titulo);
    }

    private static bool EhPrefixo(string[] partes, string prefixo)
        => partes.Length >= 1 && partes[0].Equals(prefixo, StringComparison.OrdinalIgnoreCase);

    private static (TipoTituloRendaFixa Tipo, string? Emissor, int? Ano, string Nome) TituloBancario(
        TipoTituloRendaFixa tipo,
        string[] partes,
        LinhaExtratoB3 linha,
        string corretora)
    {
        var emissor = partes.Length >= 3 ? Limitar(partes[2], 80) : corretora;
        var nome = Limitar($"{linha.Ticker} — {emissor}", 80);
        return (tipo, emissor, null, nome);
    }

    private static string Limitar(string texto, int tamanho)
        => texto.Length <= tamanho ? texto : texto[..tamanho];

    private static string Icone(LinhaExtratoB3 linha, ComandoImportacaoB3? comando)
    {
        if (comando is null)
            return "ℹ️";
        return comando.Destino switch
        {
            DestinoImportacaoB3.Provento => "💰",
            DestinoImportacaoB3.RendaFixa when comando.TipoRendaFixa == TipoOperacaoRendaFixa.Resgate => "🏦",
            DestinoImportacaoB3.RendaFixa => "🏛️",
            _ when linha.Tipo == TipoOperacaoB3.Venda => "📉",
            _ => "📈"
        };
    }

    private static int Prioridade(ComandoImportacaoB3 comando) => comando.Destino switch
    {
        DestinoImportacaoB3.OperacaoAtivo when comando.TipoOperacao == TipoOperacao.Compra => 0,
        DestinoImportacaoB3.RendaFixa when comando.TipoRendaFixa == TipoOperacaoRendaFixa.Aplicacao => 0,
        DestinoImportacaoB3.Provento => 2,
        _ => 1
    };

    private static async Task<int> GarantirAtivoAsync(
        AppDbContext context,
        Dictionary<string, int> cache,
        ComandoImportacaoB3 comando,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(comando.Ticker, out var id))
            return id;

        var existente = await context.AtivosFinanceiros
            .FirstOrDefaultAsync(ativo => ativo.Ticker == comando.Ticker, cancellationToken);
        if (existente is null)
        {
            existente = new AtivoFinanceiro
            {
                Ticker = comando.Ticker,
                Nome = comando.Nome,
                Tipo = comando.TipoAtivo,
                Ativo = true
            };
            context.AtivosFinanceiros.Add(existente);
            await context.SaveChangesAsync(cancellationToken);
        }

        cache[comando.Ticker] = existente.Id;
        return existente.Id;
    }

    private static async Task<int> GarantirTituloAsync(
        AppDbContext context,
        Dictionary<(string Codigo, string Corretora), int> cache,
        ComandoImportacaoB3 comando,
        CancellationToken cancellationToken)
    {
        var chave = (comando.Ticker, comando.Corretora);
        if (cache.TryGetValue(chave, out var id))
            return id;

        var existente = await context.TitulosRendaFixa.FirstOrDefaultAsync(
            titulo => titulo.Codigo == comando.Ticker && titulo.Corretora == comando.Corretora,
            cancellationToken);
        if (existente is null)
        {
            existente = new TituloRendaFixa
            {
                Tipo = comando.TipoTitulo,
                Codigo = comando.Ticker,
                Nome = comando.Nome,
                Emissor = comando.Emissor,
                AnoVencimento = comando.AnoVencimento,
                Corretora = comando.Corretora,
                Ativo = true
            };
            context.TitulosRendaFixa.Add(existente);
            await context.SaveChangesAsync(cancellationToken);
        }

        cache[chave] = existente.Id;
        return existente.Id;
    }

    private static async Task<EstadoImportacao> CarregarAsync(
        AppDbContext context,
        int membroFamiliaId,
        CancellationToken cancellationToken)
    {
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        hashes.UnionWith(await context.OperacoesAtivo
            .Where(o => o.HashImportacao != null)
            .Select(o => o.HashImportacao!)
            .ToListAsync(cancellationToken));
        hashes.UnionWith(await context.Proventos
            .Where(p => p.HashImportacao != null)
            .Select(p => p.HashImportacao!)
            .ToListAsync(cancellationToken));
        hashes.UnionWith(await context.OperacoesRendaFixa
            .Where(o => o.HashImportacao != null)
            .Select(o => o.HashImportacao!)
            .ToListAsync(cancellationToken));

        var fechadosLista = await context.FechamentosPeriodo
            .Where(f => f.Fechado)
            .Select(f => new { f.Mes, f.Ano })
            .ToListAsync(cancellationToken);

        var operacoes = await context.OperacoesAtivo
            .Include(o => o.Ativo)
            .Where(o => o.MembroFamiliaId == membroFamiliaId)
            .ToListAsync(cancellationToken);
        var lancamentosAtivo = await context.Transacoes
            .Where(t => t.OperacaoAtivoId != null && t.MembroFamiliaId == membroFamiliaId)
            .Select(t => new { Id = t.OperacaoAtivoId!.Value, t.Data, t.Valor, t.Tipo })
            .ToListAsync(cancellationToken);

        var proventos = await context.Proventos
            .Include(p => p.Ativo)
            .Where(p => p.MembroFamiliaId == membroFamiliaId && p.Origem == OrigemProvento.Manual)
            .ToListAsync(cancellationToken);

        var titulos = await context.TitulosRendaFixa.Include(t => t.Operacoes).ToListAsync(cancellationToken);
        var lancamentosRf = await context.Transacoes
            .Where(t => t.OperacaoRendaFixaId != null)
            .Select(t => new { Id = t.OperacaoRendaFixaId!.Value, t.Data, t.Valor, t.Tipo })
            .ToListAsync(cancellationToken);

        var regras = new TabelaRegrasFiscais(await context.RegrasFiscais.AsNoTracking().ToListAsync(cancellationToken));
        var maiorId = 0;
        if (operacoes.Count > 0)
            maiorId = Math.Max(maiorId, operacoes.Max(o => o.Id));
        var idsRf = titulos.SelectMany(t => t.Operacoes).Select(o => o.Id).ToList();
        if (idsRf.Count > 0)
            maiorId = Math.Max(maiorId, idsRf.Max());

        var estado = new EstadoImportacao(regras, maiorId + 1)
        {
            Hashes = hashes,
            Fechados = fechadosLista.Select(f => (f.Mes, f.Ano)).ToHashSet()
        };

        foreach (var grupo in operacoes.GroupBy(o => o.Ativo?.Ticker ?? ""))
        {
            if (grupo.Key.Length == 0)
                continue;
            var carteira = estado.Ativo(grupo.Key, grupo.First().Ativo?.Tipo ?? TipoAtivo.Acao);
            foreach (var operacao in grupo)
            {
                carteira.Movimentos.Add(new MovimentoPosicao
                {
                    Id = operacao.Id,
                    Data = operacao.Data,
                    Tipo = operacao.Tipo,
                    Quantidade = operacao.Quantidade,
                    PrecoUnitario = operacao.PrecoUnitario,
                    ValorOperacao = operacao.ValorOperacao,
                    Custos = operacao.Custos
                });
            }
        }

        foreach (var lancamento in lancamentosAtivo)
        {
            var operacao = operacoes.FirstOrDefault(o => o.Id == lancamento.Id);
            var ticker = operacao?.Ativo?.Ticker;
            if (ticker is null)
                continue;
            estado.Ativo(ticker, operacao!.Ativo!.Tipo).Lancamentos[lancamento.Id] = new LancamentoConhecido
            {
                Data = lancamento.Data,
                Valor = lancamento.Valor,
                Tipo = lancamento.Tipo
            };
        }

        estado.ManuaisAtivo.AddRange(operacoes
            .Where(o => o.Origem == OrigemOperacaoAtivo.Manual && o.Ativo is not null)
            .Select(o => new ManualAtivo(o.Ativo!.Ticker, o.Data, o.Tipo, o.Quantidade, o.ValorOperacao)));
        estado.ManuaisProvento.AddRange(proventos
            .Where(p => p.Ativo is not null)
            .Select(p => new ManualProvento(p.Ativo!.Ticker, p.DataPagamento, p.Tipo, p.ValorLiquido)));

        foreach (var titulo in titulos)
        {
            var carteira = estado.Titulo(titulo.Codigo, titulo.Corretora);
            foreach (var operacao in titulo.Operacoes)
            {
                carteira.Movimentos.Add(new MovimentoRendaFixa
                {
                    Id = operacao.Id,
                    Data = operacao.Data,
                    Tipo = operacao.Tipo,
                    Quantidade = operacao.Quantidade,
                    Valor = operacao.Valor,
                    Custos = operacao.Custos,
                    IrRetido = operacao.IrRetido,
                    Codigo = titulo.Codigo
                });
                if (operacao.Origem == OrigemOperacaoRendaFixa.Manual)
                {
                    estado.ManuaisRendaFixa.Add(new ManualRendaFixa(
                        titulo.Codigo, titulo.Corretora, operacao.Data, operacao.Tipo, operacao.Quantidade, operacao.Valor));
                }
            }
        }

        foreach (var lancamento in lancamentosRf)
        {
            var titulo = titulos.FirstOrDefault(t => t.Operacoes.Any(o => o.Id == lancamento.Id));
            if (titulo is null)
                continue;
            estado.Titulo(titulo.Codigo, titulo.Corretora).Lancamentos[lancamento.Id] = new LancamentoConhecido
            {
                Data = lancamento.Data,
                Valor = lancamento.Valor,
                Tipo = lancamento.Tipo
            };
        }

        return estado;
    }

    private static bool PeriodoFechado(HashSet<(int Mes, int Ano)> periodos, DateTime data)
        => periodos.Contains((data.Month, data.Year));

    private sealed class LancamentoConhecido
    {
        public DateTime Data { get; init; }
        public decimal Valor { get; init; }
        public TipoTransacao Tipo { get; init; }
    }

    private sealed record ManualAtivo(string Ticker, DateTime Data, TipoOperacao Tipo, decimal Quantidade, decimal Valor);
    private sealed record ManualProvento(string Ticker, DateTime Data, TipoProvento Tipo, decimal Valor);
    private sealed record ManualRendaFixa(
        string Codigo, string Corretora, DateTime Data, TipoOperacaoRendaFixa Tipo, decimal Quantidade, decimal Valor);

    private sealed class CarteiraAtivo
    {
        public TipoAtivo Tipo { get; init; }
        public List<MovimentoPosicao> Movimentos { get; } = [];
        public Dictionary<int, LancamentoConhecido> Lancamentos { get; } = [];
    }

    private sealed class CarteiraTitulo
    {
        public List<MovimentoRendaFixa> Movimentos { get; } = [];
        public Dictionary<int, LancamentoConhecido> Lancamentos { get; } = [];
    }

    private sealed class EstadoImportacao
    {
        private readonly Dictionary<string, CarteiraAtivo> _ativos = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(string Codigo, string Corretora), CarteiraTitulo> _titulos = new();

        public EstadoImportacao(TabelaRegrasFiscais regras, int proximoId)
        {
            Regras = regras;
            ProximoId = proximoId;
        }

        public TabelaRegrasFiscais Regras { get; }
        public int ProximoId { get; set; }
        public HashSet<string> Hashes { get; init; } = new(StringComparer.Ordinal);
        public HashSet<(int Mes, int Ano)> Fechados { get; init; } = [];
        public List<ManualAtivo> ManuaisAtivo { get; } = [];
        public List<ManualProvento> ManuaisProvento { get; } = [];
        public List<ManualRendaFixa> ManuaisRendaFixa { get; } = [];

        public CarteiraAtivo Ativo(string ticker, TipoAtivo tipo)
        {
            if (_ativos.TryGetValue(ticker, out var carteira))
                return carteira;
            carteira = new CarteiraAtivo { Tipo = tipo };
            _ativos[ticker] = carteira;
            return carteira;
        }

        public CarteiraTitulo Titulo(string codigo, string corretora)
        {
            var chave = (codigo, corretora);
            if (_titulos.TryGetValue(chave, out var carteira))
                return carteira;
            carteira = new CarteiraTitulo();
            _titulos[chave] = carteira;
            return carteira;
        }
    }
}
