-- Seed inicial PizzaPDV — Pizzaria local 1 unidade
-- apps/api/supabase/seed.sql:1

-- Bairros taxa fixa (exemplo Ceará interior — ajuste valores)
insert into bairros (nome, taxa_fixa) values
  ('Centro', 5.00),
  ('Bairro Novo', 7.00),
  ('Sítio Sede', 8.00),
  ('Zona Rural', 10.00),
  ('Bairro Alto', 6.00)
on conflict (nome) do nothing;

-- Mesas 1..20
insert into mesas (numero, status)
select gs, 'livre' from generate_series(1,20) gs
on conflict (numero) do nothing;

-- Bordas cobradas
insert into bordas (id, nome, preco_adicional, custo, tipo) values
  ('catupiry', 'Catupiry', 8.00, 2.50, 'comum'),
  ('cream_cheese', 'Cream Cheese', 8.00, 2.80, 'comum'),
  ('chocolate', 'Chocolate', 9.00, 3.00, 'comum'),
  ('comum', 'Borda Comum', 5.00, 1.20, 'comum'),
  ('camarao', 'Borda Camarão', 15.00, 6.00, 'premium'),
  ('vulcao', 'Borda Vulcão', 18.00, 7.00, 'premium')
on conflict (id) do nothing;

-- Sabores (base)
insert into sabores (nome, categoria, custo) values
  ('Calabresa', 'salgada', 6.00),
  ('Mussarela', 'salgada', 5.50),
  ('Frango c/ Catupiry', 'salgada', 7.00),
  ('Portuguesa', 'salgada', 7.50),
  ('Margherita', 'salgada', 6.20),
  ('Pepperoni', 'salgada', 7.00),
  ('Chocolate c/ Morango', 'doce', 8.00),
  ('Prestígio', 'doce', 8.50)
on conflict (nome) do nothing;

-- Produtos exemplo
-- Pizzas
insert into produtos (nome, categoria, descricao, max_sabores) values
  ('Pizza Grande (8 fatias)', 'pizza', 'Pizza tamanho G', 2),
  ('Pizza Pequena (4 fatias)', 'pizza', 'Pizza tamanho P', 2),
  ('Esfiha Aberta', 'esfiha', 'Esfiha tradicional', 1),
  ('Calzone', 'calzone', 'Calzone recheado', 1),
  ('Pastel', 'pastel', 'Pastel frito', 1),
  ('Refrigerante Lata', 'bebida', '350ml', 1)
on conflict do nothing;

-- Variações — preços base (ajuste conforme precificação)
-- Assumindo produtos inseridos acima na ordem: precisa buscar IDs
do $$
declare
  p_g uuid; p_p uuid; p_esfiha uuid; p_calzone uuid; p_pastel uuid; p_bebida uuid;
begin
  select id into p_g from produtos where nome='Pizza Grande (8 fatias)' limit 1;
  select id into p_p from produtos where nome='Pizza Pequena (4 fatias)' limit 1;
  select id into p_esfiha from produtos where nome='Esfiha Aberta' limit 1;
  select id into p_calzone from produtos where nome='Calzone' limit 1;
  select id into p_pastel from produtos where nome='Pastel' limit 1;
  select id into p_bebida from produtos where nome='Refrigerante Lata' limit 1;

  insert into variacoes (produto_id, tamanho, fatias, preco, custo) values
    (p_g, 'G', 8, 59.90, 18.00),
    (p_p, 'P', 4, 34.90, 11.00)
  on conflict do nothing;

  insert into variacoes (produto_id, tamanho, preco, custo) values
    (p_esfiha, 'unico', 6.00, 1.80),
    (p_calzone, 'unico', 22.00, 7.00),
    (p_pastel, 'unico', 8.00, 2.50),
    (p_bebida, 'unico', 5.00, 2.50)
  on conflict do nothing;
end $$;

-- Caixa inicial demo
insert into caixas (saldo_inicial) values (100.00);

-- Entregador demo
insert into entregadores (nome, telefone) values ('Motoboy 1', '88999990000') on conflict do nothing;
