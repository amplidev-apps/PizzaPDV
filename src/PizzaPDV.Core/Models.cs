namespace PizzaPDV.Core;

// Espelho de packages/shared/src/schemas.ts + Supabase schema.sql

public record Produto(
    string Id,
    string Nome,
    string Categoria,
    string? Descricao,
    bool Ativo,
    int MaxSabores,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record Variacao(
    string Id,
    string ProdutoId,
    string Tamanho, // P/G/unico
    int? Fatias,
    decimal Preco,
    decimal Custo,
    bool Ativo,
    DateTime UpdatedAt
);

public record Sabor(
    string Id,
    string Nome,
    string Categoria, // salgada/doce/mista
    decimal Custo,
    bool Ativo,
    DateTime UpdatedAt
);

public record Borda(
    string Id,
    string Nome,
    decimal PrecoAdicional,
    decimal Custo,
    string Tipo, // comum/premium
    bool Ativo,
    DateTime UpdatedAt
);

public record Bairro(
    string Id,
    string Nome,
    decimal TaxaFixa,
    bool Ativo,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record Mesa(
    int Numero,
    string Status, // livre/ocupada/conta
    string? ComandaAberta,
    DateTime UpdatedAt
);

public record Cliente(
    string Id,
    string Nome,
    string Telefone,
    string? BairroId,
    int TotalPizzasG,
    int PizzasGParaFidelidade,
    int CuponsPendentes
);

public record ItemPedido(
    string Id,
    string PedidoId,
    string ProdutoId,
    string VariacaoId,
    string? Sabor1Id,
    string? Sabor2Id,
    string? BordaId,
    int Quantidade,
    string? Observacao,
    decimal PrecoUnit
);

public record Pedido(
    string Id,
    string Origem,
    string Status,
    string? ClienteId,
    string ClienteNome,
    string ClienteTelefone,
    string? ClienteEndereco,
    string? BairroId,
    decimal TaxaEntrega,
    int? MesaNumero,
    string? ComandaId,
    decimal Subtotal,
    decimal Total,
    string FormaPagamento,
    string StatusPagamento,
    string? Observacao,
    string? EntregadorId,
    string? PixTxid,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record Caixa(
    string Id,
    DateTime AbertoEm,
    DateTime? FechadoEm,
    decimal SaldoInicial,
    decimal TotalVendas,
    string PorFormaJson
);

public record OutboxItem(
    string Id,
    string TableName,
    string Op, // insert/update/delete
    string PayloadJson,
    int Attempts,
    DateTime CreatedAt
);
