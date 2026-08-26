# PizzaPDV — Caixa & Atendimento

Sistema profissional para pizzaria local (1 unidade) — 2 núcleos:
- **Site** (`apps/site`): Next.js cardápio com meia-a-meia, bordas cobradas, taxa fixa por bairro, Pix PushinPay
- **Central** (`apps/central`): Electron Windows offline-first, SQLite + outbox sync, impressão JP-58H 58mm, mesas 1-20, caixa, fidelidade 10G→1P

## Stack
- Monorepo pnpm + TypeScript
- Supabase (Postgres + Realtime) — ver `apps/api/supabase/schema.sql` e `seed.sql`
- Shared: `packages/shared` (Zod schemas, pricing, fidelidade)
- Site: Next.js 15 + Tailwind
- Central: Electron + Vite + better-sqlite3 + node-thermal-printer (JP-58H)

## Começar (Fase 0)

```bash
# 1. Instalar
pnpm install

# 2. Supabase: crie projeto, rode schema.sql + seed.sql no SQL Editor, ative Realtime em pedidos/mesas/comandas
# 3. Configure envs
cp apps/site/.env.example apps/site/.env.local
cp apps/api/.env.example apps/api/.env
cp apps/central/.env.example apps/central/.env  # crie se precisar

# .env.local site
NEXT_PUBLIC_SUPABASE_URL=https://xxx.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=eyJ...

# .env api/central
SUPABASE_URL=...
SUPABASE_SERVICE_ROLE_KEY=...
PUSHINPAY_API_KEY=...           # opcional p/ Pix QR
PUSHINPAY_WEBHOOK_URL=https://xxx.supabase.co/functions/v1/pushinpay-webhook

# 4. Rodar
pnpm --filter @pizzapdv/site dev      # http://localhost:3000
pnpm --filter @pizzapdv/central dev   # http://localhost:5173 (Vite) + Electron
```

## Impressora JP-58H 58mm
- Instale driver Windows, compartilhe como `JP-58H`
- Teste: Central → Pedidos → botão Cozinha/Cliente. Sem impressora, cai em mock e loga no console + salva raw.
- Templates: `apps/central/src/printer/templates.ts:1` (cozinha com MESA, cliente, delivery)

## Offline-first
- Central grava em `pizzapdv.db` (SQLite WAL) + tabela `outbox`
- `apps/central/src/sync/outbox.ts:1` → `startSyncLoop()` sincroniza quando `navigator.onLine`
- IDs temporários `tmp_*` viram UUID real no Supabase

## Fidelidade & Precificação
- `packages/shared/src/fidelidade.ts:1` — 10G → 1P
- `packages/shared/src/pricing.ts:1` — custo ficha técnica + margem

## Próximos passos Fase 1
- [ ] Conectar Site ao Supabase (substituir mocks em `apps/site/src/app/page.tsx:1`)
- [ ] Realtime Site→Central (Supabase channel pedidos)
- [ ] Conectar Central sync real + teste offline (desligar wifi, lançar mesa, religar e ver sync)
- [ ] PushinPay webhook Edge Function deploy
- [ ] Teste de impressão real JP-58H

Veja `PLANO.md` (se existir) para roadmap completo.
