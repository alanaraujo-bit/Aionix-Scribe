# Roadmap — Aionix Scribe

Hierarquia de valor (não é uma sequência rígida de datas). Cada fase só é "concluída" quando passa pelos quality gates do CLAUDE.md/diretiva mestre (funcional, testado, sem placeholder, sem mock em produção, sem secret exposto).

## P0 — Experiência essencial (capturar → compreender → formatar → inserir)
**Status: fluxo essencial funcionando ponta a ponta, validado com voz humana real em 2026-08-08**

O app real (`desktop/AionixScribe/`) existe e foi testado ao vivo pelo proprietário: atalho global → gravação real via microfone (NAudio) → backend em produção (Railway) → Gemini real → texto limpo → colagem via clipboard no campo em foco. Funcionou de primeira no teste manual.

- [x] Spike técnico (hotkey/overlay/injeção) — ver DECISIONS.md D003
- [x] Decisão de stack (D003): .NET 8 + WPF + Win32 P/Invoke; inserção via clipboard-paste
- [x] Captura de áudio real via NAudio (`AudioRecorder.cs`, 16kHz mono PCM) — dispositivo padrão do sistema; seleção manual de microfone ainda não existe (P2/§29)
- [x] Backend mínimo: proxy Gemini em produção (Railway, `aionix-scribe-api`). **Falta**: autenticação de dispositivo/usuário — endpoint ainda aceita chamadas sem auth; aceitável enquanto só o app oficial o chama, bloqueante antes de expor a usuários externos reais (P3)
- [x] Pipeline de limpeza/formatação validado com áudio sintetizado E com fala humana real ao vivo. Golden dataset (§25) ainda não construído — poucos casos manuais não são cobertura sistemática.
- [x] Inserção via clipboard-paste (`ClipboardInjector.cs`) usando `System.Windows.Clipboard.GetDataObject()/SetDataObject()` — salva e restaura **todos os formatos presentes**, não só texto (resolve o gap identificado no spike)
- [x] `RegisterHotKey` com **fallback automático de conflito**: o primeiro combo padrão (Ctrl+Alt+Espaço) já estava em uso na máquina real do proprietário no primeiro teste — o app tenta uma cadeia de candidatos e usa o primeiro livre (atualmente resolveu para Ctrl+Alt+Shift+Espaço). UI para escolher/exibir o atalho manualmente ainda não existe (P2).
- [x] Overlay com estados (ouvindo/processando/concluído/erro/cancelado) — versão visual mínima funcional; refinamento visual (§18, design system) é P2.
- [ ] Tratamento robusto de "sem microfone disponível" — implementado de forma básica (try/catch com aviso), não testado em profundidade (só descoberto porque o ambiente de teste inicial não tinha microfone)
- [ ] Push-to-talk (segurar/soltar) — versão atual é apenas toggle (aperta pra começar, aperta de novo pra parar), porque reaproveita `RegisterHotKey` (que não avisa quando a tecla é solta). Push-to-talk exigiria um low-level keyboard hook (`WH_KEYBOARD_LL`) à parte — ver ROADMAP P1.

## P1 — Confiabilidade
- [x] Tray icon básico (ícone genérico do sistema — ícone de marca é P2) com "Sair"; detecção de conflito de hotkey com fallback automático já implementado (ver DECISIONS.md D010)
- [ ] Execução em background leve, medir idle CPU/RAM de verdade (ainda não medido)
- [ ] UI para configurar/exibir o atalho manualmente (hoje só dá pra saber qual foi escolhido via log/balão de notificação)
- [ ] Push-to-talk (segurar/soltar) via low-level keyboard hook, como alternativa ao toggle atual
- [ ] Recuperação de falhas: rede cai, API erra, crash, timeout — preservar áudio para retry (hoje, se `TranscribeAsync` falhar, o áudio gravado é descartado — perda de trabalho do usuário, viola §23)
- [x] Tratamento de estados impossíveis: dupla ativação durante processamento é ignorada (`AppState.Processing` bloqueia novo trigger); handler global de exceção não derruba o app
- [ ] Seleção manual de microfone e tratamento robusto de "nenhum microfone disponível" (implementado de forma mínima, não testado a fundo)

## P2 — Produto
- [ ] Painel principal (status, atalho ativo, últimas transcrições, atividade)
- [ ] Histórico (visualizar, copiar, excluir, pesquisar)
- [ ] Configurações (conta, áudio, atalhos, idioma, privacidade, inicialização)
- [ ] Onboarding com primeira transcrição guiada
- [ ] Temas Light/Dark completos (não é inversão de cor)

## P3 — Plataforma SaaS
- [ ] Contas e autenticação
- [ ] Entitlements (Essential/Premium/Ultra) como fonte única de verdade
- [ ] Stripe: checkout, customer portal, upgrade/downgrade/cancelamento
- [ ] Webhook Stripe: implementar endpoint público, deployar, registrar no Stripe TEST, obter/configurar `STRIPE_WEBHOOK_SECRET` no Railway (secret, nunca em código/git/logs), validar assinatura, idempotência, e os eventos de subscription criada/atualizada/cancelada/renovada/pagamento falhado com sincronização de entitlement. Tarefa de engenharia normal — não é pendência do proprietário.
- [ ] Quota do plano Essencial — **decisão comercial definitiva (ver DECISIONS.md D006)**: 300 minutos (18.000s) por ciclo mensal, armazenados internamente em segundos, exibidos ao usuário em minutos/horas. Regras a implementar:
  - contabilizar somente áudio efetivamente processado (cancelamento antes do processamento não consome quota);
  - falha técnica (rede, infra, provedor) não consome quota; retries devem ser idempotentes, nunca descontando duas vezes o mesmo processamento;
  - reset baseado no ciclo real de assinatura (não mês civil), sem rollover;
  - UI mostra: consumido, saldo restante, percentual, data de renovação;
  - avisos em ~80%, ~95% e 100% de uso; ao atingir 100%, bloquear novos processamentos no Essencial e oferecer upgrade;
  - Premium/Ultra sem franquia mensal, sujeitos apenas a proteção razoável contra abuso/fraude;
  - manter métricas de uso para recalibrar o limite futuramente com base em custo real;
  - valor `18000` centralizado em uma única constante de configuração (`backend/src/config/tiers.ts`), nunca espalhado pelo código.

## P4 — Inteligência avançada
- [ ] Vocabulário personalizado (nomes, termos técnicos)
- [ ] Comandos de voz (quebra de linha, novo parágrafo, cancelar, etc.)
- [ ] Reescrita por IA sobre texto selecionado (melhorar/resumir/traduzir/tom)
- [ ] Tradução simultânea (plano Ultra)

## P5 — Distribuição
- [ ] Instalador Windows real (registro, ícone, atalhos, desinstalação)
- [ ] Atualização automática
- [ ] Assinatura de código (certificado: ver PENDENCIAS_USUARIO.md #5)

## P6 — Comercial
- [ ] Landing page (somente após P0–P2 maduros)
- [ ] Pricing page com valores reais do Stripe
- [ ] Download/release flow

---

## Pesquisa competitiva (Wispr Flow) — matriz de benchmark

Pass timeboxed via web (site oficial + reviews de terceiros, ago/2026). Não testamos o produto real — dados de segunda mão, marcados como tal. Revisitar com medições próprias conforme o produto evolui.

| Dimensão | Wispr Flow (observado/relatado) | Aionix Scribe | Evidência |
|---|---|---|---|
| ativação | hotkey global, funciona em qualquer app, sem plugin | hotkey global configurável (a definir no spike) | site oficial |
| feedback de gravação | overlay mostra "filler identified/correction identified/repetition identified" em tempo real | overlay com estados claros; ver §18 da diretiva | site oficial |
| formatação | remove filler words, corrige pontuação/estrutura; "reads like you wrote it" | mesmo objetivo — pipeline com regra explícita de não alterar intenção | site oficial |
| vocabulário/idiomas | vocabulário customizado; 100+ idiomas com auto-detecção | vocabulário customizado (P4); PT-BR como excelência primária, expansão gradual | site oficial |
| onboarding | "polido", checklist de ações cross-app | onboarding com 1ª transcrição guiada (ver ROADMAP P2) | reviews de terceiros |
| **confiabilidade (ponto fraco real)** | 75+ incidentes em 6 meses no caminho de transcrição; queda perceptível de qualidade após período de trial ("funciona 60% do tempo" após pagar); ponto único de falha no servidor de transcrição compartilhado | **diferencial-alvo**: preservar áudio localmente para retry (§23), degradar com transparência em vez de falhar silenciosamente, nunca depender de um único provedor sem fallback de UX | reviews/Trustpilot (2.7/5), getvoibe.com |
| **desempenho no Windows (ponto fraco real)** | uso alto de CPU/RAM relatado; "uneven Windows reliability"; startup lento (8–10s) | **diferencial-alvo**: cold start rápido, idle leve — ver P0/P1 e budgets de performance (§58) | reviews de terceiros |
| privacidade | modo privacidade anunciado, mas usuários relatam preocupações | minimizar retenção/coleta desde o design (§42), comunicar claramente | site oficial + reviews |
| pricing | Free (2k palavras/semana), Pro (ilimitado), Teams, Enterprise | Essencial/Premium/Ultra, mensal/anual, Price IDs reais do Stripe (§47–52) | site oficial |

**Conclusão da pesquisa**: o gap de oportunidade real não é de features (Wispr já cobre bem o essencial) — é de **confiabilidade percebida ao longo do tempo** e **leveza no Windows**. Esses dois pontos devem ser tratados como requisitos de primeira classe do P0/P1, não polimento tardio.
