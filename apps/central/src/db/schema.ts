// apps/central/src/db/schema.ts:1
// Drizzle SQLite — espelho local do Supabase para offline-first

import { sqliteTable, text, integer, real } from "drizzle-orm/sqlite-core";

export const pedidosLocal = sqliteTable("pedidos_local", {
  id: text("id").primaryKey(), // uuid ou tmp_xxx
  origem: text("origem").notNull(),
  status: text("status").notNull(),
  clienteNome: text("cliente_nome").notNull(),
  clienteTelefone: text("cliente_telefone").notNull(),
  bairroId: text("bairro_id"),
  taxaEntrega: real("taxa_entrega").notNull().default(0),
  mesaNumero: integer("mesa_numero"),
  total: real("total").notNull(),
  formaPagamento: text("forma_pagamento").notNull(),
  statusPagamento: text("status_pagamento").notNull().default("pendente"),
  payloadJson: text("payload_json").notNull(), // JSON completo do pedido
  synced: integer("synced", { mode: "boolean" }).notNull().default(false),
  createdAt: text("created_at").notNull(),
  updatedAt: text("updated_at").notNull(),
});

export const outbox = sqliteTable("outbox", {
  id: text("id").primaryKey(),
  tableName: text("table_name").notNull(),
  op: text("op").notNull(), // insert | update | delete
  payloadJson: text("payload_json").notNull(),
  attempts: integer("attempts").notNull().default(0),
  createdAt: text("created_at").notNull(),
});

export const produtosLocal = sqliteTable("produtos_local", {
  id: text("id").primaryKey(),
  nome: text("nome").notNull(),
  categoria: text("categoria").notNull(),
  preco: real("preco").notNull(),
  payloadJson: text("payload_json").notNull(),
  updatedAt: text("updated_at").notNull(),
});

export const caixaLocal = sqliteTable("caixa_local", {
  id: text("id").primaryKey(),
  abertoEm: text("aberto_em").notNull(),
  fechadoEm: text("fechado_em"),
  saldoInicial: real("saldo_inicial").notNull(),
  totalVendas: real("total_vendas").notNull().default(0),
  porFormaJson: text("por_forma_json").notNull().default("{}"),
  synced: integer("synced", { mode: "boolean" }).notNull().default(false),
});
