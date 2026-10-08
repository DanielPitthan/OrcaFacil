namespace OrcaFacil.Core.Dtos;

public class ResumoMensalDto
{
    public int Mes { get; set; }
    public int Ano { get; set; }
    public decimal TotalReceitas { get; set; }

    /// <summary>Total lançado no mês (pago + a pagar).</summary>
    public decimal TotalDespesas { get; set; }

    /// <summary>Parte do total lançado já marcada como paga.</summary>
    public decimal TotalDespesasPagas { get; set; }

    public decimal TotalDespesasAPagar => TotalDespesas - TotalDespesasPagas;
    public decimal Saldo => TotalReceitas - TotalDespesas;
}

public class GastoPorCategoriaDto
{
    public int CategoriaId { get; set; }
    public string CategoriaNome { get; set; } = string.Empty;
    public string CategoriaIcone { get; set; } = string.Empty;
    public string CategoriaCorHex { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class TendenciaMensalDto
{
    public int Mes { get; set; }
    public int Ano { get; set; }
    public string Rotulo { get; set; } = string.Empty;
    public decimal TotalReceitas { get; set; }
    public decimal TotalDespesas { get; set; }
}

/// <summary>Valores acumulados do dia 1 até <see cref="Dia"/> dentro do mês.</summary>
public class EvolucaoDiariaDto
{
    public int Dia { get; set; }
    public decimal ReceitasAcumuladas { get; set; }
    public decimal DespesasAcumuladas { get; set; }
    public decimal DespesasPagasAcumuladas { get; set; }
    public decimal Saldo => ReceitasAcumuladas - DespesasAcumuladas;
}

public class EvolucaoMensalDto
{
    public ResumoMensalDto Resumo { get; set; } = new();
    public List<EvolucaoDiariaDto> Dias { get; set; } = new();
}

public class AlertaOrcamentoDto
{
    public int CategoriaId { get; set; }
    public string CategoriaNome { get; set; } = string.Empty;
    public string CategoriaIcone { get; set; } = string.Empty;
    public decimal ValorLimite { get; set; }
    public decimal ValorGasto { get; set; }
    public decimal PercentualUsado => ValorLimite <= 0 ? 0 : Math.Round(ValorGasto / ValorLimite * 100m, 1);
}

public class OrcamentoItemDto
{
    public int? OrcamentoId { get; set; }
    public int CategoriaId { get; set; }
    public string CategoriaNome { get; set; } = string.Empty;
    public string CategoriaIcone { get; set; } = string.Empty;
    public string CategoriaCorHex { get; set; } = string.Empty;
    public decimal ValorLimite { get; set; }
    public decimal ValorGasto { get; set; }
    public decimal PercentualUsado => ValorLimite <= 0 ? 0 : Math.Round(ValorGasto / ValorLimite * 100m, 1);

    /// <summary>Gasto da mesma categoria no mês anterior.</summary>
    public decimal ValorGastoMesAnterior { get; set; }

    /// <summary>Diferença de gasto vs mês anterior (positivo = gastou mais).</summary>
    public decimal DiferencaVsMesAnterior => ValorGasto - ValorGastoMesAnterior;

    /// <summary>Variação percentual vs mês anterior. Null se não houve gasto anterior.</summary>
    public decimal? VariacaoPercentualVsMesAnterior =>
        ValorGastoMesAnterior <= 0
            ? null
            : Math.Round(DiferencaVsMesAnterior / ValorGastoMesAnterior * 100m, 1);

    public bool Economizando => DiferencaVsMesAnterior < 0;
    public bool GastandoMais => DiferencaVsMesAnterior > 0;

    public decimal? MetaReducaoPercentual { get; set; }

    /// <summary>Teto de gasto para cumprir a meta de redução (gasto anterior × (1 − meta%)).</summary>
    public decimal? MetaReducaoValorAlvo =>
        MetaReducaoPercentual is null || ValorGastoMesAnterior <= 0
            ? null
            : Math.Round(ValorGastoMesAnterior * (1m - MetaReducaoPercentual.Value / 100m), 2);

    public bool? MetaReducaoAtingida =>
        MetaReducaoValorAlvo is null ? null : ValorGasto <= MetaReducaoValorAlvo.Value;
}
