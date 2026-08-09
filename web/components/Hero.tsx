"use client";

import dynamic from "next/dynamic";
import { motion, useReducedMotion } from "framer-motion";
import { DOWNLOAD_URL } from "@/lib/constants";

// ssr:false + carregamento tardio: o three.js (o maior pedaço da página) não entra no HTML inicial
// nem bloqueia a primeira pintura. Enquanto ele não chega — e para sempre, em quem pediu menos
// movimento — fica a alternativa estática abaixo, que já é bonita por si só.
const VoiceOrb = dynamic(() => import("./VoiceOrb"), {
  ssr: false,
  loading: () => <OrbFallback />,
});

function OrbFallback() {
  return (
    <div className="relative flex h-full w-full items-center justify-center">
      <div className="h-56 w-56 rounded-full bg-gradient-to-br from-accent to-accent-strong opacity-70 blur-2xl md:h-72 md:w-72" />
      <div className="absolute h-40 w-40 rounded-full border border-accent/25 md:h-52 md:w-52" />
      <div className="absolute h-56 w-56 rounded-full border border-accent/15 md:h-72 md:w-72" />
    </div>
  );
}

export function Hero() {
  const reduced = useReducedMotion();

  return (
    <section className="relative overflow-hidden px-6 pt-16 pb-24 md:pt-24 md:pb-32">
      <div className="glow left-1/2 top-0 h-[420px] w-[620px] -translate-x-1/2 bg-accent" />

      <div className="relative z-10 mx-auto grid max-w-6xl items-center gap-12 md:grid-cols-2 md:gap-8">
        <motion.div
          initial={reduced ? false : { opacity: 0, y: 24 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.7, ease: [0.22, 1, 0.36, 1] }}
        >
          <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-white/10 bg-white/[0.03] px-3 py-1.5">
            <span className="h-1.5 w-1.5 rounded-full bg-success" />
            <span className="text-xs text-muted">Disponível para Windows 10 e 11</span>
          </div>

          <h1 className="font-display text-4xl leading-[1.05] tracking-tight md:text-6xl">
            Falar é uma forma
            <br />
            superior de{" "}
            <span className="bg-gradient-to-r from-accent to-[#f0a273] bg-clip-text text-transparent italic">
              digitar
            </span>
          </h1>

          <p className="mt-6 max-w-lg text-base leading-relaxed text-muted md:text-lg">
            Pressione um atalho, fale naturalmente, e o texto limpo aparece onde o cursor
            estiver — no e-mail, no chat, no editor de código. Sem copiar, sem colar, sem
            trocar de janela.
          </p>

          <div className="mt-9 flex flex-col gap-3 sm:flex-row sm:items-center">
            <a
              href={DOWNLOAD_URL}
              className="group inline-flex items-center justify-center gap-2.5 rounded-xl bg-primary px-7 py-3.5 text-sm font-semibold text-ink transition-transform duration-200 hover:scale-[1.02] active:scale-100"
            >
              Baixar grátis para Windows
              <svg width="15" height="15" viewBox="0 0 16 16" fill="none" className="transition-transform group-hover:translate-y-0.5">
                <path d="M8 2v9m0 0 3.5-3.5M8 11 4.5 7.5M2.5 13.5h11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </a>
            <a
              href="#como-funciona"
              className="inline-flex items-center justify-center rounded-xl border border-white/10 px-7 py-3.5 text-sm font-semibold text-primary transition-colors hover:border-white/25 hover:bg-white/[0.04]"
            >
              Ver como funciona
            </a>
          </div>

          <p className="mt-5 text-xs leading-relaxed text-footnote">
            Instalação em poucos segundos. Sem cadastro para começar.
          </p>
        </motion.div>

        <motion.div
          className="relative h-[340px] md:h-[520px]"
          initial={reduced ? false : { opacity: 0, scale: 0.94 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ duration: 1, delay: 0.15, ease: [0.22, 1, 0.36, 1] }}
        >
          {reduced ? <OrbFallback /> : <VoiceOrb />}
        </motion.div>
      </div>
    </section>
  );
}
