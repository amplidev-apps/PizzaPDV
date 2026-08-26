// apps/central/src/components/KanbanPedidos.tsx:1
import { useEffect, useState } from "react";

type Pedido = { id: string; origem: string; cliente: string; total: number; status: "recebido" | "preparo" | "pronto" | "entregue"; mesa?: number | null; bairro?: string; itens: string };

const MOCK: Pedido[] = [
  { id: "a1b2", origem: "site", cliente: "João (Centro)", total: 64.9, status: "recebido", itens: "1x G Calabresa/Mussarela + Catupiry" },
  { id: "c3d4", origem: "mesa", cliente: "Mesa 07", total: 42.0, status: "preparo", mesa: 7, itens: "2x Esfiha + 1x Calzone" },
];

export function KanbanPedidos() {
  const [pedidos, setPedidos] = useState<Pedido[]>(MOCK);

  // Realtime Supabase (quando configurado)
  useEffect(() => {
    // supabase.channel('pedidos').on('postgres_changes', { event: '*', schema: 'public', table: 'pedidos' }, (payload) => ...)
  }, []);

  function move(id: string, status: Pedido["status"]) {
    setPedidos((ps) => ps.map((p) => (p.id === id ? { ...p, status } : p)));
  }

  async function imprimir(id: string, tipo: "cozinha" | "cliente" | "delivery") {
    // @ts-ignore
    const res = await window.pizzaPDV?.print({ template: tipo, data: { id, horario: new Date().toLocaleTimeString() } });
    console.log("[print]", res);
    alert(`Impressão ${tipo} enviada para JP-58H (ver console). Mock OK se sem impressora.`);
  }

  const cols: Pedido["status"][] = ["recebido", "preparo", "pronto", "entregue"];

  return (
    <div className="grid grid-cols-4 gap-3">
      {cols.map((col) => (
        <div key={col} className="bg-white rounded border">
          <div className="px-3 py-2 font-bold text-sm border-b uppercase">{col} ({pedidos.filter((p) => p.status === col).length})</div>
          <div className="p-2 space-y-2 min-h-[400px]">
            {pedidos.filter((p) => p.status === col).map((p) => (
              <div key={p.id} className="border rounded p-3 text-sm bg-zinc-50">
                <div className="font-semibold">#{p.id} • {p.origem} {p.mesa ? `• MESA ${p.mesa}` : ""}</div>
                <div>{p.cliente}</div>
                <div className="text-zinc-600">{p.itens}</div>
                <div className="font-bold">R$ {p.total.toFixed(2)}</div>
                <div className="flex gap-1 mt-2 flex-wrap">
                  {col === "recebido" && <button onClick={() => move(p.id, "preparo")} className="bg-red-600 text-white px-2 py-1 rounded text-xs">Aceitar</button>}
                  {col === "preparo" && <button onClick={() => move(p.id, "pronto")} className="bg-amber-600 text-white px-2 py-1 rounded text-xs">Pronto</button>}
                  {col === "pronto" && <button onClick={() => move(p.id, "entregue")} className="bg-green-600 text-white px-2 py-1 rounded text-xs">Entregar</button>}
                  <button onClick={() => imprimir(p.id, "cozinha")} className="bg-zinc-900 text-white px-2 py-1 rounded text-xs">Cozinha</button>
                  <button onClick={() => imprimir(p.id, "cliente")} className="border px-2 py-1 rounded text-xs">Cliente</button>
                </div>
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
