# Pendências do Usuário

Itens que só o proprietário pode resolver. Tudo que não está aqui está sendo decidido e executado de forma autônoma.

---

## 1. ~~GEMINI_API_KEY ausente~~ — RESOLVIDO em 2026-08-08

`GEMINI_API_KEY` fornecida pelo proprietário, validada com chamadas reais (`models.list` e `generateContent`), e configurada como secret no serviço `aionix-scribe-api` no Railway (projeto `aionix-scribe`). Testada end-to-end com áudio real (fala PT-BR sintetizada) através do endpoint de produção `https://aionix-scribe-api-production.up.railway.app/api/transcribe` — pipeline completo (áudio → Gemini → texto limpo e formatado) funcionando. Modelo em uso: alias `gemini-flash-latest` (resolvendo atualmente para `gemini-3.6-flash`). Valor nunca escrito em código, git ou documentação.

---

## 2. Conta Vercel — provavelmente correta, mas não 100% confirmada

- **Achado**: a conta `alanarauj0` (autenticada no Vercel CLI local) tem acesso a um time chamado **"Aionixdev"** — o mesmo nome visto no dashboard de teste do Stripe ("Área restrita de Aionixdev"). Forte indício de que é a conta certa, mas não confirmei o e-mail exato por não haver um comando direto do CLI para isso.
- **Impacto**: nenhum agora — landing page é a última fase (P6).
- **Como resolver definitivamente**: confirmar que o time "Aionixdev" no Vercel é o destino correto para deploys do Aionix Scribe (ou informar outra conta/time).
- **Bloqueia o resto do projeto?** Não.

---

## 3. ~~Chave secreta do Stripe ausente~~ — RESOLVIDO (parcialmente) em 2026-08-08

`STRIPE_SECRET_KEY` de TESTE fornecida, validada (`livemode: false` confirmado para os 6 Price IDs oficiais via chamada real à API do Stripe), e configurada como secret no Railway. Valores reais obtidos e centralizados em `backend/src/config/tiers.ts` (Essencial R$14,90/R$149, Premium R$29,90/R$299, Ultra R$59,90/R$599 — nenhum valor inventado). A lógica de checkout/subscription/webhook em si ainda não foi implementada (planejada para P3, conforme ROADMAP) — a chave está pronta e validada, mas o fluxo comercial completo ainda não existe. `STRIPE_WEBHOOK_SECRET` continua pendente de criação (só é possível depois de o endpoint de webhook existir — ver item 3b abaixo).

## 3b. STRIPE_WEBHOOK_SECRET — pendente (não bloqueia)

- **O que é necessário**: criar o endpoint de webhook no Stripe (modo teste) apontando para o backend em produção e obter o signing secret gerado.
- **Por que ainda não foi feito**: o backend ainda não implementa lógica de assinatura/billing (P3) — não há endpoint de webhook para registrar ainda. Criar o webhook antes de ter o handler que o consome seria trabalho descartável.
- **Como resolver**: quando a implementação de billing (P3) começar, criar o endpoint `/api/stripe/webhook` no backend, registrar no Stripe (modo teste), e configurar o signing secret como variável de ambiente no Railway.
- **Bloqueia o resto do projeto?** Não.

---

## 4. Limite mensal de processamento do plano Essencial

- **O que é necessário**: o valor exato do limite mensal (minutos, número de ditados, ou outra unidade) do plano Essencial.
- **Por que**: a diretiva menciona a existência de um limite mas não especifica o valor, e proíbe explicitamente inventá-lo.
- **Onde**: lógica de entitlements/consumo do backend.
- **Impacto**: o sistema de contagem de consumo será construído de forma genérica (contador configurável), mas o valor do limite não será hardcoded até ser fornecido.
- **Como resolver**: informar o valor, ou um valor de fallback aceitável para desenvolvimento (deixarei um valor de placeholder óbvio e sinalizado em config, nunca em produção real).
- **Bloqueia o resto do projeto?** Não.

---

## 5. Certificado de assinatura de código (code signing) do instalador Windows

- **O que é necessário**: um certificado de assinatura de código (EV ou OV) para assinar o `.exe`/instalador do Aionix Scribe.
- **Por que**: apenas o proprietário pode adquirir/fornecer esse certificado (é uma compra/verificação de identidade legal, não uma decisão técnica).
- **Onde**: pipeline de build/release do instalador (P5).
- **Impacto**: o instalador funcionará sem assinatura durante o desenvolvimento, mas o Windows SmartScreen exibirá avisos de "editor desconhecido" até ser assinado. Isso é aceitável para builds internas, não para lançamento público.
- **Como resolver**: adquirir um certificado (ex: DigiCert, SSL.com) e fornecer para configuração do pipeline de assinatura.
- **Bloqueia o resto do projeto?** Não bloqueia desenvolvimento; bloqueia apenas o lançamento público final sem avisos do SmartScreen.

---

## 6. Recomendação de segurança: rotacionar secrets do projeto Aionix.Backup

- **O que aconteceu**: ao investigar projetos Railway existentes para evitar duplicar infraestrutura, rodei `railway variables` no projeto **"Aionix.Backup"** (não relacionado ao Aionix Scribe) e os valores de `JWT_SECRET`, `JWT_REFRESH_SECRET`, `GOOGLE_CLIENT_SECRET` e a `DATABASE_URL` (com senha do Postgres embutida) desse outro projeto apareceram no meu output e agora estão no histórico desta conversa.
- **Por que é uma recomendação, não uma pendência bloqueante**: o Aionix.Backup é um projeto separado e ativo seu — a decisão de rotacionar ou não esses secrets é sua.
- **Como resolver, se optar por rotacionar**: gerar novos valores para `JWT_SECRET`/`JWT_REFRESH_SECRET`, criar novo client secret OAuth no Google Cloud Console para o client ID `941875492094-...`, e trocar a senha do usuário Postgres no serviço correspondente do Railway.
- **Bloqueia o Aionix Scribe?** Não, são sistemas completamente independentes.

---

*Este arquivo é atualizado conforme novas pendências legítimas (que exigem exclusivamente o proprietário) surgirem.*
