// Supabase Edge Function — WhatsApp Business webhook (terreno pré-configurado, sem número ainda)
// apps/api/supabase/functions/whatsapp-webhook/index.ts:1
// Fluxo sem IA: menu guiado + carrinho na conversa + Delivery na Central
// 1) GET verifica webhook (hub.verify_token)
// 2) POST recebe mensagem → state machine por tel → responde via Graph API
// 3) PIX antecipado só Pix + comprovante foto manual

import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const VERIFY_TOKEN = Deno.env.get("WHATSAPP_VERIFY_TOKEN") ?? "pizzapdv_verify";
const WHATSAPP_TOKEN = Deno.env.get("WHATSAPP_TOKEN") ?? ""; // vazio até comprar número
const PHONE_ID = Deno.env.get("WHATSAPP_PHONE_ID") ?? "";
const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

const BUSINESS_HOURS_FALLBACK = { open: "18:00", close: "23:00" };

async function getBusinessHours() {
  // Tenta buscar do perfil Business via Graph API; se sem token, usa fallback
  if (!WHATSAPP_TOKEN || !PHONE_ID) return BUSINESS_HOURS_FALLBACK;
  try {
    const res = await fetch(`https://graph.facebook.com/v20.0/${PHONE_ID}?fields=business_profile`, {
      headers: { Authorization: `Bearer ${WHATSAPP_TOKEN}` },
    });
    const data = await res.json();
    // business_profile.hours parsing — se não houver, usa fallback
    return data?.business_profile?.hours ?? BUSINESS_HOURS_FALLBACK;
  } catch { return BUSINESS_HOURS_FALLBACK; }
}

function isWithinHours(hours: any): boolean {
  const now = new Date();
  const hh = now.getHours() * 60 + now.getMinutes();
  const [oh, om] = (hours.open ?? "18:00").split(":").map(Number);
  const [ch, cm] = (hours.close ?? "23:00").split(":").map(Number);
  const open = oh * 60 + om;
  const close = ch * 60 + cm;
  return hh >= open && hh <= close;
}

async function sendWhatsApp(to: string, text: string, buttons?: { id: string; title: string }[]) {
  if (!WHATSAPP_TOKEN || !PHONE_ID) {
    console.log(`[MOCK WhatsApp -> ${to}] ${text}`, buttons);
    return { mock: true };
  }
  const body: any = {
    messaging_product: "whatsapp",
    to,
    type: buttons ? "interactive" : "text",
  };
  if (buttons) {
    body.interactive = {
      type: "button",
      body: { text },
      action: { buttons: buttons.map((b) => ({ type: "reply", reply: b })) },
    };
  } else {
    body.text = { body: text };
  }
  const res = await fetch(`https://graph.facebook.com/v20.0/${PHONE_ID}/messages`, {
    method: "POST",
    headers: { Authorization: `Bearer ${WHATSAPP_TOKEN}`, "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  return res.json();
}

Deno.serve(async (req) => {
  const supabase = createClient(SUPABASE_URL, SERVICE_KEY);
  const url = new URL(req.url);

  // 1) Verificação do webhook (Meta)
  if (req.method === "GET") {
    const mode = url.searchParams.get("hub.mode");
    const token = url.searchParams.get("hub.verify_token");
    const challenge = url.searchParams.get("hub.challenge");
    if (mode === "subscribe" && token === VERIFY_TOKEN) {
      return new Response(challenge, { status: 200 });
    }
    return new Response("Forbidden", { status: 403 });
  }

  // 2) Recebe mensagem
  if (req.method === "POST") {
    const body = await req.json().catch(() => ({}));
    // Log para debug terreno
    console.log("WhatsApp webhook payload", JSON.stringify(body).slice(0, 2000));

    const entry = body.entry?.[0]?.changes?.[0]?.value;
    const msg = entry?.messages?.[0];
    if (!msg) return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });

    const from: string = msg.from;
    const text: string = msg.text?.body ?? msg.button?.payload ?? "";
    const type: string = msg.type ?? "text";

    // Áudio detectado
    if (type === "audio" || type === "voice" || type === "ptt") {
      await sendWhatsApp(from, "Recebi seu áudio 🎤 Deseja continuar por áudio com atendente ou voltar ao menu?", [
        { id: "audio_continuar", title: "Áudio" },
        { id: "menu", title: "Menu" },
      ]);
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    // Horário via perfil Business
    const hours = await getBusinessHours();
    if (!isWithinHours(hours)) {
      await sendWhatsApp(from, `Estamos fechados agora 😊 Horário: ${hours.open} às ${hours.close}. Deixe seu pedido agendado!`);
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    // State machine simples por tel (conversas_whatsapp)
    let { data: conv } = await supabase.from("conversas_whatsapp").select("*").eq("tel", from).single();
    if (!conv) {
      // Primeiro contato → cadastro
      await supabase.from("conversas_whatsapp").insert({ tel: from, nome: null, estado: "cadastro", carrinho_json: "[]", created_at: new Date().toISOString(), updated_at: new Date().toISOString() });
      await sendWhatsApp(from, "Olá, sou o assistente virtual da Pizzaria Donna Helena! Como você se chama? (primeiro pedido, preciso para cadastro e te avisar da fidelidade 10G→1P ❤️)");
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    // Se está em cadastro e mandou nome
    if (conv.estado === "cadastro" && text && !text.startsWith("Pizza")) {
      await supabase.from("conversas_whatsapp").update({ nome: text, estado: "menu", updated_at: new Date().toISOString() }).eq("tel", from);
      // Cria/atualiza cliente_local espelho
      await supabase.from("clientes").upsert({ telefone: from, nome: text, enderecos: [] }, { onConflict: "telefone" });
      await sendWhatsApp(from, `Obrigado, ${text}! Você já está no nosso sistema de fidelidade: a cada 10 pizzas G ganha 1 P grátis 🎉\nO que vai pedir hoje?`, [
        { id: "pizza", title: "Pizza" },
        { id: "esfiha", title: "Esfihas" },
        { id: "bebida", title: "Bebidas" },
      ]);
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    // Menu → categoria
    if (conv.estado === "menu") {
      if (text.toLowerCase().includes("pizza")) {
        await supabase.from("conversas_whatsapp").update({ estado: "pizza_tamanho", updated_at: new Date().toISOString() }).eq("tel", from);
        await sendWhatsApp(from, "Qual tamanho? 🍕", [
          { id: "pizza_p", title: "P (4 fatias)" },
          { id: "pizza_g", title: "G (8 fatias)" },
          { id: "monte_sua", title: "Monte Sua" },
        ]);
      } else {
        await sendWhatsApp(from, "Anotado! Qual sabor?", [
          { id: "calabresa", title: "Calabresa" },
          { id: "mussarela", title: "Mussarela" },
          { id: "frango", title: "Frango" },
        ]);
      }
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    // Pizza tamanho → sabor1
    if (conv.estado === "pizza_tamanho") {
      await supabase.from("conversas_whatsapp").update({ estado: "sabor1", updated_at: new Date().toISOString() }).eq("tel", from);
      await sendWhatsApp(from, "Primeira metade? Escolha o sabor:", [
        { id: "sabor_calabresa", title: "Calabresa" },
        { id: "sabor_mussarela", title: "Mussarela" },
        { id: "sabor_frango", title: "Frango" },
      ]);
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    if (conv.estado === "sabor1") {
      await supabase.from("conversas_whatsapp").update({ estado: "sabor2", carrinho_json: JSON.stringify({ sabor1: text }), updated_at: new Date().toISOString() }).eq("tel", from);
      await sendWhatsApp(from, "Segunda metade? (se igual, pizza inteira)", [
        { id: "sabor_calabresa", title: "Calabresa" },
        { id: "sabor_mussarela", title: "Mussarela" },
        { id: "sabor_frango", title: "Frango" },
      ]);
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    if (conv.estado === "sabor2") {
      await supabase.from("conversas_whatsapp").update({ estado: "borda", updated_at: new Date().toISOString() }).eq("tel", from);
      await sendWhatsApp(from, "Borda? Escolha o tipo:", [
        { id: "borda_sem", title: "Sem borda" },
        { id: "borda_comum", title: "Borda Comum" },
        { id: "borda_vulcao", title: "Borda Vulcão" },
      ]);
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    if (conv.estado === "borda") {
      await supabase.from("conversas_whatsapp").update({ estado: "mais_itens", updated_at: new Date().toISOString() }).eq("tel", from);
      await sendWhatsApp(from, "Deseja mais alguma coisa?", [
        { id: "bebida", title: "Bebidas" },
        { id: "mais_pedido", title: "Mais um pedido" },
        { id: "fechar", title: "Fechar comanda" },
      ]);
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    if (conv.estado === "mais_itens" && text.includes("Fechar")) {
      await supabase.from("conversas_whatsapp").update({ estado: "bairro", updated_at: new Date().toISOString() }).eq("tel", from);
      const { data: bairros } = await supabase.from("bairros").select("nome, taxa_fixa").eq("ativo", true).limit(10);
      const lista = (bairros ?? []).map((b: any) => `${b.nome} — R$ ${Number(b.taxa_fixa).toFixed(2)}`).join("\n") || "Centro — R$ 5,00";
      await sendWhatsApp(from, `Qual região de entrega?\n${lista}\nResponda com o nome do bairro.`);
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    if (conv.estado === "bairro") {
      await supabase.from("conversas_whatsapp").update({ estado: "endereco", bairro_id: text, updated_at: new Date().toISOString() }).eq("tel", from);
      await sendWhatsApp(from, "Endereço completo (rua, número, ponto de referência):");
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    if (conv.estado === "endereco") {
      await supabase.from("conversas_whatsapp").update({ estado: "pagamento", endereco: text, updated_at: new Date().toISOString() }).eq("tel", from);
      await sendWhatsApp(from, "Forma de pagamento:", [
        { id: "pagar_entrega_dinheiro", title: "Na entrega Dinheiro" },
        { id: "pagar_entrega_cartao", title: "Na entrega Cartão" },
        { id: "pagar_pix", title: "PIX antecipado" },
      ]);
      // Envia taxa já: buscar taxa do bairro
      const { data: bairro } = await supabase.from("bairros").select("taxa_fixa").ilike("nome", `%${conv.bairro_id ?? ""}%`).limit(1).single();
      const taxa = bairro?.taxa_fixa ?? 5;
      await sendWhatsApp(from, `Taxa de entrega para ${conv.bairro_id}: R$ ${Number(taxa).toFixed(2)} será adicionada.`);
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    if (conv.estado === "pagamento") {
      if (text.includes("pix") || text.includes("PIX")) {
        const { data: cfg } = await supabase.from("config").select("valor").eq("chave", "pix_chave").single();
        const pix = cfg?.valor ?? "PIX não configurado — configure em Configurações F10";
        await supabase.from("conversas_whatsapp").update({ estado: "aguardando_comprovante", updated_at: new Date().toISOString() }).eq("tel", from);
        // Cria pedido delivery aguardando comprovante
        const { data: pedido } = await supabase.from("pedidos").insert({
          origem: "whatsapp", status: "recebido", cliente_nome: conv.nome ?? from, cliente_telefone: from,
          cliente_endereco: conv.endereco, bairro_id: conv.bairro_id, taxa_entrega: 5, subtotal: 59.90, total: 64.90,
          forma_pagamento: "pix", status_pagamento: "pendente", observacao: "Aguardando comprovante PIX",
        }).select().single();
        await supabase.from("conversas_whatsapp").update({ carrinho_json: JSON.stringify({ pedido_id: pedido?.id }) }).eq("tel", from);
        await sendWhatsApp(from, `PIX antecipado (só Pix):\nChave: ${pix}\nValor: R$ 64,90\nEnvie o comprovante (foto) aqui. Assim que confirmar, emitimos as 3 comandas 🧾`);
      } else {
        // Pagar na entrega
        const isDinheiro = text.includes("Dinheiro");
        await sendWhatsApp(from, isDinheiro ? "Troco para quanto? (ex: 100)" : "Pagamento na entrega com cartão anotado!");
        await supabase.from("conversas_whatsapp").update({ estado: "finalizado", updated_at: new Date().toISOString() }).eq("tel", from);
        await supabase.from("pedidos").insert({
          origem: "whatsapp", status: "recebido", cliente_nome: conv.nome ?? from, cliente_telefone: from,
          cliente_endereco: conv.endereco, bairro_id: conv.bairro_id, taxa_entrega: 5, subtotal: 59.90, total: 64.90,
          forma_pagamento: isDinheiro ? "dinheiro" : "cartao_infinitepay", status_pagamento: "pendente",
        });
        await sendWhatsApp(from, "Pedido fechado! Você será notificado quando sair para entrega 🛵");
      }
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    // Se enviou imagem (comprovante) enquanto aguardando
    if (msg.type === "image" && conv.estado === "aguardando_comprovante") {
      await supabase.from("whatsapp_mensagens").insert({ tel: from, direcao: "in", tipo: "image", body: "comprovante", payload_json: JSON.stringify(msg), created_at: new Date().toISOString() });
      await sendWhatsApp(from, "Comprovante recebido! O dono vai confirmar e já emitimos as comandas. Você será avisado quando sair para entrega.");
      return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
    }

    await sendWhatsApp(from, "Desculpe, não entendi. Digite *menu* para voltar.");
    return new Response(JSON.stringify({ ok: true }), { headers: { "Content-Type": "application/json" } });
  }

  return new Response("ok", { status: 200 });
});
