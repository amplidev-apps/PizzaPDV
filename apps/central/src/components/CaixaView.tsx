// apps/central/src/components/CaixaView.tsx:1
import { useState } from "react";

export function CaixaView() {
  const [aberto, setAberto] = useState(true);
  const [saldoInicial] = useState(100);
  return (
    <div className="grid gap-4 max-w-3xl">
      <div className="bg-white rounded border p-4 flex items-center justify-between">
        <div>
          <div className="font-bold">Caixa {aberto ? "Aberto" : "Fechado"}</div>
          <div className="text-sm text-zinc-600">Saldo inicial R$ {saldoInicial.toFixed(2)} • {new Date().toLocaleDateString()}</div>
        </div>
        <button onClick={() => setAberto(!aberto)} className={`px-4 py-2 rounded text-white text-sm ${aberto ? "bg-red-600" : "bg-green-600"}`}>
          {aberto ? "Fechar Caixa" : "Abrir Caixa"}
        </button>
      </div>

      <div className="bg-white rounded border p-4">
        <h3 className="font-semibold">Resumo do dia</h3>
        <div className="grid grid-cols-3 gap-4 mt-3 text-sm">
          <div className="border rounded p-3"><div>Dinheiro</div><div className="font-bold">R$ 320,00</div></div>
          <div className="border rounded p-3"><div>Pix</div><div className="font-bold">R$ 540,00</div></div>
          <div className="border rounded p-3"><div>Cartão InfinitePay</div><div className="font-bold">R$ 210,00</div></div>
        </div>
        <div className="mt-4 text-sm">
          <div className="flex justify-between"><span>Total vendas</span><span className="font-bold">R$ 1.070,00</span></div>
          <div className="flex justify-between"><span>Taxas delivery (12 entregas)</span><span>R$ 84,00 — comissão motoboy</span></div>
          <div className="flex justify-between"><span>Sangrias</span><span>R$ 0,00</span></div>
        </div>
        <div className="mt-3 flex gap-2">
          <button className="border px-3 py-1 rounded text-sm">Sangria</button>
          <button className="border px-3 py-1 rounded text-sm">Suprimento</button>
          <button className="bg-zinc-900 text-white px-3 py-1 rounded text-sm">Imprimir fechamento (JP-58H)</button>
        </div>
      </div>
    </div>
  );
}
