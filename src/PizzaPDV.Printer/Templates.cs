namespace PizzaPDV.Printer;

public record ItemPrint(string Nome, int Qtd, decimal Preco, string? Detalhe, string? Obs);

public record PedidoPrint(
    string Id,
    string Origem,
    string ClienteNome,
    string Telefone,
    string? Endereco,
    string? Bairro,
    decimal Taxa,
    int? Mesa,
    string? Comanda,
    List<ItemPrint> Itens,
    decimal Subtotal,
    decimal Total,
    string FormaPag,
    string Horario,
    string? Observacao
);

public static class Templates
{
    private static string Line(char c='-', int w=32) => new string(c, w);

    public static string ComandaCozinha(PedidoPrint p)
    {
        var lines = new List<string>();
        lines.Add("  *** COZINHA ***");
        lines.Add(Line('=',32));
        if (p.Mesa != null) lines.Add($"MESA {p.Mesa.Value:D2}  {p.Horario}");
        else lines.Add($"{p.Origem.ToUpper()}  {p.Horario}");
        lines.Add($"{p.ClienteNome}  {p.Telefone}");
        if (!string.IsNullOrEmpty(p.Endereco)) lines.Add(p.Endereco);
        if (!string.IsNullOrEmpty(p.Bairro)) lines.Add($"Bairro: {p.Bairro}  Taxa R$ {p.Taxa:F2}");
        lines.Add(Line('-',32));
        foreach (var it in p.Itens)
        {
            lines.Add($"{it.Qtd}x {it.Nome.ToUpper()}");
            if (!string.IsNullOrEmpty(it.Detalhe)) lines.Add($"   {it.Detalhe}");
            if (!string.IsNullOrEmpty(it.Obs)) lines.Add($"   OBS: {it.Obs}");
        }
        if (!string.IsNullOrEmpty(p.Observacao)) lines.Add($"OBS GERAL: {p.Observacao}");
        lines.Add(Line('=',32));
        lines.Add($"PEDIDO #{p.Id.Substring(0, Math.Min(8,p.Id.Length))}");
        lines.Add("");
        return string.Join("\n", lines);
    }

    public static string TicketCliente(PedidoPrint p)
    {
        var lines = new List<string>();
        lines.Add("     PIZZAPDV");
        lines.Add("  Sitio / Centro");
        lines.Add(Line('-',32));
        lines.Add($"Pedido #{p.Id.Substring(0, Math.Min(8,p.Id.Length))}  {p.Horario}");
        lines.Add($"{p.ClienteNome}  {p.Telefone}");
        if (p.Mesa != null) lines.Add($"MESA {p.Mesa.Value:D2}");
        lines.Add(Line('-',32));
        foreach (var it in p.Itens)
        {
            var totalItem = (it.Preco * it.Qtd).ToString("F2");
            var left = $"{it.Qtd}x {it.Nome}";
            if (left.Length > 22) left = left.Substring(0,22);
            lines.Add(left.PadRight(22) + $"R$ {totalItem}".PadLeft(10));
            if (!string.IsNullOrEmpty(it.Detalhe)) lines.Add($"   {it.Detalhe}");
        }
        lines.Add(Line('-',32));
        lines.Add($"Subtotal".PadRight(22) + $"R$ {p.Subtotal:F2}".PadLeft(10));
        lines.Add($"Taxa".PadRight(22) + $"R$ {p.Taxa:F2}".PadLeft(10));
        lines.Add($"TOTAL".PadRight(22) + $"R$ {p.Total:F2}".PadLeft(10));
        lines.Add(Line('-',32));
        lines.Add($"Pagamento: {p.FormaPag}");
        lines.Add("Obrigado pela preferencia!");
        lines.Add("");
        return string.Join("\n", lines);
    }

    public static string TicketDelivery(PedidoPrint p)
    {
        var lines = new List<string>();
        lines.Add("   *** DELIVERY ***");
        lines.Add(Line('=',32));
        lines.Add($"{p.ClienteNome}  {p.Telefone}");
        lines.Add(p.Endereco ?? "");
        if (!string.IsNullOrEmpty(p.Bairro)) lines.Add($"Bairro: {p.Bairro}");
        lines.Add(Line('-',32));
        foreach (var it in p.Itens) lines.Add($"{it.Qtd}x {it.Nome} {(it.Detalhe!=null? "("+it.Detalhe+")":"")}");
        lines.Add(Line('-',32));
        lines.Add($"TOTAL R$ {p.Total:F2}  ({p.FormaPag})");
        lines.Add($"Taxa entregador R$ {p.Taxa:F2}");
        if (!string.IsNullOrEmpty(p.Observacao)) lines.Add($"Obs: {p.Observacao}");
        lines.Add(Line('=',32));
        return string.Join("\n", lines);
    }
}
