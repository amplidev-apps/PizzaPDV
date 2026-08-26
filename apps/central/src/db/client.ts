// apps/central/src/db/client.ts:1
import Database from "better-sqlite3";
import { drizzle } from "drizzle-orm/better-sqlite3";
import * as schema from "./schema.js";

const dbPath = process.env.PIZZAPDV_DB_PATH || "./pizzapdv.db";
let _db: ReturnType<typeof drizzle> | null = null;

export function getDb() {
  if (_db) return _db;
  const sqlite = new Database(dbPath);
  sqlite.pragma("journal_mode = WAL");
  // Cria tabelas se não existem (migração simples v1)
  sqlite.exec(`
    CREATE TABLE IF NOT EXISTS pedidos_local (id TEXT PRIMARY KEY, origem TEXT NOT NULL, status TEXT NOT NULL, cliente_nome TEXT NOT NULL, cliente_telefone TEXT NOT NULL, bairro_id TEXT, taxa_entrega REAL NOT NULL DEFAULT 0, mesa_numero INTEGER, total REAL NOT NULL, forma_pagamento TEXT NOT NULL, status_pagamento TEXT NOT NULL DEFAULT 'pendente', payload_json TEXT NOT NULL, synced INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
    CREATE TABLE IF NOT EXISTS outbox (id TEXT PRIMARY KEY, table_name TEXT NOT NULL, op TEXT NOT NULL, payload_json TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL);
    CREATE TABLE IF NOT EXISTS produtos_local (id TEXT PRIMARY KEY, nome TEXT NOT NULL, categoria TEXT NOT NULL, preco REAL NOT NULL, payload_json TEXT NOT NULL, updated_at TEXT NOT NULL);
    CREATE TABLE IF NOT EXISTS caixa_local (id TEXT PRIMARY KEY, aberto_em TEXT NOT NULL, fechado_em TEXT, saldo_inicial REAL NOT NULL, total_vendas REAL NOT NULL DEFAULT 0, por_forma_json TEXT NOT NULL DEFAULT '{}', synced INTEGER NOT NULL DEFAULT 0);
  `);
  _db = drizzle(sqlite, { schema });
  return _db;
}
