// Fonte de verdade dos planos comerciais do Aionix Scribe.
// Product/Price IDs e valores são identificadores públicos do Stripe, não secrets.
// Valores confirmados via API do Stripe (modo teste) em 2026-08-08 — nunca inventados.

export type Tier = "essencial" | "premium" | "ultra";
export type BillingPeriod = "mensal" | "anual";

export interface PriceDefinition {
  tier: Tier;
  period: BillingPeriod;
  productId: string;
  priceId: string;
  unitAmountCents: number;
  currency: "brl";
  statementDescriptor: string;
}

export const STRIPE_MODE = (process.env.STRIPE_MODE ?? "test") as "test" | "live";

export const PRICES: PriceDefinition[] = [
  {
    tier: "essencial",
    period: "mensal",
    productId: "prod_V259n5o8JJwjtJ",
    priceId: "price_1U20zVRCZcRie5rtsld1kEKr",
    unitAmountCents: 1490,
    currency: "brl",
    statementDescriptor: "AIONIX*ESSENCIAL MES",
  },
  {
    tier: "essencial",
    period: "anual",
    productId: "prod_V25EzWveXDnNv8",
    priceId: "price_1U213dRCZcRie5rtoo5xbP3C",
    unitAmountCents: 14900,
    currency: "brl",
    statementDescriptor: "AIONIX*ESSENCIAL ANO",
  },
  {
    tier: "premium",
    period: "mensal",
    productId: "prod_V25aaIW5V4g2Fg",
    priceId: "price_1U21PPRCZcRie5rtnUW9gw6g",
    unitAmountCents: 2990,
    currency: "brl",
    statementDescriptor: "AIONIX*PREMIUM MES",
  },
  {
    tier: "premium",
    period: "anual",
    productId: "prod_V25bXOiJmAp6L4",
    priceId: "price_1U21QJRCZcRie5rtQ3ohcwfx",
    unitAmountCents: 29900,
    currency: "brl",
    statementDescriptor: "AIONIX*PREMIUM ANO",
  },
  {
    tier: "ultra",
    period: "mensal",
    productId: "prod_V25dPhF44UgGCc",
    priceId: "price_1U21SORCZcRie5rt1nxgIgVb",
    unitAmountCents: 5990,
    currency: "brl",
    statementDescriptor: "AIONIX*ULTRA MES",
  },
  {
    tier: "ultra",
    period: "anual",
    productId: "prod_V25eaaRUpjTLSB",
    priceId: "price_1U21TARCZcRie5rtxv6tZaW7",
    unitAmountCents: 59900,
    currency: "brl",
    statementDescriptor: "AIONIX*ULTRA ANO",
  },
];

export function findPriceById(priceId: string): PriceDefinition | undefined {
  return PRICES.find((p) => p.priceId === priceId);
}

// Limite mensal de processamento do plano Essencial: decisão comercial definitiva (ver DECISIONS.md D006).
// 300 minutos / 5 horas por ciclo mensal, armazenado internamente em segundos.
// Override via env var permitido para recalibração futura com base em uso/custo real — nunca espalhe
// o número 18000 em outros pontos do código, sempre importe esta constante.
export const ESSENCIAL_MONTHLY_QUOTA_SECONDS = process.env.ESSENCIAL_MONTHLY_QUOTA_SECONDS
  ? Number(process.env.ESSENCIAL_MONTHLY_QUOTA_SECONDS)
  : 18_000;
