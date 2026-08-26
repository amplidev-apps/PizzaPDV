-- PizzaPDV — Schema Supabase (Postgres + Realtime)
-- apps/api/supabase/schema.sql:1
-- Execute no SQL Editor do Supabase

-- Extensões
create extension if not exists "uuid-ossp";

-- Enums
do $$ begin
  create type categoria_produto as enum ('pizza','esfiha','calzone','salgado','pastel','bebida','borda');
exception when duplicate_object then null; end $$;

do $$ begin
  create type origem_pedido as enum ('site','balcao','mesa','telefone');
exception when duplicate_object then null; end $$;

do $$ begin
  create type status_pedido as enum ('recebido','preparo','pronto','entregue','cancelado');
exception when duplicate_object then null; end $$;

do $$ begin
  create type forma_pagamento as enum ('dinheiro','pix','pix_pushinpay','cartao_infinitepay');
exception when duplicate_object then null; end $$;

do $$ begin
  create type status_pagamento as enum ('pendente','pago','falhou');
exception when duplicate_object then null; end $$;

-- Bairros (taxa fixa)
create table if not exists bairros (
  id uuid primary key default uuid_generate_v4(),
  nome text not null unique,
  taxa_fixa numeric(10,2) not null check (taxa_fixa >= 0),
  ativo boolean default true,
  created_at timestamptz default now()
);

-- Mesas 1..20
create table if not exists mesas (
  numero int primary key check (numero between 1 and 20),
  status text not null default 'livre' check (status in ('livre','ocupada','conta')),
  comanda_aberta uuid,
  updated_at timestamptz default now()
);

-- Bordas
create table if not exists bordas (
  id text primary key, -- ex: catupiry, camarao, vulcao
  nome text not null,
  preco_adicional numeric(10,2) not null check (preco_adicional >= 0),
  custo numeric(10,2) default 0,
  tipo text not null check (tipo in ('comum','premium')),
  ativo boolean default true
);

-- Produtos
create table if not exists produtos (
  id uuid primary key default uuid_generate_v4(),
  nome text not null,
  categoria categoria_produto not null,
  descricao text,
  ativo boolean default true,
  max_sabores int default 1 check (max_sabores between 1 and 2),
  created_at timestamptz default now()
);

-- Variações (P/G/unico)
create table if not exists variacoes (
  id uuid primary key default uuid_generate_v4(),
  produto_id uuid not null references produtos(id) on delete cascade,
  tamanho text not null check (tamanho in ('P','G','unico')),
  fatias int,
  preco numeric(10,2) not null check (preco >= 0),
  custo numeric(10,2) default 0,
  ativo boolean default true,
  unique(produto_id, tamanho)
);

-- Sabores (para pizza meia-a-meia e precificação)
create table if not exists sabores (
  id uuid primary key default uuid_generate_v4(),
  nome text not null unique,
  categoria text not null default 'salgada' check (categoria in ('salgada','doce','mista')),
  custo numeric(10,2) default 0, -- ficha técnica simplificada v1
  ativo boolean default true
);

create table if not exists produto_sabores (
  produto_id uuid references produtos(id) on delete cascade,
  sabor_id uuid references sabores(id) on delete cascade,
  primary key (produto_id, sabor_id)
);

-- Clientes (telefone como chave natural, mas id uuid)
create table if not exists clientes (
  id uuid primary key default uuid_generate_v4(),
  nome text not null,
  telefone text not null unique,
  enderecos jsonb default '[]'::jsonb,
  bairro_id uuid references bairros(id),
  total_pizzas_g int default 0,
  pizzas_g_para_fidelidade int default 0 check (pizzas_g_para_fidelidade between 0 and 9),
  cupons_pendentes int default 0,
  created_at timestamptz default now()
);

-- Caixas
create table if not exists caixas (
  id uuid primary key default uuid_generate_v4(),
  aberto_em timestamptz not null default now(),
  fechado_em timestamptz,
  saldo_inicial numeric(10,2) not null default 0,
  sangrias jsonb default '[]'::jsonb,
  suprimentos jsonb default '[]'::jsonb,
  total_vendas numeric(10,2) default 0,
  por_forma jsonb default '{}'::jsonb
);

-- Entregadores
create table if not exists entregadores (
  id uuid primary key default uuid_generate_v4(),
  nome text not null,
  telefone text,
  ativo boolean default true
);

-- Pedidos
create table if not exists pedidos (
  id uuid primary key default uuid_generate_v4(),
  origem origem_pedido not null,
  status status_pedido not null default 'recebido',
  cliente_id uuid references clientes(id),
  cliente_nome text not null,
  cliente_telefone text not null,
  cliente_endereco text,
  bairro_id uuid references bairros(id),
  taxa_entrega numeric(10,2) not null default 0,
  mesa_numero int references mesas(numero),
  comanda_id uuid,
  subtotal numeric(10,2) not null,
  total numeric(10,2) not null,
  forma_pagamento forma_pagamento not null,
  status_pagamento status_pagamento not null default 'pendente',
  observacao text,
  entregador_id uuid references entregadores(id),
  pix_txid text,
  created_at timestamptz default now(),
  updated_at timestamptz default now()
);

-- Itens do pedido
create table if not exists itens_pedido (
  id uuid primary key default uuid_generate_v4(),
  pedido_id uuid not null references pedidos(id) on delete cascade,
  produto_id uuid not null references produtos(id),
  variacao_id uuid not null references variacoes(id),
  sabor1_id uuid references sabores(id),
  sabor2_id uuid references sabores(id),
  borda_id text references bordas(id),
  quantidade int not null check (quantidade > 0),
  observacao text,
  preco_unit numeric(10,2) not null,
  check (sabor1_id is distinct from sabor2_id)
);

-- Comandas (para mesas)
create table if not exists comandas (
  id uuid primary key default uuid_generate_v4(),
  mesa_numero int not null references mesas(numero),
  status text not null default 'aberta' check (status in ('aberta','fechada','paga')),
  total numeric(10,2) default 0,
  created_at timestamptz default now(),
  closed_at timestamptz
);

-- Índices
create index if not exists idx_pedidos_status on pedidos(status);
create index if not exists idx_pedidos_created on pedidos(created_at desc);
create index if not exists idx_pedidos_mesa on pedidos(mesa_numero);
create index if not exists idx_itens_pedido on itens_pedido(pedido_id);

-- Trigger updated_at
create or replace function set_updated_at() returns trigger as $$
begin new.updated_at = now(); return new; end; $$ language plpgsql;
drop trigger if exists trg_pedidos_updated on pedidos;
create trigger trg_pedidos_updated before update on pedidos for each row execute function set_updated_at();

-- Fidelidade: incrementa contador quando pedido entregue com pizza G
create or replace function fidelidade_on_pedido_entregue() returns trigger as $$
declare
  qtd_g int;
  cli record;
begin
  if new.status = 'entregue' and old.status != 'entregue' and new.cliente_id is not null then
    select count(*) into qtd_g from itens_pedido ip
      join variacoes v on v.id = ip.variacao_id
      join produtos p on p.id = ip.produto_id
      where ip.pedido_id = new.id and p.categoria = 'pizza' and v.tamanho = 'G';
    if qtd_g > 0 then
      select * into cli from clientes where id = new.cliente_id;
      -- usa função de fidelidade
      update clientes set
        total_pizzas_g = total_pizzas_g + qtd_g,
        pizzas_g_para_fidelidade = (pizzas_g_para_fidelidade + qtd_g) % 10,
        cupons_pendentes = cupons_pendentes + floor((pizzas_g_para_fidelidade + qtd_g)/10.0)::int
      where id = new.cliente_id;
    end if;
  end if;
  return new;
end; $$ language plpgsql;

drop trigger if exists trg_fidelidade on pedidos;
create trigger trg_fidelidade after update on pedidos for each row execute function fidelidade_on_pedido_entregue();

-- Realtime
alter publication supabase_realtime add table pedidos;
alter publication supabase_realtime add table itens_pedido;
alter publication supabase_realtime add table mesas;
alter publication supabase_realtime add table comandas;
