using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class LimpezaFotoTemporariaTests
{
    [Fact]
    public void ApagaCacheETemporario_ENuncaPerfilNemIrma()
    {
        var raiz = Directory.CreateTempSubdirectory("orca-limpeza-");
        try
        {
            var perfil = Path.Combine(raiz.FullName, "perfil");
            var cache = Path.Combine(raiz.FullName, "cache");
            var temporario = Path.Combine(raiz.FullName, "tmp");
            Directory.CreateDirectory(perfil);
            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(temporario);
            var foto = Path.Combine(perfil, "foto.jpg");
            var noCache = Path.Combine(cache, "captura.jpg");
            var noTmp = Path.Combine(temporario, "captura.jpg");
            var irma = Path.Combine(raiz.FullName, "perfil2", "foto.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(irma)!);
            File.WriteAllBytes(foto, [1]);
            File.WriteAllBytes(noCache, [1]);
            File.WriteAllBytes(noTmp, [1]);
            File.WriteAllBytes(irma, [1]);

            Assert.False(LimpezaFotoTemporaria.PodeApagar(foto, perfil, cache, temporario));
            Assert.False(LimpezaFotoTemporaria.PodeApagar(irma, perfil, cache, temporario));
            Assert.False(LimpezaFotoTemporaria.PodeApagar(Path.Combine(raiz.FullName, "outro.jpg"), perfil, cache, temporario));
            Assert.True(LimpezaFotoTemporaria.PodeApagar(noCache, perfil, cache, temporario));
            Assert.True(LimpezaFotoTemporaria.PodeApagar(noTmp, perfil, cache, temporario));
            Assert.False(LimpezaFotoTemporaria.PodeApagar(null, perfil, cache, temporario));
            Assert.False(LimpezaFotoTemporaria.PodeApagar("   ", perfil, cache, temporario));
        }
        finally
        {
            raiz.Delete(recursive: true);
        }
    }

    [Fact]
    public void ArquivoAlheioNoCache_Sobrevive_ENaoEntraNaLista()
    {
        var raiz = Directory.CreateTempSubdirectory("orca-limpeza-alheio-");
        try
        {
            var perfil = Path.Combine(raiz.FullName, "perfil");
            var cache = Path.Combine(raiz.FullName, "cache");
            var temporario = Path.Combine(raiz.FullName, "tmp");
            var picker = Path.Combine(cache, "picker-do-media");
            Directory.CreateDirectory(perfil);
            Directory.CreateDirectory(picker);
            Directory.CreateDirectory(Path.Combine(cache, "orcafacil-espelho"));
            Directory.CreateDirectory(temporario);

            var foto = Path.Combine(picker, "captura.jpg");
            var csv = Path.Combine(cache, "orcafacil-relatorio-20261019-120000.csv");
            var estagio = Path.Combine(cache, "orcafacil-espelho", "estagio.bin");
            var outroNoPicker = Path.Combine(picker, "nao-e-do-fluxo.txt");
            var noTmp = Path.Combine(temporario, "outro.tmp");
            var noPerfil = Path.Combine(perfil, "foto.jpg");
            File.WriteAllBytes(foto, [0xFF, 0xD8]);
            File.WriteAllBytes(csv, "Descrição;Valor"u8.ToArray());
            File.WriteAllBytes(estagio, [2]);
            File.WriteAllBytes(outroNoPicker, [3]);
            File.WriteAllBytes(noTmp, [4]);
            File.WriteAllBytes(noPerfil, [5]);

            var lista = LimpezaFotoTemporaria.ListarRemocao(foto, perfil, cache, temporario);

            Assert.Equal(
                [Path.GetFullPath(foto), Path.GetFullPath(picker)],
                lista);
            Assert.DoesNotContain(lista, item => Mesmo(item, csv));
            Assert.DoesNotContain(lista, item => Mesmo(item, estagio));
            Assert.DoesNotContain(lista, item => Mesmo(item, outroNoPicker));
            Assert.DoesNotContain(lista, item => Mesmo(item, noTmp));
            Assert.DoesNotContain(lista, item => Mesmo(item, noPerfil));
            Assert.DoesNotContain(lista, item => Mesmo(item, cache));
            Assert.DoesNotContain(lista, item => Mesmo(item, Path.Combine(cache, "orcafacil-espelho")));

            foreach (var item in lista)
            {
                if (File.Exists(item))
                    File.Delete(item);
                else if (Directory.Exists(item) && !Directory.EnumerateFileSystemEntries(item).Any())
                    Directory.Delete(item);
            }

            Assert.False(File.Exists(foto));
            Assert.True(Directory.Exists(picker));
            Assert.True(File.Exists(csv));
            Assert.True(File.Exists(estagio));
            Assert.True(File.Exists(outroNoPicker));
            Assert.True(File.Exists(noTmp));
            Assert.True(File.Exists(noPerfil));
            Assert.Equal([], LimpezaFotoTemporaria.ListarRemocao(null, perfil, cache, temporario));
            Assert.Equal([], LimpezaFotoTemporaria.ListarRemocao(noPerfil, perfil, cache, temporario));
        }
        finally
        {
            raiz.Delete(recursive: true);
        }
    }

    [Fact]
    public void PastaVaziaDoPicker_EntraNaLista_EArquivoForaNao()
    {
        var raiz = Directory.CreateTempSubdirectory("orca-limpeza-vazia-");
        try
        {
            var perfil = Path.Combine(raiz.FullName, "perfil");
            var cache = Path.Combine(raiz.FullName, "cache");
            var temporario = Path.Combine(raiz.FullName, "tmp");
            var picker = Path.Combine(temporario, "picker-do-media");
            Directory.CreateDirectory(perfil);
            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(picker);
            var foto = Path.Combine(picker, "captura.jpg");
            var csv = Path.Combine(cache, "orcafacil-relatorio-20261019-120000.csv");
            File.WriteAllBytes(foto, [1]);
            File.WriteAllBytes(csv, [2]);

            var lista = LimpezaFotoTemporaria.ListarRemocao(foto, perfil, cache, temporario);

            Assert.Equal(
                [Path.GetFullPath(foto), Path.GetFullPath(picker)],
                lista);
            Assert.DoesNotContain(lista, item => Mesmo(item, csv));

            foreach (var item in lista)
            {
                if (File.Exists(item))
                    File.Delete(item);
                else if (Directory.Exists(item) && !Directory.EnumerateFileSystemEntries(item).Any())
                    Directory.Delete(item);
            }

            Assert.False(File.Exists(foto));
            Assert.False(Directory.Exists(picker));
            Assert.True(File.Exists(csv));
            Assert.True(Directory.Exists(cache));
            Assert.True(Directory.Exists(temporario));
        }
        finally
        {
            raiz.Delete(recursive: true);
        }
    }

    [Fact]
    public void RaizDeVolume_Cache_TemporarioEPerfil_SaoRecusadosNaEntrada()
    {
        var raiz = Directory.CreateTempSubdirectory("orca-limpeza-raiz-");
        try
        {
            var perfil = Path.Combine(raiz.FullName, "perfil");
            var cache = Path.Combine(raiz.FullName, "cache");
            var temporario = Path.Combine(raiz.FullName, "tmp");
            Directory.CreateDirectory(perfil);
            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(temporario);
            var foto = Path.Combine(cache, "picker", "captura.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(foto)!);
            File.WriteAllBytes(foto, [1]);

            Assert.Empty(LimpezaFotoTemporaria.ListarRemocao(cache, perfil, cache, temporario));
            Assert.Empty(LimpezaFotoTemporaria.ListarRemocao(
                cache + Path.DirectorySeparatorChar, perfil, cache, temporario));
            Assert.Empty(LimpezaFotoTemporaria.ListarRemocao(temporario, perfil, cache, temporario));
            Assert.Empty(LimpezaFotoTemporaria.ListarRemocao(perfil, perfil, cache, temporario));
            Assert.Empty(LimpezaFotoTemporaria.ListarRemocao(Path.GetPathRoot(cache), perfil, cache, temporario));

            var lista = LimpezaFotoTemporaria.ListarRemocao(foto, perfil, cache, temporario);
            Assert.Contains(Path.GetFullPath(foto), lista);
            Assert.DoesNotContain(Path.GetFullPath(cache), lista);
            Assert.DoesNotContain(Path.GetFullPath(perfil), lista);
            Assert.DoesNotContain(Path.GetFullPath(temporario), lista);
        }
        finally
        {
            raiz.Delete(recursive: true);
        }
    }

    private static bool Mesmo(string esquerda, string direita) =>
        string.Equals(Path.GetFullPath(esquerda), Path.GetFullPath(direita), StringComparison.OrdinalIgnoreCase);
}
