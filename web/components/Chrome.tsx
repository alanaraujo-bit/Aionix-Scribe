"use client";

import { DOWNLOAD_URL } from "@/lib/constants";
import { Reveal } from "./Sections";

function Wordmark() {
  return (
    <div className="flex items-center gap-2.5">
      <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden>
        {[3, 6.5, 10, 13.5].map((x, i) => {
          const h = [6, 12, 9, 4][i];
          return (
            <rect
              key={x}
              x={x - 0.9}
              y={9 - h / 2}
              width="1.8"
              height={h}
              rx="0.9"
              fill="#e8763f"
              opacity={i === 1 ? 1 : 0.6}
            />
          );
        })}
      </svg>
      <span className="font-display text-[17px] font-semibold">Aionix Scribe</span>
    </div>
  );
}

export function Nav() {
  return (
    <header className="sticky top-0 z-50 border-b border-white/[0.06] bg-ink/70 backdrop-blur-xl">
      <nav className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
        <Wordmark />
        <div className="hidden items-center gap-8 md:flex">
          <a href="#como-funciona" className="text-sm text-muted transition-colors hover:text-primary">Como funciona</a>
          <a href="#recursos" className="text-sm text-muted transition-colors hover:text-primary">Recursos</a>
          <a href="#privacidade" className="text-sm text-muted transition-colors hover:text-primary">Privacidade</a>
          <a href="#planos" className="text-sm text-muted transition-colors hover:text-primary">Planos</a>
        </div>
        <a
          href={DOWNLOAD_URL}
          className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-ink transition-transform hover:scale-[1.03]"
        >
          Baixar
        </a>
      </nav>
    </header>
  );
}

export function FinalCta() {
  return (
    <section className="relative overflow-hidden px-6 py-24 md:py-32">
      <div className="glow left-1/2 top-1/2 h-[380px] w-[560px] -translate-x-1/2 -translate-y-1/2 bg-accent" />
      <Reveal>
        <div className="relative z-10 mx-auto max-w-2xl text-center">
          <h2 className="font-display text-3xl leading-tight tracking-tight md:text-5xl">
            Você digita mais devagar do que pensa.
          </h2>
          <p className="mx-auto mt-5 max-w-lg text-base leading-relaxed text-muted">
            Instale, aperte o atalho e fale a primeira frase. Dá para saber se serve para você em
            menos de um minuto.
          </p>
          <a
            href={DOWNLOAD_URL}
            className="mt-9 inline-flex items-center gap-2.5 rounded-xl bg-primary px-8 py-4 text-sm font-semibold text-ink transition-transform duration-200 hover:scale-[1.02]"
          >
            Baixar grátis para Windows
          </a>
          <p className="mt-5 text-xs text-footnote">
            Windows 10 ou 11, 64 bits. O instalador não é assinado digitalmente ainda, então o
            SmartScreen pede uma confirmação extra na primeira execução.
          </p>
        </div>
      </Reveal>
    </section>
  );
}

export function Footer() {
  return (
    <footer className="border-t border-white/[0.06] px-6 py-12">
      <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-6 md:flex-row">
        <Wordmark />
        <div className="flex items-center gap-7">
          <a
            href="https://github.com/alanaraujo-bit/Aionix-Scribe"
            className="text-sm text-muted transition-colors hover:text-primary"
            target="_blank"
            rel="noreferrer"
          >
            GitHub
          </a>
          <a href="#planos" className="text-sm text-muted transition-colors hover:text-primary">Planos</a>
          <a href={DOWNLOAD_URL} className="text-sm text-muted transition-colors hover:text-primary">Download</a>
        </div>
        <p className="text-xs text-footnote">© {new Date().getFullYear()} Aionix</p>
      </div>
    </footer>
  );
}
