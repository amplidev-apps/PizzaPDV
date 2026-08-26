// apps/site/src/app/page.tsx:1
// Cardápio público — categorias + meia-a-meia + borda cobrada + taxa por bairro
"use client";
import { useState, useEffect } from "react";
import { subtotalCart, totalComTaxa, type CartItem } from "../lib/cart";

// Mock inicial (depois vem do Supabase: produtos + variacoes + bordas + bairros)

const BORDAS = [
  { id: "catupiry", nome: "Catupiry", preco: 8 },
  { id: "cream_cheese", nome: "Cream Cheese", preco: 8 },
  { id: "chocolate", nome: "Chocolate", preco: 9 },
  { id: "comum", nome: "Borda Comum", preco: 5 },
  { id: "camarao", nome: "Borda Camarão", preco: 15 },
  { id: "vulcao", nome: "Borda Vulcão", preco: 18 },
];

const BAIRROS = [
  { id: "centro", nome: "Centro", taxa: 5 },
  { id: "novo", nome: "Bairro Novo", taxa: 7 },
  { id: "sitio", nome: "Sítio Sede", taxa: 8 },
  { id: "rural", nome: "Zona Rural", taxa: 10 },
];

const CARDAPIO = [
  { id: "pizza_g", nome: "Pizza Grande (8 fatias)", categoria: "pizza", preco: 59.9, tam: "G" as const, sabores: ["Calabresa", "Mussarela", "Frango c/ Catupiry", "Portuguesa"] },
  { id: "pizza_p", nome: "Pizza Pequena (4 fatias)", categoria: "pizza", preco: 34.9, tam: "P" as const, sabores: ["Calabresa", "Mussarela", "Frango c/ Catupiry"] },
  { id: "esfiha", nome: "Esfiha Aberta", categoria: "esfiha", preco: 6, tam: "unico" as const },
  { id: "calzone", nome: "Calzone", categoria: "calzone", preco: 22, tam: "unico" as const },
  { id: "pastel", nome: "Pastel", categoria: "pastel", preco: 8, tam: "unico" as const },
];

export default function Page() {
  const [cart, setCart] = useState<CartItem[]>([]);
  const [bairroId, setBairroId] = useState(BAIRROS[0].id);
  const taxa = BAIRROS.find((b) => b.id === bairroId)?.taxa ?? 0;
  const subtotal = subtotalCart(cart);
  const total = totalComTaxa(subtotal, taxa);

  // Modal meia-a-meia simplificado
  const [sel, setSel] = useState<{ prod: typeof CARDAPIO[0] | null; sabor1: string; sabor2: string | null; bordaId: string | null }>({
    prod: null, sabor1: "", sabor2: null, bordaId: null,
  });

  function addToCart() {
    if (!sel.prod) return;
    const borda = BORDAS.find((b) => b.id === sel.bordaId) ?? null;
    const item: CartItem = {
      id: Math.random().toString(36).slice(2),
      produtoId: sel.prod.id,
      produtoNome: sel.prod.nome,
      categoria: sel.prod.categoria,
      variacaoId: sel.prod.id,
      tamanho: sel.prod.tam,
      precoBase: sel.prod.preco,
      sabor1: sel.sabor1 ? { id: sel.sabor1, nome: sel.sabor1, preco: sel.prod.preco } : undefined,
      sabor2: sel.sabor2 ? { id: sel.sabor2, nome: sel.sabor2, preco: sel.prod.preco } : null,
      borda: borda ? { id: borda.id, nome: borda.nome, preco: borda.preco } : null,
      quantidade: 1,
    };
    setCart((c) => [...c, item]);
    setSel({ prod: null, sabor1: "", sabor2: null, bordaId: null });
  }

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_380px]">
      <div>
        <h2 className="text-2xl font-bold mb-2">Cardápio</h2>
        <p className="text-sm text-zinc-600 mb-4">Pizzas P (4 fatias) e G (8 fatias) • Esfihas • Calzones • Meia-a-meia • Bordas cobradas</p>

        <div className="grid gap-3">
          {CARDAPIO.map((p) => (
            <div key={p.id} className="rounded-xl border bg-white p-4 flex items-center justify-between">
              <div>
                <div className="font-semibold">{p.nome}</div>
                <div className="text-sm text-zinc-600">R$ {p.preco.toFixed(2)} {p.categoria === "pizza" && "• meia-a-meia disponível"}</div>
              </div>
              <button onClick={() => setSel({ prod: p, sabor1: p.sabores?.[0] ?? "", sabor2: null, bordaId: null })} className="rounded-lg bg-red-600 px-4 py-2 text-white text-sm hover:bg-red-700">
                Adicionar
              </button>
            </div>
          ))}
        </div>

        {sel.prod && (
          <div className="mt-6 rounded-xl border bg-white p-4">
            <h3 className="font-semibold">Montar: {sel.prod.nome}</h3>
            {sel.prod.categoria === "pizza" && (
              <div className="grid gap-2 mt-3">
                <label className="text-sm">Sabor 1
                  <select value={sel.sabor1} onChange={(e) => setSel({ ...sel, sabor1: e.target.value })} className="ml-2 border rounded px-2 py-1">
                    {sel.prod.sabores?.map((s) => <option key={s} value={s}>{s}</option>)}
                  </select>
                </label>
                <label className="text-sm">Sabor 2 (opcional — meia-a-meia)
                  <select value={sel.sabor2 ?? ""} onChange={(e) => setSel({ ...sel, sabor2: e.target.value || null })} className="ml-2 border rounded px-2 py-1">
                    <option value="">— sem —</option>
                    {sel.prod.sabores?.map((s) => <option key={s} value={s}>{s}</option>)}
                  </select>
                </label>
              </div>
            )}
            <label className="text-sm mt-3 block">Borda cobrada
              <select value={sel.bordaId ?? ""} onChange={(e) => setSel({ ...sel, bordaId: e.target.value || null })} className="ml-2 border rounded px-2 py-1">
                <option value="">Sem borda</option>
                {BORDAS.map((b) => <option key={b.id} value={b.id}>{b.nome} (+R$ {b.preco.toFixed(2)})</option>)}
              </select>
            </label>
            <button onClick={addToCart} className="mt-4 w-full rounded-lg bg-zinc-900 text-white py-2">Confirmar</button>
          </div>
        )}
      </div>

      <aside className="rounded-xl border bg-white p-4 h-fit sticky top-[72px]">
        <h3 className="font-bold">Seu pedido</h3>
        <label className="text-sm mt-3 block">Bairro (taxa fixa)
          <select value={bairroId} onChange={(e) => setBairroId(e.target.value)} className="ml-2 border rounded px-2 py-1">
            {BAIRROS.map((b) => <option key={b.id} value={b.id}>{b.nome} — R$ {b.taxa.toFixed(2)}</option>)}
          </select>
        </label>

        <div className="mt-4 space-y-2">
          {cart.length === 0 && <p className="text-sm text-zinc-500">Carrinho vazio</p>}
          {cart.map((it) => (
            <div key={it.id} className="text-sm flex justify-between border-b py-1">
              <span>{it.produtoNome} {it.sabor1 ? `(${it.sabor1.nome}${it.sabor2 ? "/" + it.sabor2.nome : ""})` : ""} {it.borda ? `+ ${it.borda.nome}` : ""}</span>
              <span>R$ {(it.precoBase + (it.borda?.preco ?? 0)).toFixed(2)}</span>
            </div>
          ))}
        </div>

        <div className="mt-4 text-sm space-y-1">
          <div className="flex justify-between"><span>Subtotal</span><span>R$ {subtotal.toFixed(2)}</span></div>
          <div className="flex justify-between"><span>Taxa entrega</span><span>R$ {taxa.toFixed(2)}</span></div>
          <div className="flex justify-between font-bold text-base"><span>Total</span><span>R$ {total.toFixed(2)}</span></div>
        </div>

        <div className="mt-4 grid gap-2">
          <input placeholder="Seu nome" className="border rounded px-3 py-2 text-sm" />
          <input placeholder="Telefone/WhatsApp" className="border rounded px-3 py-2 text-sm" />
          <input placeholder="Endereço completo" className="border rounded px-3 py-2 text-sm" />
          <select className="border rounded px-3 py-2 text-sm">
            <option>Pix (QR automático PushinPay)</option>
            <option>Dinheiro</option>
            <option>Cartão InfinitePay (na entrega)</option>
          </select>
          <button className="rounded-lg bg-red-600 text-white py-3 font-semibold hover:bg-red-700">Enviar pedido para a Central</button>
          <p className="text-xs text-zinc-500 text-center">Pedido cai na Central em tempo real • Comanda cozinha impressa na JP-58H</p>
        </div>
      </aside>
    </div>
  );
}
