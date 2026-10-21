using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dirpf;

/// <summary>Campo do diálogo. O valor zero já gravado continua zero na próxima abertura.</summary>
public sealed class CampoIrrfDarf
{
    public int PerfilId { get; init; }

    public string Perfil { get; init; } = "";

    public int Ano { get; init; }

    public int Mes { get; init; }

    public CestaApuracaoIr Cesta { get; init; }

    public decimal Valor { get; set; }

    /// <summary>Havia linha gravada para esta chave antes desta abertura.</summary>
    public bool FoiSalvo { get; init; }
}

/// <summary>
/// Meses do ano-base com venda e o IRRF já gravado, por perfil e por cesta.
/// A gravação é um upsert. A planilha recebe só o que está gravado nesses meses.
/// </summary>
public static class CapturaIrrfDarf
{
    private static readonly StringComparer Nomes = StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);

    public static IReadOnlyList<CestaApuracaoIr> Cestas { get; } =
        Enum.GetValues<CestaApuracaoIr>().OrderBy(cesta => (int)cesta).ToArray();

    public static string NomeDoMes(int mes)
    {
        if (mes is < 1 or > 12)
            throw new InvalidOperationException("O mês do IRRF informado deve estar entre 1 e 12.");

        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        return cultura.TextInfo.ToTitleCase(cultura.DateTimeFormat.GetMonthName(mes));
    }

    public static decimal Normalizar(decimal valor)
    {
        if (valor < 0m)
            throw new InvalidOperationException("O IRRF informado não pode ser negativo.");

        var arredondado = Math.Round(valor, 2, MidpointRounding.AwayFromZero);
        if (arredondado < 0m)
            throw new InvalidOperationException("O IRRF informado não pode ser negativo.");

        return arredondado;
    }

    public static List<CampoIrrfDarf> Montar(AppDbContext contexto, int ano)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var inicio = new DateTime(ano, 1, 1);
        var fim = inicio.AddYears(1);
        var vendas = contexto.OperacoesAtivo.AsNoTracking()
            .Where(operacao => operacao.Tipo == TipoOperacao.Venda && operacao.Data >= inicio && operacao.Data < fim)
            .Select(operacao => new VendaDoMes(
                operacao.MembroFamiliaId,
                operacao.Data.Month,
                operacao.MembroFamilia!.Nome,
                operacao.MembroFamilia.Apelido))
            .ToList();

        var salvos = contexto.IrrfInformados.AsNoTracking()
            .Where(irrf => irrf.Ano == ano)
            .ToList();
        var porChave = new Dictionary<(int Perfil, int Mes, CestaApuracaoIr Cesta), decimal>();
        foreach (var salvo in salvos)
            porChave[(salvo.MembroFamiliaId, salvo.Mes, salvo.Cesta)] = salvo.Valor;

        var campos = new List<CampoIrrfDarf>();
        foreach (var perfil in vendas
                     .GroupBy(venda => venda.PerfilId)
                     .OrderBy(grupo => Texto(grupo.First()), Nomes))
        {
            var amostra = perfil.First();
            var nome = Texto(amostra);
            foreach (var mes in perfil.Select(venda => venda.Mes).Distinct().OrderBy(mes => mes))
            {
                foreach (var cesta in Cestas)
                {
                    var gravado = porChave.TryGetValue((perfil.Key, mes, cesta), out var valor);
                    campos.Add(new CampoIrrfDarf
                    {
                        PerfilId = perfil.Key,
                        Perfil = nome,
                        Ano = ano,
                        Mes = mes,
                        Cesta = cesta,
                        Valor = gravado ? valor : 0m,
                        FoiSalvo = gravado
                    });
                }
            }
        }

        return campos;
    }

    public static void Salvar(AppDbContext contexto, int ano, IReadOnlyList<CampoIrrfDarf> campos)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(campos);

        var normalizados = new List<(int Perfil, int Mes, CestaApuracaoIr Cesta, decimal Valor)>();
        var vistos = new HashSet<(int, int, CestaApuracaoIr)>();
        foreach (var campo in campos)
        {
            if (campo.Ano != ano)
                throw new InvalidOperationException("O IRRF informado é de outro ano-base.");
            if (campo.Mes is < 1 or > 12)
                throw new InvalidOperationException("O mês do IRRF informado deve estar entre 1 e 12.");
            if (!Enum.IsDefined(campo.Cesta))
                throw new InvalidOperationException($"Cesta de apuração desconhecida: {campo.Cesta}.");

            var valor = Normalizar(campo.Valor);
            if (!vistos.Add((campo.PerfilId, campo.Mes, campo.Cesta)))
                throw new InvalidOperationException("Há dois IRRF informados para o mesmo mês e a mesma cesta.");

            normalizados.Add((campo.PerfilId, campo.Mes, campo.Cesta, valor));
        }

        if (normalizados.Count == 0)
            return;

        var perfis = normalizados.Select(item => item.Perfil).Distinct().ToList();
        var existentesPerfis = contexto.MembrosFamilia
            .Where(membro => perfis.Contains(membro.Id))
            .Select(membro => membro.Id)
            .ToList();
        if (existentesPerfis.Count != perfis.Count)
            throw new InvalidOperationException("O perfil informado não existe.");

        var gravados = contexto.IrrfInformados.Where(irrf => irrf.Ano == ano).ToList();
        var porChave = gravados.ToDictionary(irrf => (irrf.MembroFamiliaId, irrf.Mes, irrf.Cesta));
        foreach (var item in normalizados)
        {
            if (porChave.TryGetValue((item.Perfil, item.Mes, item.Cesta), out var linha))
            {
                linha.Valor = item.Valor;
                continue;
            }

            contexto.IrrfInformados.Add(new IrrfInformado
            {
                MembroFamiliaId = item.Perfil,
                Ano = ano,
                Mes = item.Mes,
                Cesta = item.Cesta,
                Valor = item.Valor
            });
        }

        contexto.SaveChanges();
    }

    /// <summary>O que a planilha recebe: linhas gravadas dos meses que ainda têm venda neste ano.</summary>
    public static List<IrrfManual> Listar(AppDbContext contexto, int ano)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return Montar(contexto, ano)
            .Where(campo => campo.FoiSalvo)
            .Select(campo => new IrrfManual(campo.PerfilId, campo.Ano, campo.Mes, campo.Cesta, campo.Valor))
            .ToList();
    }

    private static string Texto(VendaDoMes venda)
        => string.IsNullOrWhiteSpace(venda.Apelido) ? venda.Nome : $"{venda.Nome} ({venda.Apelido})";

    private sealed record VendaDoMes(int PerfilId, int Mes, string Nome, string? Apelido);
}
