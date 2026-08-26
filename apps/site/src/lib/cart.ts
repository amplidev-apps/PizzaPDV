// apps/site/src/lib/cart.ts:1
// Carrinho com lógica meia-a-meia + borda + taxa por bairro

export type CartItem = {
  id: string; // uuid local
  produtoId: string;
  produtoNome: string;
  categoria: string;
  variacaoId: string;
  tamanho: "P" | "G" | "unico";
  precoBase: number;
  sabor1?: { id: string; nome: string; preco: number };
  sabor2?: { id: string; nome: string; preco: number } | null;
  borda?: { id: string; nome: string; preco: number } | null;
  quantidade: number;
  observacao?: string;
};

export function precoItem(item: CartItem): number {
  // Pizza meia: maior valor dos sabores (regra pedida)
  let base = item.precoBase;
  if (item.categoria === "pizza" && item.sabor1 && item.sabor2) {
    // precoBase já é da variação P/G, mas sabores podem ter preços diferentes
    // Se sabores têm preço próprio, usa maior; senão usa base
    const p1 = item.sabor1.preco ?? base;
    const p2 = item.sabor2.preco ?? base;
    base = Math.max(p1, p2);
  } else if (item.categoria === "pizza" && item.sabor1) {
    base = item.sabor1.preco ?? base;
  }
  const borda = item.borda?.preco ?? 0;
  return (base + borda) * item.quantidade;
}

export function subtotalCart(items: CartItem[]): number {
  return items.reduce((acc, it) => acc + precoItem(it), 0);
}

export function totalComTaxa(subtotal: number, taxa: number): number {
  return subtotal + taxa;
}
