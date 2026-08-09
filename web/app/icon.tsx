import { ImageResponse } from "next/og";

// Favicon gerado em build a partir da mesma marca do app desktop (as quatro barras de áudio em
// laranja sobre o fundo escuro do tema), em vez de um .ico binário que ninguém consegue revisar
// num diff. O Next serve isto como /icon e injeta a tag no <head> automaticamente.
export const size = { width: 64, height: 64 };
export const contentType = "image/png";

export default function Icon() {
  // A barra mais alta é a segunda, igual ao wordmark do site e ao ícone da bandeja — o ícone
  // precisa ser reconhecível como a mesma marca em 16px na aba do navegador.
  const bars = [
    { h: 20, o: 0.55 },
    { h: 38, o: 1 },
    { h: 28, o: 0.8 },
    { h: 14, o: 0.45 },
  ];

  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          gap: 5,
          background: "#18161d",
          borderRadius: 14,
        }}
      >
        {bars.map((b, i) => (
          <div
            key={i}
            style={{
              width: 7,
              height: b.h,
              borderRadius: 4,
              background: "#e8763f",
              opacity: b.o,
            }}
          />
        ))}
      </div>
    ),
    size,
  );
}
