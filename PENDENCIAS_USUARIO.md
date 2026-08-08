# Pendências do Usuário

Itens que só o proprietário pode resolver. Tudo que não está aqui está sendo decidido e executado de forma autônoma.

---

## 1. GEMINI_API_KEY ausente

- **O que é necessário**: a chave real da Gemini API (projeto Google `522832932628`, credencial "Gemini API Key").
- **Por que**: não está presente no ambiente (`env | grep -i gemini` retornou vazio). Não posso inventar nem simular uma chave funcional.
- **Onde**: variável de ambiente `GEMINI_API_KEY` no backend (nunca no cliente/instalador).
- **Impacto**: o pipeline de voz (P0 — captura → transcrição → formatação) pode ser inteiramente implementado e testado com mocks, mas **não pode ser validado end-to-end com áudio real** até a chave ser fornecida.
- **Como resolver**: adicionar a chave como secret no ambiente de execução do backend (ex: `railway variables set GEMINI_API_KEY=...` no projeto Railway) ou informar o valor para que eu a configure.
- **Bloqueia o resto do projeto?** Não. Todo o restante (desktop, overlay, hotkey, inserção de texto, UI, billing, infra) continua avançando com o pipeline de IA por trás de uma interface bem definida e mocks nos testes.

---

## 2. Conta Vercel divergente

- **O que é necessário**: confirmação de qual conta Vercel deve ser usada.
- **Por que**: a diretiva menciona `alanvitoraraujo2a@gmail.com` (mesma conta autenticada no Railway CLI), mas o Vercel CLI local está autenticado como `alanarauj0` — podem ou não ser a mesma pessoa/conta.
- **Onde**: qualquer `vercel deploy`/`vercel link` da landing page (P6).
- **Impacto**: nenhum agora — landing page é a última fase. Só relevante quando chegarmos lá.
- **Como resolver**: confirmar se `alanarauj0` é a conta correta, ou rodar `vercel login` com a conta certa antes do primeiro deploy.
- **Bloqueia o resto do projeto?** Não.

---

## 3. Chave secreta do Stripe ausente

- **O que é necessário**: `STRIPE_SECRET_KEY` (e `STRIPE_WEBHOOK_SECRET` quando o endpoint de webhook existir).
- **Por que**: não está presente no ambiente. Sem ela não consigo consultar a API do Stripe para confirmar valores reais associados aos Price IDs listados na diretiva (`price_1U20zVRCZcRie5rtsld1kEKr` etc.) — e a diretiva proíbe explicitamente inventar valores monetários.
- **Onde**: variável de ambiente do backend de billing.
- **Impacto**: o sistema de entitlements/billing pode ser construído inteiramente em torno dos Price IDs reais fornecidos (a estrutura de tiers está clara: Essencial/Premium/Ultra, mensal/anual), mas a tela de preços não pode exibir valores monetários reais até a chave estar disponível para consulta, e o fluxo de checkout não pode ser testado ponta a ponta.
- **Como resolver**: fornecer a chave (idealmente via secret no Railway) ou os valores exatos de cada price.
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

*Este arquivo é atualizado conforme novas pendências legítimas (que exigem exclusivamente o proprietário) surgirem.*
