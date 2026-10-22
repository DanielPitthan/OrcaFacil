using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Exportacao;
using Serilog;

namespace OrcaFacil.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var context = await factory.CreateDbContextAsync();
        ResultadoPreparoBanco resultado;
        try
        {
            resultado = await PreparoBanco.MigrarEVerificarAsync(
                context,
                new OpcoesPreparoBanco { VersaoAplicativo = VersaoAplicativo() });
        }
        catch (Exception ex)
        {
            resultado = FalhaSemRelatorio(context, ex);
        }

        Registrar(resultado);
        if (resultado.ExigeRecuperacao)
            throw new IntegridadeBancoException(resultado);

        var haMembro = await context.MembrosFamilia.AnyAsync();
        if (PoliticaSeed.DevePopular(resultado, haMembro))
            await SeedData.PopularAsync(context);
    }

    private static string VersaoAplicativo()
    {
        try
        {
            return AppInfo.VersionString;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return "desconhecida";
        }
    }

    private static ResultadoPreparoBanco FalhaSemRelatorio(AppDbContext context, Exception ex)
    {
        RelatorioIntegridadeBanco relatorio;
        try
        {
            relatorio = VerificadorIntegridadeBanco.RelatorioIlegivel(context);
        }
        catch (Exception)
        {
            relatorio = RelatorioIntegridadeBanco.IlegivelMinimo;
        }

        return new ResultadoPreparoBanco(ex, relatorio, aviso: PreparoBanco.AvisoArquivoIlegivel);
    }

    private static void Registrar(ResultadoPreparoBanco resultado) =>
        RegistroPreparoBanco.Registrar(resultado, AdaptadorLog.Instancia);

    private sealed class AdaptadorLog : IRegistroLog
    {
        public static AdaptadorLog Instancia { get; } = new();

        public void Informacao(string mensagem, object?[] argumentos) =>
            Log.Information(mensagem, argumentos);

        public void Erro(Exception? excecao, string mensagem, object?[] argumentos)
        {
            var valores = new object?[argumentos.Length + 1];
            argumentos.CopyTo(valores, 0);
            valores[^1] = VarreduraPlanilhaExportada.TipoDaFalha(excecao);
            Log.Error(mensagem + " Tipo: {Tipo}", valores);
        }
    }
}
