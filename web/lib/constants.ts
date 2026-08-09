/// Link PERMANENTE de download: aponta para o ativo de nome fixo do release mais recente, então
/// não precisa ser trocado a cada versão publicada. Ver DECISIONS.md D021.
export const DOWNLOAD_URL =
  "https://github.com/alanaraujo-bit/Aionix-Scribe/releases/latest/download/AionixScribe-Setup.exe";

export const API_URL = "https://aionix-scribe-api-production.up.railway.app";

export type TierId = "essencial" | "premium" | "ultra";

/// Valores REAIS do Stripe, espelhados de backend/src/config/tiers.ts. Nenhum número aqui é
/// inventado ou "de exemplo" — o site anuncia exatamente o que será cobrado quando o checkout abrir.
export const PLANS: {
  id: TierId;
  name: string;
  monthly: number;
  yearly: number;
  pitch: string;
  quota: string;
  features: string[];
  highlighted?: boolean;
}[] = [
  {
    id: "essencial",
    name: "Essencial",
    monthly: 14.9,
    yearly: 149,
    pitch: "Para quem escreve todos os dias e quer parar de digitar o óbvio.",
    quota: "300 minutos de ditado por mês",
    features: [
      "Ditado global em qualquer aplicativo",
      "Limpeza automática de hesitações e repetições",
      "Escreve no idioma que você falou, sem traduzir",
      "Histórico local dos seus ditados",
      "Atualizações automáticas",
    ],
  },
  {
    id: "premium",
    name: "Premium",
    monthly: 29.9,
    yearly: 299,
    pitch: "Para quem vive escrevendo e não quer pensar em minutos restantes.",
    quota: "Ditado sem franquia mensal",
    features: [
      "Tudo do Essencial",
      "Sem limite mensal de minutos",
      "Prioridade nas melhorias do produto",
    ],
    highlighted: true,
  },
  {
    id: "ultra",
    name: "Ultra",
    monthly: 59.9,
    yearly: 599,
    pitch: "Para quem trabalha em mais de um idioma o dia inteiro.",
    quota: "Ditado sem franquia mensal",
    features: ["Tudo do Premium", "Recursos avançados de idioma conforme forem liberados"],
  },
];

export function formatBRL(value: number) {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}
