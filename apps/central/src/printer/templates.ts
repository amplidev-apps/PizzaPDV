// apps/central/src/printer/templates.ts:1
// Templates 58mm JP-58H — 32 cols (fonte normal) / 48 condensado
// Usando node-thermal-printer (ESC/POS)

export type PedidoPrint = {
  id: string;
  origem: string;
  clienteNome: string;
  telefone: string;
  endereco?: string;
  bairro?: string;
  taxa: number;
  mesa?: number | null;
  comanda?: string | null;
  itens: { nome: string; qtd: number; obs?: string; preco: number; detalhe?: string }[];
  subtotal: number;
  total: number;
  formaPag: string;
  horario: string;
  observacao?: string;
};

function line(char = "-", width = 32) { return char.repeat(width); }
function pad2(n: number) { return n.toFixed(2).padStart(6); }

export function comandaCozinha(p: PedidoPrint): string {
  // Fonte grande, sem preço, com MESA marcada
  const lines: string[] = [];
  lines.push("  *** COZINHA ***");
  lines.push(line("="));
  if (p.mesa) lines.push(`MESA ${String(p.mesa).padStart(2, "0")}  ${p.horario}`);
  else lines.push(`${p.origem.toUpperCase()}  ${p.horario}`);
  lines.push(`${p.clienteNome}  ${p.telefone}`);
  if (p.endereco) lines.push(p.endereco);
  if (p.bairro) lines.push(`Bairro: ${p.bairro}  Taxa R$ ${p.taxa.toFixed(2)}`);
  lines.push(line("-"));
  for (const it of p.itens) {
    lines.push(`${it.qtd}x ${it.nome.toUpperCase()}`);
    if (it.detalhe) lines.push(`   ${it.detalhe}`);
    if (it.obs) lines.push(`   OBS: ${it.obs}`);
  }
  if (p.observacao) lines.push(`OBS GERAL: ${p.observacao}`);
  lines.push(line("="));
  lines.push(`PEDIDO #${p.id.slice(0, 8)}`);
  lines.push("");
  return lines.join("\n");
}

export function ticketCliente(p: PedidoPrint): string {
  const lines: string[] = [];
  lines.push("     PIZZAPDV");
  lines.push("  Sítio / Centro");
  lines.push(line("-"));
  lines.push(`Pedido #${p.id.slice(0, 8)}  ${p.horario}`);
  lines.push(`${p.clienteNome}  ${p.telefone}`);
  if (p.mesa) lines.push(`MESA ${String(p.mesa).padStart(2, "0")}`);
  lines.push(line("-"));
  for (const it of p.itens) {
    const totalItem = (it.preco * it.qtd).toFixed(2);
    lines.push(`${it.qtd}x ${it.nome}`.padEnd(22) + `R$ ${totalItem}`.padStart(10));
    if (it.detalhe) lines.push(`   ${it.detalhe}`);
  }
  lines.push(line("-"));
  lines.push(`Subtotal`.padEnd(22) + `R$ ${pad2(p.subtotal)}`.padStart(10));
  lines.push(`Taxa`.padEnd(22) + `R$ ${pad2(p.taxa)}`.padStart(10));
  lines.push(`TOTAL`.padEnd(22) + `R$ ${pad2(p.total)}`.padStart(10));
  lines.push(line("-"));
  lines.push(`Pagamento: ${p.formaPag}`);
  lines.push("Obrigado pela preferência!");
  lines.push("");
  return lines.join("\n");
}

export function ticketDelivery(p: PedidoPrint): string {
  const lines: string[] = [];
  lines.push("   *** DELIVERY ***");
  lines.push(line("="));
  lines.push(`${p.clienteNome}  ${p.telefone}`);
  lines.push(p.endereco ?? "");
  if (p.bairro) lines.push(`Bairro: ${p.bairro}`);
  lines.push(line("-"));
  for (const it of p.itens) lines.push(`${it.qtd}x ${it.nome} ${it.detalhe ? "(" + it.detalhe + ")" : ""}`);
  lines.push(line("-"));
  lines.push(`TOTAL R$ ${p.total.toFixed(2)}  (${p.formaPag})`);
  lines.push(`Taxa entregador R$ ${p.taxa.toFixed(2)}`);
  lines.push(p.observacao ? `Obs: ${p.observacao}` : "");
  lines.push(line("="));
  return lines.join("\n");
}
