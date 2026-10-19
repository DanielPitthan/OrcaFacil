using Android.Content;
using Android.Database;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using AndroidUri = Android.Net.Uri;

namespace OrcaFacil.Platforms.Android.Espelho;

/// <summary>
/// Pasta do espelho via SAF (<c>DocumentsContract</c> / <c>ContentResolver</c>).
/// A troca é temporário e depois rename. <c>ColumnFlags</c> nula ou sem
/// <c>SupportsRename</c> recusa com <c>ProvedorSemTroca</c> antes de <c>RenameDocument</c>.
/// </summary>
/// <remarks>
/// Provedores como o Drive podem anunciar rename e ainda assim implementá-lo como
/// cópia mais exclusão, sem atomicidade. A pós-condição de troca confirmada exige
/// o destino visível e a origem ausente. Se os dois nomes continuam visíveis, este
/// volume não apaga nenhum dos dois: o protocolo publica com resíduo e a reserva
/// do arquivo anterior só sai numa troca confirmada. Se <c>createDocument</c> devolve
/// outro nome, esse URI não é apagado: pode ser o espelho que já estava na pasta.
/// Cancelamento também não apaga o que já foi criado. A URI da pasta não entra em mensagem nem em log.
/// </remarks>
public sealed class VolumePastaEspelhoAndroid : IVolumePastaEspelho
{
    private const string MimeEspelho = "application/octet-stream";

    private readonly IMemoriaPastaEspelho _memoria;

    public VolumePastaEspelhoAndroid(IMemoriaPastaEspelho memoria)
    {
        _memoria = memoria ?? throw new ArgumentNullException(nameof(memoria));
    }

    public string NomeArquivo => FormatoEspelho.NomeArquivoSugerido;

    public DiagnosticoPastaEspelho Consultar()
    {
        if (!TentarAbrir(out var resolver, out var arvore, out var codigo))
            return new DiagnosticoPastaEspelho(codigo, false);

        try
        {
            var treeId = DocumentsContract.GetTreeDocumentId(arvore);
            if (string.IsNullOrEmpty(treeId))
                return new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.PastaApagada, false);

            var pasta = DocumentsContract.BuildDocumentUriUsingTree(arvore, treeId);
            if (pasta is null || !DocumentoVisivel(resolver, pasta))
                return new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.PastaApagada, false);

            var varredura = InspecionarFilhos(resolver, arvore, treeId, NomeArquivo);
            if (varredura.Falha is CodigoFalhaPastaEspelho falha)
                return new DiagnosticoPastaEspelho(falha, false);

            return new DiagnosticoPastaEspelho(
                CodigoFalhaPastaEspelho.Nenhuma,
                varredura.ArquivoFinal,
                varredura.Residuo,
                varredura.ArquivoFinal ? null : varredura.NomeReserva);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DiagnosticoPastaEspelho(Classificar(ex, resolver, arvore, null), false);
        }
    }

    public Task<ResultadoOperacaoPasta> CriarTemporarioAsync(
        string nomeTemporario,
        string arquivoOrigemLocal,
        CancellationToken cancellationToken)
    {
        var recusa = RegrasPastaEspelho.ImpedirCriacao(NomeArquivo, nomeTemporario);
        if (recusa is CodigoFalhaPastaEspelho codigo)
            return Task.FromResult(ResultadoOperacaoPasta.Falha(codigo));

        return Task.FromResult(CriarTemporario(nomeTemporario, arquivoOrigemLocal, cancellationToken));
    }

    public Task<ResultadoRenomeacaoVolume> RenomearAsync(
        string nomeAtual,
        string nomeNovo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!RegrasPastaEspelho.PodeRenomear(NomeArquivo, nomeAtual, nomeNovo))
            return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.NomeRecusado));

        if (!TentarAbrir(out var resolver, out var arvore, out var codigo))
            return Task.FromResult(ResultadoRenomeacaoVolume.Falha(codigo));

        string? treeId = null;
        try
        {
            treeId = DocumentsContract.GetTreeDocumentId(arvore);
            if (string.IsNullOrEmpty(treeId))
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.PastaApagada));

            var destino = Buscar(resolver, arvore, treeId, nomeNovo);
            if (destino.Falha is CodigoFalhaPastaEspelho falhaDestino)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(falhaDestino));
            if (destino.Quantidade > 0)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca, destinoJaExistia: true));

            var origem = Buscar(resolver, arvore, treeId, nomeAtual);
            if (origem.Falha is CodigoFalhaPastaEspelho falhaOrigem)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(falhaOrigem));
            if (origem.Quantidade == 0 || origem.Uri is null)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ArquivoAusente));
            if (origem.Quantidade != 1)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.Indisponivel));

            if (RegrasPastaEspelho.ImpedirRenameSemFlag(origem.Flags is { } flagsDocumento ? (int)flagsDocumento : null)
                is CodigoFalhaPastaEspelho semRename)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(semRename));

            var renomeado = DocumentsContract.RenameDocument(resolver, origem.Uri, nomeNovo);
            if (renomeado is null)
                return Task.FromResult(AvaliarDepoisDoRename(resolver, arvore, treeId, nomeAtual, nomeNovo)
                    ?? ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca));

            return Task.FromResult(AvaliarDepoisDoRename(resolver, arvore, treeId, nomeAtual, nomeNovo)
                ?? ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.TrocaNaoConfirmada));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(treeId))
            {
                var depois = AvaliarDepoisDoRename(resolver, arvore, treeId, nomeAtual, nomeNovo);
                if (depois is not null)
                    return Task.FromResult(depois);
            }

            return Task.FromResult(ResultadoRenomeacaoVolume.Falha(Classificar(ex, resolver, arvore, null)));
        }
    }

    public Task<ResultadoOperacaoPasta> DescartarTemporarioAsync(
        string nomeTemporario,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var recusa = RegrasPastaEspelho.ImpedirDescarteTemporario(NomeArquivo, nomeTemporario);
        if (recusa is CodigoFalhaPastaEspelho codigo)
            return Task.FromResult(ResultadoOperacaoPasta.Falha(codigo));

        return Task.FromResult(ApagarPorNome(nomeTemporario, exigeFinalPresente: false));
    }

    public Task<ResultadoOperacaoPasta> DescartarReservaAposTrocaAsync(
        string nomeReserva,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TentarAbrir(out var resolver, out var arvore, out var codigoAbertura))
            return Task.FromResult(ResultadoOperacaoPasta.Falha(codigoAbertura));

        bool finalPresente;
        try
        {
            var treeId = DocumentsContract.GetTreeDocumentId(arvore);
            var buscaFinal = Buscar(resolver, arvore, treeId!, NomeArquivo);
            if (buscaFinal.Falha is CodigoFalhaPastaEspelho falha)
                return Task.FromResult(ResultadoOperacaoPasta.Falha(falha));

            finalPresente = buscaFinal.Quantidade > 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(ResultadoOperacaoPasta.Falha(Classificar(ex, resolver, arvore, null)));
        }

        var recusa = RegrasPastaEspelho.ImpedirDescarteReserva(NomeArquivo, nomeReserva, finalPresente);
        if (recusa is CodigoFalhaPastaEspelho codigo)
            return Task.FromResult(ResultadoOperacaoPasta.Falha(codigo));

        return Task.FromResult(ApagarPorNome(nomeReserva, exigeFinalPresente: true));
    }

    public async Task<ResultadoOperacaoPasta> CopiarParaLocalAsync(
        string nome,
        string caminhoLocal,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var podeLer = string.Equals(nome, NomeArquivo, StringComparison.Ordinal)
            || NomeTemporarioEspelho.EhReservaDesteApp(nome, NomeArquivo);
        if (!podeLer)
            return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.NomeRecusado);

        if (!TentarAbrir(out var resolver, out var arvore, out var codigo))
            return ResultadoOperacaoPasta.Falha(codigo);

        try
        {
            var treeId = DocumentsContract.GetTreeDocumentId(arvore);
            var busca = Buscar(resolver, arvore, treeId!, nome);
            if (busca.Falha is CodigoFalhaPastaEspelho falha)
                return ResultadoOperacaoPasta.Falha(falha);
            if (busca.Quantidade == 0 || busca.Uri is null)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ArquivoAusente);
            if (busca.Quantidade != 1)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.Indisponivel);

            var entrada = resolver.OpenInputStream(busca.Uri);
            if (entrada is null)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.FalhaDeLeitura);

            await using (entrada)
            await using (var saida = new FileStream(
                caminhoLocal,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous))
            {
                await entrada.CopyToAsync(saida, cancellationToken);
                await saida.FlushAsync(cancellationToken);
            }

            return ResultadoOperacaoPasta.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ResultadoOperacaoPasta.Falha(Classificar(ex, resolver, arvore, null));
        }
    }

    private ResultadoOperacaoPasta CriarTemporario(
        string nomeTemporario,
        string arquivoOrigemLocal,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TentarAbrir(out var resolver, out var arvore, out var codigo))
            return ResultadoOperacaoPasta.Falha(codigo);

        AndroidUri? criado = null;
        try
        {
            var treeId = DocumentsContract.GetTreeDocumentId(arvore);
            if (string.IsNullOrEmpty(treeId))
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.PastaApagada);

            var pasta = DocumentsContract.BuildDocumentUriUsingTree(arvore, treeId);
            if (pasta is null)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.PastaApagada);

            var flagsPasta = FlagsDe(resolver, pasta);
            if (flagsPasta is DocumentContractFlags flags && !flags.HasFlag(DocumentContractFlags.DirSupportsCreate))
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca);

            var jaExiste = Buscar(resolver, arvore, treeId, nomeTemporario);
            if (jaExiste.Falha is CodigoFalhaPastaEspelho falhaBusca)
                return ResultadoOperacaoPasta.Falha(falhaBusca);
            if (jaExiste.Quantidade > 0)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca);

            criado = DocumentsContract.CreateDocument(resolver, pasta, MimeEspelho, nomeTemporario);
            if (criado is null)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca);

            if (!NomeConfere(resolver, criado, nomeTemporario))
            {
                // O URI pode ser o espelho que já existia. Na dúvida, não apagar.
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca);
            }

            var saida = resolver.OpenOutputStream(criado);
            if (saida is null)
            {
                DescartarSeForOTemporario(resolver, criado, nomeTemporario);
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.FalhaDeEscrita);
            }

            using (saida)
            using (var entrada = new FileStream(
                arquivoOrigemLocal,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.SequentialScan))
            {
                cancellationToken.ThrowIfCancellationRequested();
                entrada.CopyTo(saida);
                saida.Flush();
            }

            return ResultadoOperacaoPasta.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (criado is not null)
                DescartarSeForOTemporario(resolver, criado, nomeTemporario);

            return ResultadoOperacaoPasta.Falha(Classificar(ex, resolver, arvore, null));
        }
    }

    private ResultadoOperacaoPasta ApagarPorNome(string nome, bool exigeFinalPresente)
    {
        if (!TentarAbrir(out var resolver, out var arvore, out var codigo))
            return ResultadoOperacaoPasta.Falha(codigo);

        try
        {
            var treeId = DocumentsContract.GetTreeDocumentId(arvore);
            if (string.IsNullOrEmpty(treeId))
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.PastaApagada);

            if (exigeFinalPresente)
            {
                var final = Buscar(resolver, arvore, treeId, NomeArquivo);
                if (final.Falha is CodigoFalhaPastaEspelho falhaFinal)
                    return ResultadoOperacaoPasta.Falha(falhaFinal);
                if (final.Quantidade == 0)
                    return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome);
            }

            var busca = Buscar(resolver, arvore, treeId, nome);
            if (busca.Falha is CodigoFalhaPastaEspelho falha)
                return ResultadoOperacaoPasta.Falha(falha);
            if (busca.Quantidade == 0)
                return ResultadoOperacaoPasta.Ok();
            if (busca.Quantidade != 1 || busca.Uri is null)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.Indisponivel);

            if (busca.Flags is DocumentContractFlags flags && !flags.HasFlag(DocumentContractFlags.SupportsDelete))
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.FalhaDeEscrita);

            var apagou = DocumentsContract.DeleteDocument(resolver, busca.Uri);
            return apagou
                ? ResultadoOperacaoPasta.Ok()
                : ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.FalhaDeEscrita);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ResultadoOperacaoPasta.Falha(Classificar(ex, resolver, arvore, null));
        }
    }

    private static void DescartarSeForOTemporario(ContentResolver resolver, AndroidUri uri, string nomeTemporario)
    {
        if (!NomeConfere(resolver, uri, nomeTemporario))
            return;

        try
        {
            DocumentsContract.DeleteDocument(resolver, uri);
        }
        catch (Exception)
        {
        }
    }

    private static ResultadoRenomeacaoVolume? AvaliarDepoisDoRename(
        ContentResolver resolver,
        AndroidUri arvore,
        string treeId,
        string nomeAtual,
        string nomeNovo)
    {
        var origemDepois = Buscar(resolver, arvore, treeId, nomeAtual);
        var destinoDepois = Buscar(resolver, arvore, treeId, nomeNovo);
        if (origemDepois.Falha is not null || destinoDepois.Falha is not null)
            return null;

        if (destinoDepois.Quantidade == 1 && origemDepois.Quantidade == 0)
            return ResultadoRenomeacaoVolume.Ok();

        if (destinoDepois.Quantidade > 0 && origemDepois.Quantidade > 0)
            return ResultadoRenomeacaoVolume.CopiaSemExcluirOrigem();

        if (destinoDepois.Quantidade > 0 && origemDepois.Quantidade == 0)
            return ResultadoRenomeacaoVolume.FalhaAposEfetuar(CodigoFalhaPastaEspelho.TrocaNaoConfirmada);

        return null;
    }

    private static Varredura InspecionarFilhos(
        ContentResolver resolver,
        AndroidUri arvore,
        string treeId,
        string nomeFinal)
    {
        ICursor? cursor = null;
        try
        {
            var filhos = DocumentsContract.BuildChildDocumentsUriUsingTree(arvore, treeId);
            if (filhos is null)
                return Varredura.ComFalha(CodigoFalhaPastaEspelho.PastaApagada);

            cursor = resolver.Query(
                filhos,
                [DocumentsContract.Document.ColumnDisplayName],
                null,
                null,
                null);
            if (cursor is null)
                return Varredura.ComFalha(CodigoFalhaPastaEspelho.PastaApagada);

            var indiceNome = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDisplayName);
            if (indiceNome < 0)
                return Varredura.ComFalha(CodigoFalhaPastaEspelho.Indisponivel);

            var arquivoFinal = false;
            var residuo = false;
            string? nomeReserva = null;
            while (cursor.MoveToNext())
            {
                if (cursor.IsNull(indiceNome))
                    continue;

                var exibicao = cursor.GetString(indiceNome);
                if (string.Equals(exibicao, nomeFinal, StringComparison.Ordinal))
                {
                    arquivoFinal = true;
                    continue;
                }

                if (NomeTemporarioEspelho.EhReservaDesteApp(exibicao, nomeFinal))
                {
                    residuo = true;
                    nomeReserva ??= exibicao;
                    continue;
                }

                if (NomeTemporarioEspelho.EhTemporarioDesteApp(exibicao, nomeFinal))
                    residuo = true;
            }

            return new Varredura(null, arquivoFinal, residuo, nomeReserva);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Varredura.ComFalha(ClassificarBusca(ex));
        }
        finally
        {
            cursor?.Close();
            cursor?.Dispose();
        }
    }

    private readonly record struct Varredura(
        CodigoFalhaPastaEspelho? Falha,
        bool ArquivoFinal,
        bool Residuo,
        string? NomeReserva)
    {
        public static Varredura ComFalha(CodigoFalhaPastaEspelho codigo) => new(codigo, false, false, null);
    }

    private bool TentarAbrir(out ContentResolver resolver, out AndroidUri arvore, out CodigoFalhaPastaEspelho codigo)
    {
        resolver = null!;
        arvore = null!;
        var texto = _memoria.LerUri();
        if (!UriEscolhaPasta.EhUriDeConteudo(texto))
        {
            codigo = CodigoFalhaPastaEspelho.PastaNaoEscolhida;
            return false;
        }

        var obtido = Resolver();
        if (obtido is null)
        {
            codigo = CodigoFalhaPastaEspelho.Indisponivel;
            return false;
        }

        AndroidUri? uri;
        try
        {
            uri = AndroidUri.Parse(texto);
        }
        catch (Exception)
        {
            codigo = CodigoFalhaPastaEspelho.Indisponivel;
            return false;
        }

        if (uri is null || !"content".Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            codigo = CodigoFalhaPastaEspelho.Indisponivel;
            return false;
        }

        if (!PermissaoPastaEspelho.Vigente(obtido, uri))
        {
            codigo = CodigoFalhaPastaEspelho.PermissaoRevogada;
            return false;
        }

        resolver = obtido;
        arvore = uri;
        codigo = CodigoFalhaPastaEspelho.Nenhuma;
        return true;
    }

    private static ContentResolver? Resolver()
    {
        var atividade = Platform.CurrentActivity;
        if (atividade?.ContentResolver is not null)
            return atividade.ContentResolver;

        return global::Android.App.Application.Context?.ContentResolver;
    }

    private static CodigoFalhaPastaEspelho Classificar(
        Exception excecao,
        ContentResolver resolver,
        AndroidUri arvore,
        bool? documentoEncontrado)
    {
        bool? permissao = null;
        try
        {
            permissao = PermissaoPastaEspelho.Vigente(resolver, arvore);
        }
        catch (Exception)
        {
        }

        return ClassificadorFalhaPastaEspelho.Classificar(
            SinalFalhaPasta.De(excecao, permissao, documentoEncontrado));
    }

    private static bool DocumentoVisivel(ContentResolver resolver, AndroidUri documento)
    {
        ICursor? cursor = null;
        try
        {
            cursor = resolver.Query(documento, [DocumentsContract.Document.ColumnDocumentId], null, null, null);
            return cursor is not null && cursor.MoveToFirst();
        }
        finally
        {
            cursor?.Close();
            cursor?.Dispose();
        }
    }

    private static DocumentContractFlags? FlagsDe(ContentResolver resolver, AndroidUri documento)
    {
        ICursor? cursor = null;
        try
        {
            cursor = resolver.Query(documento, [DocumentsContract.Document.ColumnFlags], null, null, null);
            if (cursor is null || !cursor.MoveToFirst())
                return null;

            var indice = cursor.GetColumnIndex(DocumentsContract.Document.ColumnFlags);
            if (indice < 0 || cursor.IsNull(indice))
                return null;

            return (DocumentContractFlags)cursor.GetInt(indice);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            cursor?.Close();
            cursor?.Dispose();
        }
    }

    private static bool NomeConfere(ContentResolver resolver, AndroidUri documento, string nome)
    {
        ICursor? cursor = null;
        try
        {
            cursor = resolver.Query(documento, [DocumentsContract.Document.ColumnDisplayName], null, null, null);
            if (cursor is null || !cursor.MoveToFirst())
                return false;

            var indice = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDisplayName);
            if (indice < 0 || cursor.IsNull(indice))
                return false;

            return string.Equals(cursor.GetString(indice), nome, StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            cursor?.Close();
            cursor?.Dispose();
        }
    }

    private static BuscaNome Buscar(ContentResolver resolver, AndroidUri arvore, string treeId, string nome)
    {
        ICursor? cursor = null;
        try
        {
            var filhos = DocumentsContract.BuildChildDocumentsUriUsingTree(arvore, treeId);
            if (filhos is null)
                return BuscaNome.ComFalha(CodigoFalhaPastaEspelho.PastaApagada);

            cursor = resolver.Query(
                filhos,
                [
                    DocumentsContract.Document.ColumnDocumentId,
                    DocumentsContract.Document.ColumnDisplayName,
                    DocumentsContract.Document.ColumnFlags
                ],
                null,
                null,
                null);

            if (cursor is null)
                return BuscaNome.ComFalha(CodigoFalhaPastaEspelho.PastaApagada);

            var indiceId = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDocumentId);
            var indiceNome = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDisplayName);
            var indiceFlags = cursor.GetColumnIndex(DocumentsContract.Document.ColumnFlags);
            if (indiceId < 0 || indiceNome < 0)
                return BuscaNome.ComFalha(CodigoFalhaPastaEspelho.Indisponivel);

            AndroidUri? encontrado = null;
            DocumentContractFlags? flags = null;
            var quantidade = 0;
            while (cursor.MoveToNext())
            {
                if (cursor.IsNull(indiceNome))
                    continue;

                var exibicao = cursor.GetString(indiceNome);
                if (!string.Equals(exibicao, nome, StringComparison.Ordinal))
                    continue;

                quantidade++;
                if (quantidade > 1)
                    continue;

                var id = cursor.IsNull(indiceId) ? null : cursor.GetString(indiceId);
                if (!string.IsNullOrEmpty(id))
                    encontrado = DocumentsContract.BuildDocumentUriUsingTree(arvore, id);

                if (indiceFlags >= 0 && !cursor.IsNull(indiceFlags))
                    flags = (DocumentContractFlags)cursor.GetInt(indiceFlags);
            }

            return new BuscaNome(null, quantidade, encontrado, flags);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BuscaNome.ComFalha(ClassificarBusca(ex));
        }
        finally
        {
            cursor?.Close();
            cursor?.Dispose();
        }
    }

    private static CodigoFalhaPastaEspelho ClassificarBusca(Exception excecao)
        => ClassificadorFalhaPastaEspelho.Classificar(SinalFalhaPasta.De(excecao));

    private readonly record struct BuscaNome(
        CodigoFalhaPastaEspelho? Falha,
        int Quantidade,
        AndroidUri? Uri,
        DocumentContractFlags? Flags)
    {
        public static BuscaNome ComFalha(CodigoFalhaPastaEspelho codigo) => new(codigo, 0, null, null);
    }
}
