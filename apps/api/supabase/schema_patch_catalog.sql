-- Patch Catálogo — Central dona do cardápio (Etapa C)
-- Execute após schema.sql no Supabase SQL Editor
-- apps/api/supabase/schema_patch_catalog.sql:1

-- Adiciona updated_at onde falta
alter table produtos add column if not exists updated_at timestamptz default now();
alter table variacoes add column if not exists updated_at timestamptz default now();
alter table sabores add column if not exists updated_at timestamptz default now();
alter table bordas add column if not exists updated_at timestamptz default now();
alter table bairros add column if not exists updated_at timestamptz default now();

-- Triggers updated_at
create or replace function set_updated_at() returns trigger as $$
begin new.updated_at = now(); return new; end; $$ language plpgsql;

drop trigger if exists trg_produtos_updated on produtos;
create trigger trg_produtos_updated before update on produtos for each row execute function set_updated_at();
drop trigger if exists trg_variacoes_updated on variacoes;
create trigger trg_variacoes_updated before update on variacoes for each row execute function set_updated_at();
drop trigger if exists trg_sabores_updated on sabores;
create trigger trg_sabores_updated before update on sabores for each row execute function set_updated_at();
drop trigger if exists trg_bordas_updated on bordas;
create trigger trg_bordas_updated before update on bordas for each row execute function set_updated_at();
drop trigger if exists trg_bairros_updated on bairros;
create trigger trg_bairros_updated before update on bairros for each row execute function set_updated_at();

-- Realtime para catálogo (Site consome)
alter publication supabase_realtime add table produtos;
alter publication supabase_realtime add table variacoes;
alter publication supabase_realtime add table bordas;
alter publication supabase_realtime add table bairros;
alter publication supabase_realtime add table sabores;

-- RLS: anon só lê catálogo ativo (Central escreve com service_role)
alter table produtos enable row level security;
alter table variacoes enable row level security;
alter table bordas enable row level security;
alter table bairros enable row level security;
alter table sabores enable row level security;

drop policy if exists "catalog_read" on produtos;
create policy "catalog_read" on produtos for select using (ativo = true);
drop policy if exists "catalog_read" on variacoes;
create policy "catalog_read" on variacoes for select using (ativo = true);
drop policy if exists "catalog_read" on bordas;
create policy "catalog_read" on bordas for select using (ativo = true);
drop policy if exists "catalog_read" on bairros;
create policy "catalog_read" on bairros for select using (ativo = true);
drop policy if exists "catalog_read" on sabores;
create policy "catalog_read" on sabores for select using (ativo = true);
