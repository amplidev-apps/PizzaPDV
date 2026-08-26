// PizzaPDV — Constantes de negócio validadas com cliente

export const TAMANHOS_PIZZA = {
  P: { label: "Pequena", fatias: 4, maxSabores: 2 },
  G: { label: "Grande", fatias: 8, maxSabores: 2 },
} as const;

export const CATEGORIAS = [
  "pizza",
  "esfiha",
  "calzone",
  "salgado",
  "pastel",
  "bebida",
  "borda",
] as const;

export const BORDAS = [
  { id: "catupiry", nome: "Catupiry", tipo: "comum" },
  { id: "cream_cheese", nome: "Cream Cheese", tipo: "comum" },
  { id: "chocolate", nome: "Chocolate", tipo: "comum" },
  { id: "comum", nome: "Borda Comum", tipo: "comum" },
  { id: "camarao", nome: "Borda Camarão", tipo: "premium" },
  { id: "vulcao", nome: "Borda Vulcão", tipo: "premium" },
] as const;

export const ORIGENS_PEDIDO = ["site", "balcao", "mesa", "telefone"] as const;
export const STATUS_PEDIDO = [
  "recebido",
  "preparo",
  "pronto",
  "entregue",
  "cancelado",
] as const;
export const FORMAS_PAGAMENTO = [
  "dinheiro",
  "pix",
  "pix_pushinpay",
  "cartao_infinitepay",
] as const;

export const MESAS_TOTAL = 20;

// Impressora térmica 58mm JP-58H — 32 cols efetivas (fonte A) / 48 em modo condensado
export const PRINTER_58MM = {
  width: 32,
  widthCondensed: 48,
  model: "JP-58H",
} as const;
