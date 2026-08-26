// apps/site/src/lib/catalog.ts:1
// Site consome catálogo da Central via Supabase (read-only) + Realtime
import { supabase } from "./supabase";

export type CatalogItem = {
  id: string;
  nome: string;
  categoria: string;
  descricao?: string;
  preco: number;
  tamanho?: string;
  fatias?: number;
  ativo: boolean;
};

export async function fetchCatalog() {
  const [produtos, variacoes, bordas, bairros, sabores] = await Promise.all([
    supabase.from("produtos").select("*").eq("ativo", true),
    supabase.from("variacoes").select("*").eq("ativo", true),
    supabase.from("bordas").select("*").eq("ativo", true),
    supabase.from("bairros").select("*").eq("ativo", true),
    supabase.from("sabores").select("*").eq("ativo", true),
  ]);
  return { produtos: produtos.data ?? [], variacoes: variacoes.data ?? [], bordas: bordas.data ?? [], bairros: bairros.data ?? [], sabores: sabores.data ?? [] };
}

export function subscribeCatalog(onChange: () => void) {
  const ch = supabase
    .channel("catalog")
    .on("postgres_changes", { event: "*", schema: "public", table: "produtos" }, onChange)
    .on("postgres_changes", { event: "*", schema: "public", table: "variacoes" }, onChange)
    .on("postgres_changes", { event: "*", schema: "public", table: "bordas" }, onChange)
    .on("postgres_changes", { event: "*", schema: "public", table: "bairros" }, onChange)
    .subscribe();
  return () => { supabase.removeChannel(ch); };
}
