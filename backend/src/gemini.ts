const GEMINI_MODEL = process.env.GEMINI_MODEL ?? "gemini-flash-latest";
const GEMINI_BASE_URL = "https://generativelanguage.googleapis.com/v1beta";

const TRANSCRIPTION_PROMPT = `Você é o motor de transcrição do Aionix Scribe, um ditado por voz inteligente.
Transcreva o áudio a seguir para português do Brasil, transformando fala natural em texto útil:
- remova hesitações, repetições acidentais, falsos começos e pausas de preenchimento (tipo "é...", "então...", "deixa eu pensar...");
- adicione pontuação, capitalização e estrutura de frase corretas;
- NUNCA altere a intenção do usuário nem invente conteúdo que não foi dito.
Responda APENAS com o texto final limpo, sem comentários, sem aspas, sem explicações.`;

export interface TranscribeResult {
  text: string;
  modelVersion: string;
  geminiLatencyMs: number;
  usage: {
    promptTokens: number;
    candidateTokens: number;
    totalTokens: number;
  };
}

export class GeminiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly body: unknown,
  ) {
    super(message);
  }
}

export async function transcribeAudio(audioBase64: string, mimeType: string): Promise<TranscribeResult> {
  const apiKey = process.env.GEMINI_API_KEY;
  if (!apiKey) {
    throw new Error("GEMINI_API_KEY não configurada no ambiente");
  }

  const payload = {
    contents: [
      {
        parts: [{ text: TRANSCRIPTION_PROMPT }, { inline_data: { mime_type: mimeType, data: audioBase64 } }],
      },
    ],
    // Transcrição/limpeza é uma tarefa direta, não de raciocínio — sem limitar o orçamento de
    // "thinking", o modelo pode gastar todos os tokens de saída pensando e devolver conteúdo
    // vazio (finishReason STOP, sem texto) para áudio curto/ambíguo. Visto em produção (ver
    // DECISIONS.md D011). thinkingBudget baixo evita isso sem prejudicar a qualidade da limpeza.
    generationConfig: {
      thinkingConfig: { thinkingBudget: 256 },
    },
  };

  const start = performance.now();
  const res = await fetch(`${GEMINI_BASE_URL}/models/${GEMINI_MODEL}:generateContent?key=${apiKey}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  const geminiLatencyMs = performance.now() - start;

  const json = (await res.json()) as any;

  if (!res.ok) {
    throw new GeminiError(json?.error?.message ?? "Erro na chamada à Gemini API", res.status, json);
  }

  const text: string | undefined = json?.candidates?.[0]?.content?.parts?.find((p: any) => typeof p.text === "string")?.text;
  const finishReason = json?.candidates?.[0]?.finishReason;

  // finishReason STOP com conteúdo vazio (sem texto) acontece em áudio curto/ambíguo — tratamos
  // como "nenhuma fala detectada" (resultado normal), não como erro. Qualquer outro finishReason
  // sem texto (SAFETY, RECITATION, etc.) ainda é reportado como falha real.
  if (typeof text !== "string") {
    if (finishReason === "STOP") {
      return {
        text: "",
        modelVersion: json?.modelVersion ?? GEMINI_MODEL,
        geminiLatencyMs,
        usage: {
          promptTokens: json?.usageMetadata?.promptTokenCount ?? 0,
          candidateTokens: json?.usageMetadata?.candidatesTokenCount ?? 0,
          totalTokens: json?.usageMetadata?.totalTokenCount ?? 0,
        },
      };
    }
    throw new GeminiError(`Resposta da Gemini sem texto utilizável (finishReason=${finishReason})`, 502, json);
  }

  return {
    text: text.trim(),
    modelVersion: json?.modelVersion ?? GEMINI_MODEL,
    geminiLatencyMs,
    usage: {
      promptTokens: json?.usageMetadata?.promptTokenCount ?? 0,
      candidateTokens: json?.usageMetadata?.candidatesTokenCount ?? 0,
      totalTokens: json?.usageMetadata?.totalTokenCount ?? 0,
    },
  };
}
