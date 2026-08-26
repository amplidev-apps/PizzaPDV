# PizzaPDV — Caixa & Atendimento (Batata Classic)

Sistema profissional para pizzaria local (1 unidade, sítio CE) — 2 núcleos:
- **Site** (`apps/site`): Next.js cardápio com meia-a-meia, bordas cobradas (catupiry/vulcão...), taxa fixa por bairro, Pix PushinPay
- **Central BATATA** (`src/PizzaPDV.Central`): **C# WinForms .NET 8** — 2.5MB, 15MB RAM, roda em Core Duo DDR2 4GB SynX 10, SQLite WAL offline-first + outbox → Supabase → Site, JP-58H 58mm ESC/POS, mesas 1-20, caixa, fidelidade 10G→1P

> PDV clássico: F1 Pedidos | F2 Mesas | F3 Cardápio | F4 Caixa | F5 Clientes | F12 Imprimir — sem animações, sem Chrome, abre <1s na batata.

## Stack
- Monorepo pnpm (Site) + dotnet sln (Central)
- Supabase (Postgres + Realtime) — `apps/api/supabase/schema.sql:1` + `seed.sql:1` + `schema_patch_catalog.sql:1` (Central dona do cardápio)
- Shared: `packages/shared` (Zod) + `src/PizzaPDV.Core` (C# port Pricing/Fidelidade)
- Site: Next.js 15 + Tailwind — consome catálogo via `apps/site/src/lib/catalog.ts:1` Realtime
- Central: C# WinForms .NET 8 + Microsoft.Data.Sqlite + Dapper + RawPrinter WinSpool (`src/PizzaPDV.Printer/RawPrinter.cs:1`, `Templates.cs:1`) + SyncService (`src/PizzaPDV.Sync/SyncService.cs:1`)

## Começar — Site
```bash
pnpm install
# Supabase: crie projeto → SQL Editor → rode schema.sql + seed.sql + schema_patch_catalog.sql → habilite Realtime em produtos/variacoes/bordas/bairros/sabores/pedidos
cp apps/site/.env.example apps/site/.env.local
# NEXT_PUBLIC_SUPABASE_URL / ANON
pnpm --filter @pizzapdv/site dev # http://localhost:3000
```

## Começar — Central Batata (Windows 10 SynX)
```bash
# Pré-requisito: .NET 8 SDK (https://dotnet.microsoft.com/download)
dotnet build PizzaPDV.sln -c Release
dotnet run --project src/PizzaPDV.Central -c Release
# ou publique:
dotnet publish src/PizzaPDV.Central -c Release -r win-x64 --self-contained false # framework-dependent -> release-batata/framework (2.5MB)
dotnet publish src/PizzaPDV.Central -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true # portable single 156MB -> release-batata/portable
```

**Executáveis gerados:**
- `release-batata/framework/PizzaPDV.Central.exe` (151KB + DLLs = 2.5MB) — precisa .NET 8 Runtime no PC (recomendado batata com runtime instalado)
- `release-batata/portable/PizzaPDV-Portable.exe` (156MB) — roda sem instalar nada, copie para a batata e execute

DB local: `pizzapdv.db` ao lado do .exe (WAL). Seed inicial: 4 produtos, 6 bordas, 5 bairros, 20 mesas.

## Fluxo Cardápio (Central → Site)
1. Central F3 Cardápio: CRUD produtos/P/G/unico, sabores, bordas (8-18), bairros/taxa. Salva local + `outbox`.
2. SyncService envia a cada 10s se online (ou ao clicar Sincronizar). Supabase `upsert` com `Prefer: resolution=merge-duplicates`.
3. Site `catalog.ts:1` faz `fetchCatalog()` + `subscribeCatalog()` — atualiza sem reload.

## Impressora JP-58H 58mm
- Instale driver, compartilhe como `JP-58H` (ou `POS-58`). Central tenta `JP-58H` → `POS-58` → mock em `%TEMP%\pizzapdv_mock_*.txt`.
- Teste: F12 ou Pedidos → Cozinha/Cliente. Templates 32cols em `PizzaPDV.Printer/Templates.cs:1` (cozinha com MESA 07 em destaque).

## Offline-first
- Sem rede: Central grava `pedidos_local` + `outbox` (`tmp_xxx`). Mesas/balcão funcionam normal.
- Voltou rede: `SyncService.SyncOutboxAsync()` envia fila; `PullCatalogAsync()` puxa cardápio do cloud se necessário.

## Verificação batata
```bash
#pricing + fidelidade + seed + templates
dotnet run --project TestBatata # (exemplo temporário, removido)
# esperado: Pricing 45.00, fidelidade 0/10 1 cupom, 4 produtos/6 bordas/5 bairros/20 mesas
```

## Próximos passos
- [ ] Instalar na batata SynX 10 e testar JP-58H real
- [ ] Configurar `SUPABASE_URL`/`SERVICE_ROLE_KEY` como variáveis de ambiente no PC (ou `appsettings.json`)
- [ ] Deploy schema_patch_catalog.sql no Supabase produção
- [ ] Teste offline: desligar wifi, lançar mesa, religar
