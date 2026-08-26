// apps/site/src/lib/pix.ts:1
// Cliente chama /api/pix/create que proxia para PushinPay via Edge Function

export async function criarPixPedido(pedidoId: string, totalCentavos: number) {
  const res = await fetch("/api/pix/create", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ pedidoId, valorCentavos: totalCentavos }),
  });
  if (!res.ok) throw new Error("Erro ao gerar Pix");
  return (await res.json()) as { qr_code: string; qr_code_base64: string; id: string };
}
