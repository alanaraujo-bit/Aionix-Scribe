"use client";

import { motion, useReducedMotion } from "framer-motion";
import type { ReactNode } from "react";

/// Entrada por scroll reaproveitada em todas as seções. `once` evita reanimar ao rolar de volta,
/// que vira distração em vez de refinamento.
export function Reveal({ children, delay = 0 }: { children: ReactNode; delay?: number }) {
  const reduced = useReducedMotion();
  if (reduced) return <>{children}</>;

  return (
    <motion.div
      initial={{ opacity: 0, y: 22 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: "-80px" }}
      transition={{ duration: 0.65, delay, ease: [0.22, 1, 0.36, 1] }}
    >
      {children}
    </motion.div>
  );
}

export function SectionHeading({
  eyebrow,
  title,
  subtitle,
}: {
  eyebrow: string;
  title: ReactNode;
  subtitle?: string;
}) {
  return (
    <div className="mx-auto max-w-2xl text-center">
      <p className="mb-4 text-xs font-semibold tracking-[0.18em] text-accent uppercase">{eyebrow}</p>
      <h2 className="font-display text-3xl leading-tight tracking-tight md:text-5xl">{title}</h2>
      {subtitle && <p className="mt-5 text-base leading-relaxed text-muted">{subtitle}</p>}
    </div>
  );
}

const STEPS = [
  {
    n: "01",
    title: "Pressione o atalho",
    body: "Um atalho global funciona em qualquer aplicativo do Windows. Se o combo padrão já estiver ocupado na sua máquina, o Aionix Scribe encontra outro sozinho.",
  },
  {
    n: "02",
    title: "Fale como você fala",
    body: "Sem roteiro, sem dicção de locutor. Hesitações, repetições e começos falsos são removidos automaticamente — o que sai é o que você quis dizer.",
  },
  {
    n: "03",
    title: "O texto aparece onde o cursor está",
    body: "Direto no campo em foco: o e-mail, a mensagem, o editor de código. Sem copiar, sem colar, sem trocar de janela.",
  },
];

export function HowItWorks() {
  return (
    <section id="como-funciona" className="relative px-6 py-24 md:py-32">
      <Reveal>
        <SectionHeading
          eyebrow="Como funciona"
          title="Três passos, e nenhum deles é abrir o Aionix Scribe"
          subtitle="Ele vive na bandeja do sistema e só aparece quando você chama."
        />
      </Reveal>

      <div className="mx-auto mt-16 grid max-w-5xl gap-5 md:grid-cols-3">
        {STEPS.map((step, i) => (
          <Reveal key={step.n} delay={i * 0.08}>
            <div className="group h-full rounded-2xl border border-white/[0.07] bg-card/60 p-7 transition-colors duration-300 hover:border-accent/30">
              <span className="font-display text-3xl text-accent/50 transition-colors duration-300 group-hover:text-accent">
                {step.n}
              </span>
              <h3 className="mt-5 text-lg font-semibold">{step.title}</h3>
              <p className="mt-3 text-sm leading-relaxed text-muted">{step.body}</p>
            </div>
          </Reveal>
        ))}
      </div>
    </section>
  );
}

const FEATURES = [
  {
    title: "Escreve no idioma que você falou",
    body: "Falou português, sai português. Falou inglês, sai inglês. E termos técnicos ficam como você disse — \"fiz o deploy\", \"abre um pull request\" — em vez de virarem traduções que ninguém usa.",
    span: "md:col-span-2",
  },
  {
    title: "Limpeza automática",
    body: "Remove \"é...\", \"então...\", repetições e frases recomeçadas. Aplica pontuação e capitalização.",
  },
  {
    title: "Funciona em qualquer app",
    body: "Não é uma caixa de texto separada. É o campo onde você já estava escrevendo.",
  },
  {
    title: "Silêncio nunca sai da sua máquina",
    body: "Um toque acidental no atalho é descartado localmente: nada é enviado, nada é processado, nada é cobrado.",
    span: "md:col-span-2",
  },
];

export function Features() {
  return (
    <section id="recursos" className="relative px-6 py-24 md:py-32">
      <div className="glow right-0 top-1/3 h-[380px] w-[420px] bg-[#7c5cff] opacity-20" />
      <Reveal>
        <SectionHeading
          eyebrow="O que ele faz de diferente"
          title="Transcrever é fácil. O difícil é entregar texto que já dá para enviar."
        />
      </Reveal>

      <div className="relative z-10 mx-auto mt-16 grid max-w-5xl gap-5 md:grid-cols-3">
        {FEATURES.map((f, i) => (
          <Reveal key={f.title} delay={i * 0.07}>
            <div className={`h-full rounded-2xl border border-white/[0.07] bg-card/60 p-7 ${f.span ?? ""}`}>
              <h3 className="text-lg font-semibold">{f.title}</h3>
              <p className="mt-3 text-sm leading-relaxed text-muted">{f.body}</p>
            </div>
          </Reveal>
        ))}
      </div>
    </section>
  );
}

export function Privacy() {
  return (
    <section id="privacidade" className="relative px-6 py-24 md:py-32">
      <div className="mx-auto max-w-4xl rounded-3xl border border-white/[0.07] bg-gradient-to-b from-card/80 to-card/30 p-10 md:p-14">
        <Reveal>
          <p className="mb-4 text-xs font-semibold tracking-[0.18em] text-accent uppercase">Privacidade</p>
          <h2 className="font-display text-3xl leading-tight tracking-tight md:text-4xl">
            Seu histórico não fica no nosso servidor. Ele nem chega lá.
          </h2>
          <div className="mt-8 grid gap-6 md:grid-cols-2">
            <div>
              <h3 className="text-sm font-semibold">O áudio não é armazenado</h3>
              <p className="mt-2 text-sm leading-relaxed text-muted">
                Ele é processado em memória durante a transcrição e descartado. Não vai para disco
                nem para banco de dados.
              </p>
            </div>
            <div>
              <h3 className="text-sm font-semibold">O histórico é só seu</h3>
              <p className="mt-2 text-sm leading-relaxed text-muted">
                Seus ditados ficam gravados apenas na sua máquina. O app mostra a pasta exata e tem
                um botão para apagar tudo.
              </p>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
