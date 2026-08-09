import type { Metadata } from "next";
import { Fraunces, Sora } from "next/font/google";
import "./globals.css";

// Mesmas duas famílias do app desktop: Fraunces como voz da marca, Sora como texto de leitura.
const fraunces = Fraunces({
  subsets: ["latin"],
  variable: "--font-fraunces",
  display: "swap",
  axes: ["SOFT", "WONK", "opsz"],
});

const sora = Sora({
  subsets: ["latin"],
  variable: "--font-sora",
  display: "swap",
});

export const metadata: Metadata = {
  metadataBase: new URL("https://aionix-scribe.vercel.app"),
  title: "Aionix Scribe — falar é uma forma superior de digitar",
  description:
    "Ditado por voz inteligente para Windows. Pressione um atalho, fale, e o texto limpo aparece onde o cursor estiver — em qualquer aplicativo.",
  openGraph: {
    title: "Aionix Scribe — falar é uma forma superior de digitar",
    description:
      "Ditado por voz inteligente para Windows. Fale em qualquer aplicativo e receba texto pronto, sem hesitações e sem tradução indevida.",
    type: "website",
    locale: "pt_BR",
  },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR" className={`${fraunces.variable} ${sora.variable}`}>
      <body className="antialiased">
        <div className="grain" aria-hidden />
        {children}
      </body>
    </html>
  );
}
