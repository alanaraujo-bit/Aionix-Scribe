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
  if (typeof text !== "string") {
    throw new GeminiError("Resposta da Gemini sem texto utilizável", 502, json);
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
