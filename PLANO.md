# PLANO PizzaPDV — Execução

## Decisões travadas com cliente
- 1 unidade, sítio interior CE, método profissional desde já
- Impressora JP-58H 58mm genérica
- Windows + offline obrigatório com sync
- Pizzas P 4 fatias / G 8 fatias + esfihas/calzones/salgados/pastéis
- Bordas cobradas: catupiry, cream cheese, chocolate, comum, camarão, vulcão
- Pagamento: dinheiro, Pix, cartão InfinitePay + Pix QR automático PushinPay
- Delivery taxa fixa por bairro → comissão entregador = soma taxas
- Mesas 1-20, localhost, comanda cozinha com mesa + conta final
- Fidelidade: 10 G → 1 P grátis
- Fiscal: só controle interno
- Hospedagem MVP: Vercel + Supabase free (não ByetHost)

## Arquitetura (offline-first)
Site Next.js → Supabase Realtime → Central Electron SQLite+outbox → JP-58H

## Fase 0 — Fundação ✅ (entregue)
- Monorepo, shared, schema Supabase, seeds, templates impressão, sync outbox, scaffolds Site/Central

## Fase 1 — MVP (próxima)
- Site conectado Supabase + checkout real + Pix
- Central Kanban realtime + mesas + caixa + impressão real
- Teste offline + comissão entregador

## Fase 2 — Operação
- KDS, WhatsApp, fidelidade trigger, relatórios

## Fase 3 — Gestão
- Precificação ficha técnica, dashboard

Riscos: driver JP-58H, IDs tmp, PushinPay taxas.
