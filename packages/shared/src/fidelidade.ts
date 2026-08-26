// Fidelidade: a cada 10 pizzas G -> 1 pizza P grátis
// packages/shared/src/fidelidade.ts:1

export const META_FIDELIDADE = 10;
export const PREMIO_TAMANHO = "P" as const;

export type ClienteFidelidade = {
  telefone: string;
  pizzasGGanhas: number; //contador 0..9
  totalPizzasG: number;
  cuponsPendentes: number;
};

export function registrarVendaFidelidade(
  cliente: ClienteFidelidade,
  qtdPizzasG: number
): ClienteFidelidade & { novosCupons: number } {
  const total = cliente.pizzasGGanhas + qtdPizzasG;
  const novosCupons = Math.floor(total / META_FIDELIDADE);
  return {
    ...cliente,
    pizzasGGanhas: total % META_FIDELIDADE,
    totalPizzasG: cliente.totalPizzasG + qtdPizzasG,
    cuponsPendentes: cliente.cuponsPendentes + novosCupons,
    novosCupons,
  };
}

export function podeResgatar(cliente: ClienteFidelidade): boolean {
  return cliente.cuponsPendentes > 0;
}

export function resgatarCupom(cliente: ClienteFidelidade): ClienteFidelidade {
  if (!podeResgatar(cliente)) throw new Error("Sem cupons");
  return { ...cliente, cuponsPendentes: cliente.cuponsPendentes - 1 };
}
