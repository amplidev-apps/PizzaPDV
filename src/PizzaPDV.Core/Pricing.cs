namespace PizzaPDV.Core;

// Port de packages/shared/src/pricing.ts:1
public record Insumo(string Nome, decimal CustoPorKg, decimal GramasPorPizza);

public record InsumoPorcao(string Nome, decimal CustoPorUnidade, string Unidade, decimal Quantidade, string? FormaCorte, decimal FatorPerda = 1m);

public static class Pricing
{
    public static decimal CustoSabor(IEnumerable<Insumo> insumos)
        => insumos.Sum(i => (i.CustoPorKg / 1000m) * i.GramasPorPizza);

    public static decimal CustoInsumo(decimal custoPorUnidade, decimal quantidade, decimal fatorPerda = 1m)
        => custoPorUnidade * quantidade * fatorPerda;

    public static decimal CustoRecheioManipulado(IEnumerable<InsumoPorcao> itens)
        => itens.Sum(i => CustoInsumo(i.CustoPorUnidade, i.Quantidade, i.FatorPerda));

    public static decimal CustoPorPorcao(decimal custoTotal, int porcoes) => porcoes == 0 ? custoTotal : custoTotal / porcoes;

    public static decimal CustoPorPorcao120g(decimal custoPorG) => custoPorG * 120m;

    public static decimal PrecoPorcao(decimal custoPorcao, decimal margem) => PrecoSugerido(custoPorcao, margem);

    public static decimal PrecoMeiaMedia(decimal precoSabor1, decimal precoSabor2) => (precoSabor1 + precoSabor2) / 2m;

    public static decimal FatorCorte(string? forma)
        => forma switch { "cubos" => 1.10m, "fatiado" => 1.05m, "ralado" => 1.03m, _ => 1.0m };

    public static decimal CustoPizzaMeia(decimal custoSabor1, decimal? custoSabor2, decimal custoMassa, decimal custoBorda, decimal embalagem)
    {
        var media = custoSabor2 == null ? custoSabor1 : (custoSabor1 + custoSabor2.Value) / 2m;
        return media + custoMassa + custoBorda + embalagem;
    }

    public static decimal PrecoSugerido(decimal custoTotal, decimal margemDesejada)
    {
        if (margemDesejada >= 1m) throw new ArgumentException("Margem deve ser < 1");
        return custoTotal / (1m - margemDesejada);
    }

    public static decimal MargemReal(decimal custoTotal, decimal precoVenda)
        => precoVenda == 0 ? 0 : (precoVenda - custoTotal) / precoVenda;

    public static decimal PrecoMeiaMaiorValor(decimal precoSabor1, decimal? precoSabor2)
        => precoSabor2 == null ? precoSabor1 : Math.Max(precoSabor1, precoSabor2.Value);

    public static (decimal NovoCusto, decimal NovoPreco, decimal Impacto) SimularAumento(decimal custoAtual, decimal percentual, decimal margem)
    {
        var novoCusto = custoAtual * (1m + percentual);
        return (novoCusto, PrecoSugerido(novoCusto, margem), novoCusto - custoAtual);
    }
}
