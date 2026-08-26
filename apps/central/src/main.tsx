import React, { useState, useEffect } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import { KanbanPedidos } from "./components/KanbanPedidos";
import { MesasGrid } from "./components/MesasGrid";
import { CaixaView } from "./components/CaixaView";
import { ClientesView } from "./components/ClientesView";

type Tab = "pedidos" | "mesas" | "caixa" | "clientes" | "relatorios";

function App() {
  const [tab, setTab] = useState<Tab>("pedidos");
  const [online, setOnline] = useState(navigator.onLine);

  useEffect(() => {
    const on = () => setOnline(true);
    const off = () => setOnline(false);
    window.addEventListener("online", on);
    window.addEventListener("offline", off);
    return () => { window.removeEventListener("online", on); window.removeEventListener("offline", off); };
  }, []);

  return (
    <div className="min-h-screen bg-zinc-100 text-zinc-900">
      <header className="bg-zinc-900 text-white px-4 py-3 flex items-center justify-between">
        <h1 className="font-bold">PizzaPDV Central</h1>
        <div className="flex items-center gap-3 text-sm">
          <span className={online ? "text-green-400" : "text-red-400"}>● {online ? "Online" : "Offline — salvando local"}</span>
          <span>JP-58H 58mm</span>
          <span>Caixa: Aberto</span>
        </div>
      </header>

      <nav className="bg-white border-b flex gap-1 px-2">
        {(["pedidos", "mesas", "caixa", "clientes", "relatorios"] as Tab[]).map((t) => (
          <button key={t} onClick={() => setTab(t)} className={`px-4 py-2 text-sm font-medium border-b-2 ${tab === t ? "border-red-600 text-red-600" : "border-transparent"}`}>
            {t.toUpperCase()}
          </button>
        ))}
      </nav>

      <main className="p-4">
        {tab === "pedidos" && <KanbanPedidos />}
        {tab === "mesas" && <MesasGrid />}
        {tab === "caixa" && <CaixaView />}
        {tab === "clientes" && <ClientesView />}
        {tab === "relatorios" && <div className="bg-white rounded p-6">Relatórios: vendas por dia, comissão entregador (taxa fixa por bairro), fidelidade 10G→1P. (Fase 2)</div>}
      </main>
    </div>
  );
}

createRoot(document.getElementById("root")!).render(<App />);
