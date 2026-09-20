# PizzaPDV — PDV de Pizzaria (Central WinForms + Site + WhatsApp)

Sistema ponto-de-venda completo para pizzaria de unidade única no interior do Ceará.
Roda em hardware modesto (**Core Duo DDR2 4 GB + SynX 10**), funciona **offline-first**
(SQLite local + fila outbox → Supabase quando volta a rede) e imprime em
**JP-58H 58 mm (ESC/POS via WinSpool)**.

> Canal principal de vendas: **WhatsApp Business**. O site é secundário.
> Atalhos de teclado estilo "PDV clássico": abre em <1 s, sem animações, sem Chrome.

---

## 1. Visão geral

| Frente | Onde | Status |
|---|---|---|
| **Central Batata** (PDV desktop) | `src/PizzaPDV.Central` — C# WinForms .NET 8 | ✅ Operacional |
| Biblioteca de regras | `src/PizzaPDV.Core` — precificação, meia-a-meia, fidelidade | ✅ Operacional |
| Banco local + seed | `src/PizzaPDV.Data` — SQLite WAL + Dapper | ✅ Operacional |
| Impressão 58 mm | `src/PizzaPDV.Printer` — RawPrinter + Templates 32 col | ✅ Operacional |
| Sincronização | `src/PizzaPDV.Sync` — outbox → Supabase | ✅ Terreno pronto |
| Webhook WhatsApp | `apps/api/supabase/functions/whatsapp-webhook` | 🟡 Terreno pré-config (aguarda número Business) |
| Site cardápio | `apps/site` (Next.js, monorepo pnpm) | 🟡 Secundário / pausado |

### Hardware alvo

- CPU Core Duo, 4 GB DDR2, Windows 10 leve (SynX 10)
- Impressora térmica genérica **JP-58H 58 mm** (driver WinSpool, nome `JP-58H` ou `POS-58`)
- Sem internet obrigatória: tudo salva local e sincroniza depois

---

## 2. Regras de negócio (travadas com a dona)

- **Tamanhos**: Pizza P = 4 fatias, Pizza G = 8 fatias. Também: esfihas, calzones, salgados, pastéis.
- **Meia-a-meia obrigatória**: toda pizza de 2 sabores usa `(p1 + p2) / 2`
  (`PizzaPDV.Core.Pricing.PrecoMeiaMedia`). Se os 2 sabores forem iguais, vira inteira.
- **Bordas cobradas**: Catupiry, Cream Cheese, Chocolate, Comum, Camarão, Vulcão
  (de R$ 5,00 a R$ 18,00).
- **Monte Sua (build-your-own)**: base invisível P R$ 9,90 / G R$ 14,90 + porções de
  120 g (máx. 4 por pizza). Cada porção = custo/porção com margem de 60 %.
- **Precificação justa**: recheios manipulados têm ficha técnica
  (insumo × quantidade × unidade × corte). Fator de corte: cubos 1,10×, fatiado 1,05× etc.
  (`Pricing.FatorCorte`). Custo/g → custo/porção → preço sugerido com margem padrão 60 %.
- **Bases P/G invisíveis**: orégano + azeitona + embalagem + molho compõem o custo fixo,
  só a massa aparece para o cliente (tabela `bases_tamanho`).
- **Delivery**: taxa **fixa por bairro** (tabela `bairros`). Comissão do entregador do dia =
  soma das taxas de delivery/WhatsApp.
- **Mesas 1–20**: branca = livre, amarela = ocupada. Comanda da cozinha sai com
  **MESA em destaque**. Recursos: adiantamento (pagar parte), divisão igual por pessoa,
  transferir mesa, pedir conta, liberar.
- **Fidelidade**: a cada **10 pizzas G → 1 pizza P grátis** (cupom). Resgate no F6.
- **Pagamento**: Dinheiro, Pix, Pix QR (PushinPay), Cartão (InfinitePay).
- **Validade/etiquetas**: produtos manipulados têm manipulação → validade → responsável →
  etiqueta JP-58H 32 col (reimpressão, baixa/consumo, descarte).
- **Fiscal**: só controle interno, sem NFC-e neste MVP.

---

## 3. A Central (telas e atalhos)

| Atalho | Tela | O que faz |
|---|---|---|
| F1 | **Pedidos** | Kanban Recebido → Preparo → Pronto → Entregue. Botões: Aceitar, Marcar Pronto, Entregar, Cozinha/Cliente/Delivery. Botão direito: ver detalhe, mudar status, reimprimir, atribuir entregador, cancelar. F12 imprime. |
| F2 | **Balcão** | Venda rápida sem mesa. Produto + 2 sabores (meia) + borda + obs + qtd → carrinho. Cliente/tel opcionais, bairro/taxa, pagamento. Finaliza, grava `pedidos_local` + itens, enfileira sync e imprime ticket cliente. |
| F3 | **Mesas 1–20** | Grade 5×4. Clique abre comanda (pizza P/G meia-a-meia, esfihas, bordas → envia p/ cozinha). Botão direito: ver/editar, item rápido, reimprimir cozinha, pedir conta, **adiantamento**, **dividir igual**, transferir, liberar. |
| F4 | **Cardápio** | Abas Produtos, Bordas, Bairros, Recheios/Sabores. Novo produto, editar preço, desativar. Recheio manipulado: composição de insumos (qtd/un/corte), cálculo peso total → custo total → custo/porção → preço sugerido 60 %. Botão "Sincronizar com Site agora". |
| F5 | **Caixa** | KPIs: saldo inicial, vendas hoje, comissão entregador. Sangria, suprimento, fechar caixa (abre novo com R$ 100), imprimir fechamento (por forma de pagamento). |
| F6 | **Clientes** | Busca por tel/nome. Progresso `X / 10`, cupons. Novo cliente, resgatar pizza P. Semeia João (7/10) e Maria (1 cupom) se vazio. |
| F8 | **Delivery • WhatsApp** | Filtro tel/nome, status `aguardando_comprovante` destacado. Ver comprovante (mock), **confirmar pagamento → emite 3 comandas** (cozinha + cliente + delivery), saiu p/ entrega (mensagem Zap), áudio → atendente. |
| F7 | **Validade** | Grade manipulação → validade → resp → status (válido/vencendo/vencido com cor) → qtd. Nova manipulação (imprime etiqueta), reimprimir, baixar (consumido), descartar. |
| F10 | **Config** | Abas Insumos, Massas matriz, Bases P/G, Geral (margem %, taxa mesa, impressora WinSpool, `SUPABASE_URL`). Novo insumo (preço emb. → custo/un), backup path. |
| F12 | **Teste impressão** | Imprime ticket cliente de teste em qualquer tela. |

---

## 4. Arquitetura

```text
┌─────────────┐     outbox (10 s)     ┌──────────┐    Realtime   ┌────────┐
│  CENTRAL    │ ───────────────────▶  │ SUPABASE │ ───────────▶  │  SITE  │
│ WinForms    │ ◀───────────────────  │ Postgres │               │ Next   │
│ SQLite WAL  │     pull catálogo     └──────────┘               └────────┘
│  + Dapper   │                              ▲
└──────┬──────┘                              │ webhook terreno
       │ WinSpool ESC/POS            ┌───────┴────────┐
       ▼                             │ WhatsApp Bus.  │
   ┌────────┐                        │ (VERIFY_TOKEN, │
   │ JP-58H │                        │  state machine)│
   │ 58 mm  │                        └────────────────┘
   └────────┘
```

- **Offline-first**: sem rede, Central grava `pedidos_local` (+ `*_local`) e `outbox`
  com id `tmp_xxx`. Com rede, `SyncService.SyncOutboxAsync()` envia a fila a cada 10 s
  (ou no botão Sincronizar). `PullCatalogAsync()` puxa cardápio da nuvem se preciso.
- **Central é dona do cardápio**: edita local, enfileira, Site atualiza via Realtime.
- **Impressão**: tenta `JP-58H` → `POS-58` → mock em `%TEMP%\pizzapdv_mock_*.txt`
  (o conteúdo ESC/POS vai junto na mensagem de aviso).

### Projetos .NET (`PizzaPDV.sln`)

| Projeto | Target | Papel |
|---|---|---|
| `src/PizzaPDV.Central` | `net8.0-windows` (WinExe, WinForms) | UI: `Program.cs`, `MainForm.cs` (~1500 linhas, 9 abas) |
| `src/PizzaPDV.Core` | `net8.0` | `Pricing.cs`, `Fidelidade.cs`, `Constants.cs` (sem dependências) |
| `src/PizzaPDV.Data` | `net8.0` | `AppDb.cs` (SQLite + Dapper, 23 tabelas, seed) |
| `src/PizzaPDV.Printer` | `net8.0` | `RawPrinter.cs` (WinSpool), `Templates.cs` (comanda cozinha, ticket cliente/delivery, etiqueta validade) |
| `src/PizzaPDV.Sync` | `net8.0` | `SyncService.cs` (outbox, `SUPABASE_URL` + `SUPABASE_SERVICE_ROLE_KEY`) |

Pacotes: `Dapper 2.1.35`, `Microsoft.Data.Sqlite 8.0.11`.

### Banco local (SQLite, `pizzapdv.db` ao lado do .exe, modo WAL)

Catálogo: `produtos`, `variacoes`, `sabores`, `sabor_insumos`, `bordas`, `bairros`,
`insumos`, `receitas_massa`, `receita_itens`, `bases_tamanho`, `recheios_porcao`.
Operação: `mesas`, `pedidos_local`, `itens_pedido_local`, `comandas`,
`pagamentos_comanda`, `caixa_local`, `clientes_local`, `produtos_manipulados`,
`conversas_whatsapp`, `whatsapp_mensagens`, `outbox`, `config`.

Seed inicial: produtos, bordas, 5 bairros, **20 mesas**, bases P/G, massas, insumos,
2 clientes de exemplo.

---

## 5. Pré-requisitos

- **Windows 10/11** (desenvolvimento) ou SynX 10 (produção)
- **.NET 8 SDK** (`dotnet --version` → 8.x) — https://dotnet.microsoft.com/download
- Driver da **JP-58H** instalado e compartilhada como `JP-58H` (fallback `POS-58`)
- (Opcional, p/ sync) projeto **Supabase** + envs abaixo
- (Opcional, p/ site) **Node 20 + pnpm**

---

## 6. Como rodar

### Central (desktop)

```bash
dotnet build PizzaPDV.sln -c Release
dotnet run --project src/PizzaPDV.Central -c Release
```

Variáveis de ambiente (sem elas, roda 100 % local e mostra "Local • sem Supabase"):

```powershell
$env:SUPABASE_URL="https://xyzcompany.supabase.co"
$env:SUPABASE_SERVICE_ROLE_KEY="eyJ..."
dotnet run --project src/PizzaPDV.Central -c Release
```

### Publicar para a batata

```bash
# Framework-dependent (~2,6 MB — precisa do Runtime .NET 8 na batata, RECOMENDADO)
dotnet publish src/PizzaPDV.Central -c Release -r win-x64 --self-contained false -o release-batata/framework

# Portable single-file (~156 MB — roda sem instalar nada)
dotnet publish src/PizzaPDV.Central -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o release-batata/portable
```

Copie a pasta para a batata e execute. O `pizzapdv.db` é criado ao lado do `.exe`.

### Site (secundário)

```bash
pnpm install
# Supabase: SQL Editor → rode apps/api/supabase/schema.sql + seed.sql + schema_patch_catalog.sql
# habilite Realtime em produtos/variacoes/bordas/bairros/sabores/pedidos
cp apps/site/.env.example apps/site/.env.local  # NEXT_PUBLIC_SUPABASE_URL / ANON
pnpm --filter @pizzapdv/site dev  # http://localhost:3000
```

---

## 7. Impressora JP-58H

1. Instale o driver e compartilhe como `JP-58H` (ou `POS-58`).
2. Na Central, pressione **F12** — deve sair o ticket de teste.
3. Sem impressora: cai em mock `%TEMP%\pizzapdv_mock_*.txt` e o texto ESC/POS aparece
   na mensagem (bom para validar layout 32 col sem hardware).
4. Templates em `src/PizzaPDV.Printer/Templates.cs`: `ComandaCozinha` (MESA em destaque),
   `TicketCliente`, `TicketDelivery`, etiqueta de validade.

---

## 8. WhatsApp Business (terreno pronto, falta o número)

- Tabelas locais: `conversas_whatsapp` (tel, nome, estado) + `whatsapp_mensagens`.
- Função `apps/api/supabase/functions/whatsapp-webhook` com `VERIFY_TOKEN`,
  horário comercial e state machine sem IA.
- F8 filtra por tel/nome, confirma Pix por comprovante → 3 comandas, avisa
  "saiu para entrega", trata áudio transferindo ao atendente.
- Para ativar: provisionar número Business, preencher `WHATSAPP_*` em Config e
  apontar o webhook da Meta para a função.

---

## 9. Solução de problemas

| Sintoma | Causa provável | Ação |
|---|---|---|
| "Local • sem Supabase configurado" | envs vazias | Exporte `SUPABASE_URL` + `SERVICE_ROLE_KEY` e reinicie |
| Nada imprime, abre mock `.txt` | Nome da impressora diferente | Renomeie/compartilhe como `JP-58H` ou `POS-58` |
| Pedidos presos "pendentes" | Sem rede | Religar rede → sync automático em 10 s ou botão Sincronizar |
| Grid vazio na 1ª execução | Banco recém-criado | Seeds + linhas de exemplo são inseridos sozinhos |
| Build falha no `MainForm.cs` | Refactor UI em andamento | `git status` deve estar limpo; branch `master` sempre verde |

---

## 10. Roadmap

- [ ] **UI dark fixo** (`Theme.Bg #121214`, `Card #1E1E20`) + `PageLayout` único
      (48 título + 44 toolbar + 100 % conteúdo) para zerar cortes em 1024×768
      — especificação pronta, entra após este push
- [ ] Ativar número WhatsApp Business e homologar webhook
- [ ] Teste real na batata SynX 10 + JP-58H física
- [ ] KDS cozinha, relatórios gerenciais, backup automático do `.db`

---

## 11. Documentos

- `PLANO.md` — roadmap consolidado e decisões travadas
- `PRECIFICACAO-JUSTA-EXPLICADA.md` — explicação da precificação para a dona
  (linguagem simples, sem jargão)
- `apps/api/supabase/schema.sql` + `seed.sql` + `schema_patch_catalog.sql` — banco nuvem

---

## 12. Licença / uso

Projeto privado da pizzaria (1 unidade). Uso interno — Central no caixa,
site e webhook sob conta da loja.
