// apps/central/src/components/ClientesView.tsx:1
export function ClientesView() {
  return (
    <div className="bg-white rounded border p-4 max-w-3xl">
      <h3 className="font-bold">Clientes & Fidelidade</h3>
      <p className="text-sm text-zinc-600">Regra: a cada 10 pizzas G → 1 pizza P grátis. Tela mostra contador e botão resgatar.</p>
      <div className="mt-3 border rounded p-3 text-sm">
        <div className="font-semibold">João Silva — 88 9 9999-0000</div>
        <div>Pizzas G acumuladas: 7/10</div>
        <div>Cupons pendentes: 0</div>
        <div className="mt-2 h-2 bg-zinc-200 rounded"><div className="h-2 bg-red-600 rounded" style={{ width: "70%" }} /></div>
        <div className="mt-2">Histórico: 3 pedidos • Ticket médio R$ 58,00</div>
      </div>
      <div className="mt-3 flex gap-2">
        <input placeholder="Buscar por telefone" className="border rounded px-3 py-1 text-sm flex-1" />
        <button className="bg-zinc-900 text-white px-3 py-1 rounded text-sm">Buscar</button>
      </div>
    </div>
  );
}
