// apps/central/src/sync/outbox.ts:1
// Padrão Outbox — offline-first: grava local + outbox, sync worker envia quando online

import { getDb } from "../db/client.js";
import { outbox, pedidosLocal } from "../db/schema.js";
import { createClient } from "@supabase/supabase-js";

export async function enqueue(tableName: string, op: string, payload: unknown) {
  const db = getDb();
  const id = `out_${Date.now()}_${Math.random().toString(36).slice(2, 6)}`;
  // @ts-ignore drizzle insert
  db.insert(outbox).values({ id, tableName, op, payloadJson: JSON.stringify(payload), createdAt: new Date().toISOString() }).run();
}

export function salvarPedidoLocal(pedido: any) {
  const db = getDb();
  const id = pedido.id ?? `tmp_${Date.now()}`;
  const now = new Date().toISOString();
  // @ts-ignore
  db.insert(pedidosLocal).values({
    id,
    origem: pedido.origem,
    status: pedido.status ?? "recebido",
    clienteNome: pedido.cliente?.nome ?? pedido.cliente_nome,
    clienteTelefone: pedido.cliente?.telefone ?? pedido.cliente_telefone,
    bairroId: pedido.bairroId ?? pedido.bairro_id ?? null,
    taxaEntrega: pedido.taxaEntrega ?? pedido.taxa_entrega ?? 0,
    mesaNumero: pedido.mesaNumero ?? pedido.mesa_numero ?? null,
    total: pedido.total,
    formaPagamento: pedido.formaPagamento ?? pedido.forma_pagamento,
    statusPagamento: pedido.statusPagamento ?? pedido.status_pagamento ?? "pendente",
    payloadJson: JSON.stringify(pedido),
    synced: false,
    createdAt: now,
    updatedAt: now,
  }).run();
  enqueue("pedidos", "insert", { ...pedido, id });
  return id;
}

// Worker: tenta enviar pendentes para Supabase quando online
export async function syncOutbox() {
  const supabaseUrl = process.env.SUPABASE_URL ?? process.env.VITE_SUPABASE_URL;
  const supabaseKey = process.env.SUPABASE_SERVICE_ROLE_KEY ?? process.env.VITE_SUPABASE_ANON_KEY;
  if (!supabaseUrl || !supabaseKey) {
    console.warn("[sync] Supabase não configurado — rodando offline puro");
    return { synced: 0, pending: 1 };
  }
  const supabase = createClient(supabaseUrl, supabaseKey);
  const db = getDb();
  // @ts-ignore select
  const pendentes = db.select().from(outbox).all() as any[];
  let synced = 0;
  for (const row of pendentes) {
    try {
      const payload = JSON.parse(row.payloadJson);
      if (row.tableName === "pedidos" && row.op === "insert") {
        // Remove tmp_ prefix handling: insere e pega id real
        const toInsert = { ...payload };
        if (toInsert.id?.startsWith("tmp_") || toInsert.id?.startsWith("out_")) delete toInsert.id;
        const { error } = await supabase.from("pedidos").insert(toInsert);
        if (error) throw error;
      }
      // @ts-ignore delete
      db.delete(outbox).where((() => { const { eq } = require("drizzle-orm"); return eq(outbox.id, row.id); })()).run();
      synced++;
    } catch (e) {
      console.error("[sync] falha", row.id, e);
      // incrementa tentativas
      // @ts-ignore
      const { sql } = await import("drizzle-orm");
      db.run(sql`UPDATE outbox SET attempts = attempts + 1 WHERE id = ${row.id}`);
    }
  }
  return { synced, pending: pendentes.length - synced };
}

// Polling: tenta a cada 10s se online
export function startSyncLoop(intervalMs = 10_000) {
  setInterval(async () => {
    if (navigator.onLine) await syncOutbox();
  }, intervalMs);
}
