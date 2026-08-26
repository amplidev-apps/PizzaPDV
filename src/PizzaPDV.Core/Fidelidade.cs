namespace PizzaPDV.Core;

// Port de packages/shared/src/fidelidade.ts:1
// Regra: a cada 10 pizzas G -> 1 pizza P gratis
public static class Fidelidade
{
    public const int Meta = 10;
    public const string PremioTamanho = "P";

    public record ClienteFidelidade(string Telefone, int PizzasGGanhas, int TotalPizzasG, int CuponsPendentes);

    public static (ClienteFidelidade Cliente, int NovosCupons) RegistrarVenda(ClienteFidelidade cliente, int qtdPizzasG)
    {
        var total = cliente.PizzasGGanhas + qtdPizzasG;
        var novosCupons = total / Meta;
        var atualizado = cliente with
        {
            PizzasGGanhas = total % Meta,
            TotalPizzasG = cliente.TotalPizzasG + qtdPizzasG,
            CuponsPendentes = cliente.CuponsPendentes + novosCupons
        };
        return (atualizado, novosCupons);
    }

    public static bool PodeResgatar(ClienteFidelidade c) => c.CuponsPendentes > 0;

    public static ClienteFidelidade Resgatar(ClienteFidelidade c)
    {
        if (!PodeResgatar(c)) throw new InvalidOperationException("Sem cupons");
        return c with { CuponsPendentes = c.CuponsPendentes - 1 };
    }
}
