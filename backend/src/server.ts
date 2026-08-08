import Fastify from "fastify";
import { sql } from "drizzle-orm";
import { transcribeAudio, GeminiError } from "./gemini.js";
import { db } from "./db/index.js";

const app = Fastify({ logger: true, bodyLimit: 30 * 1024 * 1024 });

// Áudio chega como corpo binário bruto (o cliente desktop envia Content-Type: audio/wav | audio/webm | audio/mp3).
for (const mime of ["audio/wav", "audio/webm", "audio/mpeg", "audio/mp4", "audio/x-m4a"]) {
  app.addContentTypeParser(mime, { parseAs: "buffer" }, (_req, body, done) => done(null, body));
}

app.get("/health", async () => ({ status: "ok", timestamp: new Date().toISOString() }));

app.get("/health/db", async (_req, reply) => {
  try {
    await db.execute(sql`SELECT 1`);
    return reply.send({ status: "ok", database: "connected" });
  } catch (err) {
    app.log.error(err, "Falha ao conectar no Postgres");
    // err é DrizzleQueryError ("Failed query: ..."); a causa real está em err.cause, que pode ser um
    // AggregateError do Node (ex.: várias tentativas ECONNREFUSED) sem message própria — nesse caso
    // a mensagem útil está em cause.errors[0].
    let cause: string | null = null;
    if (err instanceof Error && err.cause instanceof Error) {
      cause = err.cause.message || null;
      if (!cause && "errors" in err.cause && Array.isArray((err.cause as AggregateError).errors)) {
        cause = (err.cause as AggregateError).errors[0]?.message ?? null;
      }
    }
    const message = cause ?? (err instanceof Error ? err.message : "Erro desconhecido");
    return reply.code(503).send({ status: "error", database: "disconnected", error: message });
  }
});

// Stopgap enquanto não existe conta/dispositivo real (P3): sem isso, a URL pública do endpoint
// permite que qualquer um queime a cota da Gemini. Não é autenticação de usuário de verdade.
const desktopSharedSecret = process.env.DESKTOP_SHARED_SECRET;

app.post("/api/transcribe", async (req, reply) => {
  if (desktopSharedSecret && req.headers["x-app-secret"] !== desktopSharedSecret) {
    return reply.code(401).send({ error: "Não autorizado" });
  }

  const requestStart = performance.now();
  const mimeType = req.headers["content-type"];
  const body = req.body;

  if (!mimeType || !Buffer.isBuffer(body) || body.length === 0) {
    return reply.code(400).send({ error: "Envie áudio binário com Content-Type audio/wav, audio/webm, audio/mpeg, audio/mp4 ou audio/x-m4a" });
  }

  try {
    const audioBase64 = body.toString("base64");
    const result = await transcribeAudio(audioBase64, mimeType);
    const totalLatencyMs = performance.now() - requestStart;

    return reply.send({
      text: result.text,
      modelVersion: result.modelVersion,
      usage: result.usage,
      latency: {
        totalMs: Math.round(totalLatencyMs),
        geminiMs: Math.round(result.geminiLatencyMs),
      },
    });
  } catch (err) {
    if (err instanceof GeminiError) {
      req.log.error({ status: err.status, body: err.body }, "Gemini API error");
      return reply.code(502).send({ error: "Falha ao transcrever com a Gemini API", detail: err.message });
    }
    req.log.error(err, "Unexpected error in /api/transcribe");
    return reply.code(500).send({ error: "Erro interno" });
  }
});

const port = Number(process.env.PORT ?? 3000);
app.listen({ port, host: "0.0.0.0" }).catch((err) => {
  app.log.error(err);
  process.exit(1);
});
