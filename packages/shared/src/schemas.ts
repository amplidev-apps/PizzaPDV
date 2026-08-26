import { z } from "zod";
import {
  CATEGORIAS,
  ORIGENS_PEDIDO,
  STATUS_PEDIDO,
  FORMAS_PAGAMENTO,
} from "./constants.js";

// Produto / Variação
export const variacaoSchema = z.object({
  id: z.string().uuid().or(z.string().startsWith("tmp_")),
  produtoId: z.string(),
  tamanho: z.enum(["P", "G", "unico"]),
  preco: z.number().nonnegative(),
  custo: z.number().nonnegative().optional(),
  fatias: z.number().int().optional(),
  ativo: z.boolean().default(true),
});

export const produtoSchema = z.object({
  id: z.string(),
  nome: z.string().min(2),
  categoria: z.enum(CATEGORIAS),
  descricao: z.string().optional(),
  ativo: z.boolean().default(true),
  variacoes: z.array(variacaoSchema).min(1),
  maxSabores: z.number().int().min(1).max(2).optional(), // só pizza usa 2
});

// Borda
export const bordaSchema = z.object({
  id: z.string(),
  nome: z.string(),
  precoAdicional: z.number().nonnegative(),
  custo: z.number().nonnegative().optional(),
  tipo: z.enum(["comum", "premium"]),
});

// Bairro / Taxa
export const bairroSchema = z.object({
  id: z.string(),
  nome: z.string().min(2),
  taxaFixa: z.number().nonnegative(),
  ativo: z.boolean().default(true),
});

// Item do pedido — suporta meia-a-meia + borda
export const itemPedidoSchema = z.object({
  produtoId: z.string(),
  variacaoId: z.string(),
  sabor1Id: z.string().optional(),
  sabor2Id: z.string().optional(),
  bordaId: z.string().nullable().optional(),
  quantidade: z.number().int().positive(),
  observacao: z.string().max(200).optional(),
  precoUnit: z.number().nonnegative(), // calculado no backend/frontend
});

// Pedido
export const pedidoSchema = z.object({
  id: z.string().uuid().or(z.string().startsWith("tmp_")),
  origem: z.enum(ORIGENS_PEDIDO),
  status: z.enum(STATUS_PEDIDO).default("recebido"),
  cliente: z.object({
    nome: z.string().min(2),
    telefone: z.string().min(10),
    endereco: z.string().optional(),
    bairroId: z.string().optional(),
  }),
  mesaNumero: z.number().int().min(1).max(20).nullable().optional(),
  comandaId: z.string().nullable().optional(),
  bairroId: z.string().nullable().optional(),
  taxaEntrega: z.number().nonnegative().default(0),
  itens: z.array(itemPedidoSchema).min(1),
  subtotal: z.number().nonnegative(),
  total: z.number().nonnegative(),
  formaPagamento: z.enum(FORMAS_PAGAMENTO),
  statusPagamento: z.enum(["pendente", "pago", "falhou"]).default("pendente"),
  observacao: z.string().optional(),
  criadoEm: z.string().datetime().optional(),
});

export const checkoutSchema = pedidoSchema.omit({
  id: true,
  status: true,
  subtotal: true,
  total: true,
  statusPagamento: true,
  criadoEm: true,
});

// Caixa
export const caixaSchema = z.object({
  id: z.string(),
  abertoEm: z.string().datetime(),
  fechadoEm: z.string().datetime().nullable(),
  saldoInicial: z.number().nonnegative(),
  sangrias: z.array(z.object({ valor: z.number(), motivo: z.string() })),
  suprimentos: z.array(z.object({ valor: z.number(), motivo: z.string() })),
  totalVendas: z.number().nonnegative(),
  porForma: z.record(z.number()),
});

// Validação meia-a-meia
export function validarItemPizza(item: z.infer<typeof itemPedidoSchema>, categoria: string) {
  if (categoria !== "pizza") {
    if (item.sabor2Id) return { ok: false, erro: "Só pizza permite 2 sabores" };
    return { ok: true };
  }
  // pizza P/G permite 0-2 sabores (0 = ex: pizza doce única)
  if (item.sabor1Id && item.sabor2Id && item.sabor1Id === item.sabor2Id)
    return { ok: false, erro: "Sabores iguais: use apenas sabor1" };
  return { ok: true };
}
