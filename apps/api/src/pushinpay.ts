// apps/api/src/pushinpay.ts:1
// Integração PushinPay Pix — gera QR Code automático
// Docs: https://pushinpay.com.br/docs
// Fluxo: site -> POST /api/pix/create {pedidoId, valor} -> PushinPay -> retorna QR + txid -> salva em pedidos.pix_txid

export type PushinPayCreateOpts = {
  apiKey: string;
  valorCentavos: number; // ex: 5990 = R$59,90
  webhookUrl: string; // ex: https://seu-supabase.functions.supabase.co/pushinpay-webhook
  pedidoId: string;
};

export async function criarPixPushinPay(opts: PushinPayCreateOpts) {
  // Endpoint real PushinPay: verifique docs atualizados
  const res = await fetch("https://api.pushinpay.com.br/api/pix/cashIn", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${opts.apiKey}`,
      "Content-Type": "application/json",
      Accept: "application/json",
    },
    body: JSON.stringify({
      value: opts.valorCentavos,
      webhook_url: opts.webhookUrl,
      // metadata para reconciliação
      // Alguns provedores usam `external_id` ou `metadata`
      external_id: opts.pedidoId,
    }),
  });
  if (!res.ok) {
    const txt = await res.text();
    throw new Error(`PushinPay erro ${res.status}: ${txt}`);
  }
  // Resposta esperada: { id, qr_code, qr_code_base64, status }
  return (await res.json()) as {
    id: string;
    qr_code: string; // copia e cola
    qr_code_base64: string; // imagem
    status: string;
  };
}

// Webhook handler (para Supabase Edge Function)
// POST { id, status: 'paid', external_id: pedidoId }
export function validarWebhookPushinPay(secret: string, signature: string, body: string): boolean {
  // Validação simplificada — implemente HMAC conforme docs PushinPay
  // ex: crypto.createHmac('sha256', secret).update(body).digest('hex') === signature
  return true; // TODO: implementar verificação real
}
