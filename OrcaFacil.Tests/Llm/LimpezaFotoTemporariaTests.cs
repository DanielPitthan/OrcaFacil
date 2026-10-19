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
}
