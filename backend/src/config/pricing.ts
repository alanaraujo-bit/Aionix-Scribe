// Preço oficial da Gemini API (tier pago), confirmado em https://ai.google.dev/gemini-api/docs/pricing
// em 2026-08-19 — nunca inventado (mesma regra do tiers.ts para preços do Stripe).
//
// ATENÇÃO — o próprio Google já avisa um reajuste programado: os valores abaixo valem "through
// December 31, 2026"; a partir de 1º de janeiro de 2027 dobram (USD 1.50 / USD 7.50 por 1M
// tokens). Como não há automação de calendário aqui, alguém precisa lembrar de atualizar
// GEMINI_PRICE_INPUT_PER_1M_USD / GEMINI_PRICE_OUTPUT_PER_1M_USD no Railway (ou os defaults
// abaixo) quando essa data chegar — senão o painel de custo passa a subestimar o gasto real pela
// metade sem nenhum erro aparecendo em lugar nenhum.
//
// Overridável por env var pelo mesmo motivo do ESSENCIAL_MONTHLY_QUOTA_SECONDS (tiers.ts): permite
// corrigir o número em produção sem precisar de deploy no dia em que o preço mudar de verdade.
export const GEMINI_PRICE_INPUT_PER_1M_USD = process.env.GEMINI_PRICE_INPUT_PER_1M_USD
  ? Number(process.env.GEMINI_PRICE_INPUT_PER_1M_USD)
  : 0.75;

export const GEMINI_PRICE_OUTPUT_PER_1M_USD = process.env.GEMINI_PRICE_OUTPUT_PER_1M_USD
  ? Number(process.env.GEMINI_PRICE_OUTPUT_PER_1M_USD)
  : 3.75;

// Teto mensal opcional que o proprietário define para acompanhar gasto x orçamento no painel.
// Sem isso configurado, o painel mostra só o gasto (sem "saldo restante") — mais honesto do que
// inventar um teto que ninguém decidiu.
export const GEMINI_MONTHLY_BUDGET_USD = process.env.GEMINI_MONTHLY_BUDGET_USD
  ? Number(process.env.GEMINI_MONTHLY_BUDGET_USD)
  : null;

export function estimateCostUsd(promptTokens: number, candidateTokens: number): number {
  const inputCost = (promptTokens / 1_000_000) * GEMINI_PRICE_INPUT_PER_1M_USD;
  const outputCost = (candidateTokens / 1_000_000) * GEMINI_PRICE_OUTPUT_PER_1M_USD;
  return inputCost + outputCost;
}
