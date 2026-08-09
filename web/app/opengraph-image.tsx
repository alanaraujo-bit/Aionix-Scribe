import { ImageResponse } from "next/og";

// Imagem de preview de link (WhatsApp, Discord, Slack, X). Sem ela, compartilhar o site mostra um
// retângulo vazio — o que é justamente o motivo de o proprietário ter pedido o favicon: o link
// precisa parecer um produto, não um endereço solto.
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";
export const alt = "Aionix Scribe — falar é uma forma superior de digitar";

export default function OgImage() {
  const bars = [
    { h: 34, o: 0.55 },
    { h: 64, o: 1 },
    { h: 48, o: 0.8 },
    { h: 24, o: 0.45 },
  ];

  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          padding: "0 90px",
          background: "#131118",
          position: "relative",
        }}
      >
        {/* Brilho da marca, ecoando o gradiente do topo do site. */}
        <div
          style={{
            position: "absolute",
            top: -160,
            right: -120,
            width: 620,
            height: 620,
            borderRadius: 9999,
            background: "radial-gradient(circle, rgba(232,118,63,0.42) 0%, rgba(19,17,24,0) 70%)",
            display: "flex",
          }}
        />

        <div style={{ display: "flex", alignItems: "center", gap: 14, marginBottom: 40 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 5 }}>
            {bars.map((b, i) => (
              <div key={i} style={{ width: 8, height: b.h, borderRadius: 4, background: "#e8763f", opacity: b.o }} />
            ))}
          </div>
          <div style={{ fontSize: 34, color: "#f2f0ed", fontWeight: 600 }}>Aionix Scribe</div>
        </div>

        <div style={{ fontSize: 78, color: "#f2f0ed", lineHeight: 1.06, letterSpacing: -2, display: "flex", flexDirection: "column" }}>
          <span>Falar é uma forma</span>
          {/* O espaço antes de "digitar" precisa ser um NBSP explícito: o renderizador de imagem
              colapsa o espaço solto entre um texto e um <span> irmão, e a imagem saía escrita
              "dedigitar" — visto na imagem gerada, não deduzido. */}
          <span style={{ display: "flex" }}>
            superior&nbsp;de&nbsp;<span style={{ color: "#e8763f" }}>digitar</span>
          </span>
        </div>

        <div style={{ fontSize: 28, color: "#a6a3ac", marginTop: 34, maxWidth: 820, lineHeight: 1.4, display: "flex" }}>
          Ditado por voz inteligente para Windows. Aperte um atalho, fale, e o texto limpo aparece
          onde o cursor estiver.
        </div>
      </div>
    ),
    size,
  );
}
