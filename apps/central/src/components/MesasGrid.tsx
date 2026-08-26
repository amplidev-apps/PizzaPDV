// apps/central/src/components/MesasGrid.tsx:1
import { useState } from "react";

type Mesa = { numero: number; status: "livre" | "ocupada" | "conta"; total: number };

export function MesasGrid() {
  const [mesas, setMesas] = useState<Mesa[]>(Array.from({ length: 20 }, (_, i) => ({ numero: i + 1, status: i < 2 ? "ocupada" : "livre", total: i === 0 ? 89.9 : 0 })));
  const [mesaSel, setMesaSel] = useState<number | null>(null);

  function abrirMesa(n: number) {
    setMesas((ms) => ms.map((m) => (m.numero === n ? { ...m, status: "ocupada" as const } : m)));
    setMesaSel(n);
  }

  return (
    <div>
      <div className="grid grid-cols-5 gap-3">
        {mesas.map((m) => (
          <button key={m.numero} onClick={() => (m.status === "livre" ? abrirMesa(m.numero) : setMesaSel(m.numero))}
            className={`rounded-xl border-2 p-4 text-left ${m.status === "livre" ? "bg-white border-zinc-200" : m.status === "ocupada" ? "bg-amber-100 border-amber-400" : "bg-red-100 border-red-400"}`}>
            <div className="font-bold">Mesa {String(m.numero).padStart(2, "0")}</div>
            <div className="text-xs uppercase">{m.status}</div>
            {m.total > 0 && <div className="text-sm font-semibold">R$ {m.total.toFixed(2)}</div>}
          </button>
        ))}
      </div>

      {mesaSel && (
        <div className="mt-6 bg-white rounded border p-4">
          <h3 className="font-bold">Comanda — Mesa {String(mesaSel).padStart(2, "0")}</h3>
          <p className="text-sm text-zinc-600">Adicione itens (pizza P/G meia-a-meia, esfihas, calzones, bordas). Ao enviar, imprime comanda cozinha com MESA marcada.</p>
          <div className="mt-3 grid gap-2">
            <div className="flex gap-2">
              <select className="border rounded px-2 py-1 text-sm"><option>Pizza G — R$ 59,90</option><option>Pizza P — R$ 34,90</option><option>Esfiha — R$ 6,00</option></select>
              <select className="border rounded px-2 py-1 text-sm"><option>Calabresa</option><option>Mussarela</option></select>
              <select className="border rounded px-2 py-1 text-sm"><option>Sem borda</option><option>Catupiry +R$8</option><option>Vulcão +R$18</option></select>
              <button className="bg-zinc-900 text-white px-3 py-1 rounded text-sm">Adicionar</button>
            </div>
            <div className="flex gap-2">
              <button className="bg-red-600 text-white px-4 py-2 rounded text-sm">Enviar p/ Cozinha (JP-58H)</button>
              <button className="border px-4 py-2 rounded text-sm">Fechar Conta (imprime total)</button>
              <button onClick={() => setMesaSel(null)} className="text-sm px-3">Fechar</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
