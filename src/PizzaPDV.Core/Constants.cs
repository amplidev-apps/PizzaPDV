namespace PizzaPDV.Core;

public static class Constants
{
    public const int MesasTotal = 20;
    public const int PrinterWidth32 = 32;
    public const int PrinterWidth48 = 48;
    public const string PrinterModel = "JP-58H";

    public static readonly string[] Categorias = { "pizza","esfiha","calzone","salgado","pastel","bebida","borda" };
    public static readonly string[] OrigensPedido = { "site","balcao","mesa","telefone" };
    public static readonly string[] StatusPedido = { "recebido","preparo","pronto","entregue","cancelado" };
    public static readonly string[] FormasPagamento = { "dinheiro","pix","pix_pushinpay","cartao_infinitepay" };

    public static readonly (string Id, string Nome, string Tipo)[] BordasFixas =
    {
        ("catupiry","Catupiry","comum"),
        ("cream_cheese","Cream Cheese","comum"),
        ("chocolate","Chocolate","comum"),
        ("comum","Borda Comum","comum"),
        ("camarao","Borda Camarao","premium"),
        ("vulcao","Borda Vulcao","premium"),
    };
}
