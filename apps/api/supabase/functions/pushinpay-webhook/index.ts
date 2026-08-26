// Supabase Edge Function — webhook PushinPay -> atualiza pedidos.status_pagamento
// Deploy: supabase functions deploy pushinpay-webhook
// apps/api/supabase/functions/pushinpay-webhook/index.ts:1
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

Deno.serve(async (req) => {
  const supabase = createClient(Deno.env.get("SUPABASE_URL")!, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!);
  const body = await req.json();
  // body: { id, status, external_id, value }
  const pedidoId = body.external_id ?? body.id;
  const status = body.status; // 'paid' | 'pending' ...

  if (status === "paid" || status === "pago") {
    await supabase.from("pedidos").update({ status_pagamento: "pago", pix_txid: body.id }).eq("id", pedidoId);
  }

  return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
});
