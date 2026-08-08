# Pendências do Usuário

## 1. Política de trial para "autenticado sem assinatura" (P3)

Quando um usuário faz login (D018) mas ainda não assinou nenhum plano, o que acontece? Duas opções concretas, ou uma terceira que você preferir:
- **(a) Bloquear direto**: 402 "assinatura necessária" até escolher um plano.
- **(b) Trial**: liberar X minutos por Y dias antes de exigir pagamento — nesse caso preciso saber quantos minutos e por quantos dias.

**Enquanto não decidir**: o backend assume (a) — bloqueio direto — como default seguro, e o Passo 2 do P3 (entitlement) já reflete isso. Trocar pra (b) depois é uma mudança pequena e isolada, não vai exigir refazer nada já implementado.

## 2. Aprovação do Auth0 como novo fornecedor SaaS (P3)

A arquitetura de autenticação (D018) recomenda Auth0 pra login do app desktop (fluxo OAuth padrão pra apps nativos, free tier cobre o volume esperado de P3-P5). Isso é a criação de uma conta/tenant nova num fornecedor externo — não é algo que a engenharia decide sozinha, é análogo à conta Stripe que você já forneceu.

Preciso que você:
- Confirme que Auth0 é aceitável (ou indique outro provedor de sua preferência — Clerk, Supabase Auth, etc. são alternativas viáveis, a arquitetura foi desenhada pra trocar de provedor sem reescrever o schema).
- Crie a conta/tenant (ou me avise se prefere que eu tente criar — algumas etapas de cadastro podem exigir dados pessoais/pagamento que só você deve fornecer).

**Enquanto não decidir**: o Passo 0 do P3 (Postgres/schema/migração) não depende disso e já está em andamento. Só o Passo 1 (login de verdade) fica bloqueado.

---

*Nota sobre uma terceira questão que **não** é pendência*: D018 também levanta se um ditado sem fala detectável (silêncio/ruído) deveria consumir a cota do plano Essencial. A regra já definida em D006 ("só contabilizar áudio efetivamente processado") já responde isso — o áudio foi enviado e processado pela Gemini, o custo real foi incorrido, então consome. Adotei esse default; se você quiser inverter depois é uma mudança de uma linha, não precisa decidir agora.

---

O desenvolvimento pode continuar autonomamente nas partes não bloqueadas por essas pendências.

---

*Critério para algo entrar aqui: exige uma ação que exclusivamente o proprietário pode tomar (fornecer uma credencial que não tenho, decidir algo que só ele pode decidir, aprovar um gasto/compra). Qualquer coisa que a engenharia consiga executar sozinha é tarefa de roadmap, não pendência — mesmo que ainda não tenha sido implementada.*
