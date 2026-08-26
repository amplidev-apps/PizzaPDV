// apps/site/src/app/api/pix/create/route.ts:1
import { NextRequest, NextResponse } from "next/server";

// Proxy para PushinPay via Supabase Edge Function ou direto
// Mantém API key no servidor
export async function POST(req: NextRequest) {
  const { pedidoId, valorCentavos } = await req.json();
  const apiKey = process.env.PUSHINPAY_API_KEY;
  if (!apiKey) {
    // Mock para dev sem chave
    return NextResponse.json({
      id: "mock_" + pedidoId,
      qr_code: "00020126580014BR.GOV.BCB.PIX...",
      qr_code_base64: "data:image/png;base64,iVBORw0KGgo...",
      status: "pending",
      mock: true,
    });
  }
  const res = await fetch("https://api.pushinpay.com.br/api/pix/cashIn", {
    method: "POST",
    headers: { Authorization: `Bearer ${apiKey}`, "Content-Type": "application/json" },
    body: JSON.stringify({ value: valorCentavos, webhook_url: process.env.PUSHINPAY_WEBHOOK_URL, external_id: pedidoId }),
  });
  const data = await res.json();
  return NextResponse.json(data, { status: res.status });
}
