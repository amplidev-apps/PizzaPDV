// Módulo de precificação — ficha técnica + margem
// Fórmula: precoSugerido = (custoMassa + custoMedioSabores + custoBorda + embalagem) / (1 - margem)
// packages/shared/src/pricing.ts:1

export type Insumo = { nome: string; custoPorKg: number; gramasPorPizza: number };
export type SaborCusto = { saborId: string; nome: string; insumos: Insumo[]; custoCalculado: number };

export function custoSabor(insumos: Insumo[]): number {
  return insumos.reduce((acc, i) => acc + (i.custoPorKg / 1000) * i.gramasPorPizza, 0);
}

export function custoPizzaMeia(
  custoSabor1: number,
  custoSabor2: number | null,
  custoMassa: number,
  custoBorda: number,
  embalagem: number
): number {
  const mediaSabores = custoSabor2 === null ? custoSabor1 : (custoSabor1 + custoSabor2) / 2;
  return mediaSabores + custoMassa + custoBorda + embalagem;
}

export function precoSugerido(custoTotal: number, margemDesejada: number): number {
  // margem 0.6 = 60%
  if (margemDesejada >= 1) throw new Error("Margem deve ser < 1");
  return custoTotal / (1 - margemDesejada);
}

export function margemReal(custoTotal: number, precoVenda: number): number {
  if (precoVenda === 0) return 0;
  return (precoVenda - custoTotal) / precoVenda;
}

export function precoMeiaMaiorValor(precoSabor1: number, precoSabor2: number | null): number {
  if (precoSabor2 === null) return precoSabor1;
  return Math.max(precoSabor1, precoSabor2);
}

// Exemplo: simula impacto de aumento de insumo
export function simularAumento(custoAtual: number, percentual: number, margem: number) {
  const novoCusto = custoAtual * (1 + percentual);
  return {
    novoCusto,
    novoPreco: precoSugerido(novoCusto, margem),
    impacto: novoCusto - custoAtual,
  };
}
