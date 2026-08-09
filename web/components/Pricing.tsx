"use client";

import { useState } from "react";
import { API_URL, PLANS, formatBRL, type TierId } from "@/lib/constants";
import { Reveal, SectionHeading } from "./Sections";

type Status = "idle" | "sending" | "done" | "error";

/*
  Os preços aqui são os do Stripe de verdade. O botão NÃO leva a um checkout porque o checkout
  ainda não existe (P3 Passo 5) — vender uma assinatura que o app não sabe conceder seria pior do
  que não vender. Enquanto isso, o card capta quem quer ser avisado, gravando num endpoint real.
*/
export function Pricing() {
  const [period, setPeriod] = useState<"mensal" | "anual">("mensal");
  const [openTier, setOpenTier] = useState<TierId | null>(null);

  return (
    <section id="planos" className="relative px-6 py-24 md:py-32">
      <div className="glow left-1/4 top-1/4 h-[400px] w-[500px] bg-accent opacity-20" />

      <Reveal>
        <SectionHeading
          eyebrow="Planos"
          title="Comece grátis. Assine quando fizer diferença no seu dia."
          subtitle="O aplicativo está disponível para download agora. As assinaturas abrem em breve, com estes valores."
        />
      </Reveal>

      <Reveal delay={0.05}>
        <div className="mx-auto mt-10 flex w-fit items-center gap-1 rounded-full border border-white/10 bg-white/[0.03] p-1">
          {(["mensal", "anual"] as const).map((p) => (
            <button
              key={p}
              onClick={() => setPeriod(p)}
              className={`rounded-full px-5 py-2 text-sm font-medium transition-colors ${
                period === p ? "bg-primary text-ink" : "text-muted hover:text-primary"
              }`}
            >
              {p === "mensal" ? "Mensal" : "Anual"}
              {p === "anual" && <span className="ml-2 text-xs opacity-70">2 meses grátis</span>}
            </button>
          ))}
        </div>
      </Reveal>

      <div className="relative z-10 mx-auto mt-12 grid max-w-5xl items-start gap-5 md:grid-cols-3">
        {PLANS.map((plan, i) => {
          const price = period === "mensal" ? plan.monthly : plan.yearly;
          return (
            <Reveal key={plan.id} delay={i * 0.07}>
              <div
                className={`relative flex h-full flex-col rounded-2xl border p-7 transition-colors duration-300 ${
                  plan.highlighted
                    ? "border-accent/40 bg-gradient-to-b from-accent/[0.09] to-card/60"
                    : "border-white/[0.07] bg-card/60 hover:border-white/15"
                }`}
              >
                {plan.highlighted && (
                  <span className="absolute -top-3 left-7 rounded-full bg-accent px-3 py-1 text-[11px] font-semibold text-ink">
                    Mais escolhido
                  </span>
                )}

                <h3 className="font-display text-2xl">{plan.name}</h3>
                <p className="mt-2 min-h-[40px] text-sm leading-relaxed text-muted">{plan.pitch}</p>

                <div className="mt-6 flex items-baseline gap-1.5">
                  <span className="font-display text-4xl">{formatBRL(price)}</span>
                  <span className="text-sm text-footnote">/{period === "mensal" ? "mês" : "ano"}</span>
                </div>
                <p className="mt-2 text-xs text-accent">{plan.quota}</p>

                <ul className="mt-7 flex-1 space-y-3">
                  {plan.features.map((f) => (
                    <li key={f} className="flex gap-2.5 text-sm leading-relaxed text-muted">
                      <svg width="15" height="15" viewBox="0 0 16 16" fill="none" className="mt-1 shrink-0">
                        <path d="M3 8.5 6.2 11.5 13 4.5" stroke="#e8763f" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                      {f}
                    </li>
                  ))}
                </ul>

                <div className="mt-7">
                  {openTier === plan.id ? (
                    <WaitlistForm tier={plan.id} onClose={() => setOpenTier(null)} />
                  ) : (
                    <button
                      onClick={() => setOpenTier(plan.id)}
                      className={`w-full rounded-xl px-5 py-3 text-sm font-semibold transition-transform duration-200 hover:scale-[1.02] ${
                        plan.highlighted
                          ? "bg-primary text-ink"
                          : "border border-white/12 text-primary hover:bg-white/[0.05]"
                      }`}
                    >
                      Avise-me quando abrir
                    </button>
                  )}
                </div>
              </div>
            </Reveal>
          );
        })}
      </div>

      <p className="mx-auto mt-10 max-w-xl text-center text-xs leading-relaxed text-footnote">
        Enquanto as assinaturas não abrem, o aplicativo pode ser baixado e usado. Avisamos por e-mail
        assim que der para assinar — e não usamos seu endereço para mais nada.
      </p>
    </section>
  );
}

function WaitlistForm({ tier, onClose }: { tier: TierId; onClose: () => void }) {
  const [email, setEmail] = useState("");
  const [status, setStatus] = useState<Status>("idle");
  const [error, setError] = useState("");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setStatus("sending");
    setError("");

    try {
      const res = await fetch(`${API_URL}/api/waitlist`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, tier, source: "site-planos" }),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setError(data.error ?? "Não consegui registrar agora.");
        setStatus("error");
        return;
      }
      setStatus("done");
    } catch {
      setError("Sem conexão com o servidor. Tente de novo.");
      setStatus("error");
    }
  }

  if (status === "done") {
    return (
      <div className="rounded-xl border border-success/30 bg-success/[0.08] px-4 py-3 text-center">
        <p className="text-sm font-semibold text-success">Pronto, você está na lista</p>
        <p className="mt-1 text-xs text-muted">Avisamos assim que as assinaturas abrirem.</p>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="space-y-2">
      <input
        type="email"
        required
        autoFocus
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        placeholder="seu@email.com"
        className="w-full rounded-xl border border-white/12 bg-white/[0.04] px-4 py-3 text-sm text-primary outline-none transition-colors placeholder:text-footnote focus:border-accent/60"
      />
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={status === "sending"}
          className="flex-1 rounded-xl bg-primary px-4 py-3 text-sm font-semibold text-ink disabled:opacity-60"
        >
          {status === "sending" ? "Enviando..." : "Entrar na lista"}
        </button>
        <button
          type="button"
          onClick={onClose}
          className="rounded-xl border border-white/12 px-4 py-3 text-sm text-muted hover:text-primary"
        >
          Cancelar
        </button>
      </div>
      {error && <p className="text-xs text-red-400">{error}</p>}
    </form>
  );
}
