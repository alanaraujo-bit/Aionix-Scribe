# Roadmap — Aionix Scribe

Hierarquia de valor (não é uma sequência rígida de datas). Cada fase só é "concluída" quando passa pelos quality gates do CLAUDE.md/diretiva mestre (funcional, testado, sem placeholder, sem mock em produção, sem secret exposto).

## P0 — Experiência essencial (capturar → compreender → formatar → inserir)
**Status: fluxo essencial funcionando ponta a ponta, validado com voz humana real em 2026-08-08**

O app real (`desktop/AionixScribe/`) existe e foi testado ao vivo pelo proprietário: atalho global → gravação real via microfone (NAudio) → backend em produção (Railway) → Gemini real → texto limpo → colagem via clipboard no campo em foco. Funcionou de primeira no teste manual.

- [x] Spike técnico (hotkey/overlay/injeção) — ver DECISIONS.md D003
- [x] Decisão de stack (D003): .NET 8 + WPF + Win32 P/Invoke; inserção via clipboard-paste
- [x] Captura de áudio real via NAudio (`AudioRecorder.cs`, 16kHz mono PCM) — dispositivo padrão do sistema; seleção manual de microfone ainda não existe (P2/§29)
- [x] Backend mínimo: proxy Gemini em produção (Railway, `aionix-scribe-api`), agora com stopgap de autenticação (header `X-App-Secret`, ver DECISIONS.md D013) — endpoint público não aceita mais chamadas sem o segredo compartilhado. Autenticação de usuário/dispositivo real continua P3; o stopgap é suficiente até lá.
- [x] Pipeline de limpeza/formatação validado com áudio sintetizado E com fala humana real ao vivo. Golden dataset (§25) ainda não construído — poucos casos manuais não são cobertura sistemática.
- [x] Inserção via clipboard-paste (`ClipboardInjector.cs`) usando `System.Windows.Clipboard.GetDataObject()/SetDataObject()` — salva e restaura **todos os formatos presentes**, não só texto (resolve o gap identificado no spike). Corrigido bug de restauração silenciosa: `GetDataObject()` retorna um wrapper vivo sobre a fonte original (delayed-rendering), então agora fazemos deep-copy de cada formato para um `DataObject` novo antes de sobrescrever o clipboard. Validado com teste automatizado isolado (imagem em memória sobrevive ao ciclo salvar→sobrescrever→restaurar, 64x64 íntegro) — esse teste só prova que o mecanismo funciona para dados já em memória no próprio processo; **não prova** o caso real de delayed-rendering de um app externo (ex.: Ferramenta de Captura), que só um teste ao vivo do proprietário confirma
- [x] `RegisterHotKey` com **fallback automático de conflito**: o primeiro combo padrão (Ctrl+Alt+Espaço) já estava em uso na máquina real do proprietário no primeiro teste — o app tenta uma cadeia de candidatos e usa o primeiro livre (atualmente resolveu para Ctrl+Alt+Shift+Espaço). UI para escolher/exibir o atalho manualmente ainda não existe (P2).
- [x] Overlay com estados (ouvindo/processando/concluído/erro/cancelado) — versão visual mínima funcional; refinamento visual (§18, design system) é P2.
- [x] Tratamento robusto de "sem microfone disponível" — `NoMicrophoneException` com mensagem específica e acionável; validado ao vivo com um caso real (headset sem fio desconectando/dormindo no meio do uso, não hipotético). Ver P1 abaixo e DECISIONS.md D012. Seleção manual entre múltiplos microfones ainda não existe (só usa o dispositivo padrão do Windows).
- [x] Push-to-talk (segurar/soltar) implementado como alternativa ao toggle — ver P1 abaixo e DECISIONS.md D014.

## P1 — Confiabilidade
- [x] Tray icon básico (ícone genérico do sistema — ícone de marca é P2) com "Sair"; detecção de conflito de hotkey com fallback automático (ver DECISIONS.md D010)
- [x] Idle CPU/RAM medidos de verdade: 133MB working set, 0% CPU idle (ver tabela de benchmark abaixo). Cold start ainda não medido.
- [x] UI para configurar/exibir o atalho manualmente — `SettingsWindow` acessível pelo menu da bandeja, captura o atalho ao vivo (pressione as teclas), valida conflito antes de trocar, persiste em `%LOCALAPPDATA%\AionixScribe\settings.json`, com opção de restaurar o padrão automático. Validado ao vivo pelo proprietário.
- [x] Push-to-talk (segurar/soltar) via low-level keyboard hook (`PushToTalkHook.cs`, `WH_KEYBOARD_LL`), como alternativa ao toggle atual — modo escolhido em Configurações ("Alternar"/"Segurar para falar"), persistido em `settings.json` com retrocompatibilidade (arquivo antigo sem o campo cai em Toggle). Validado ao vivo pelo proprietário: segurar/soltar grava só durante o período pressionado; alternar entre os dois modos funciona. Ver DECISIONS.md D014.
- [x] Recuperação de falhas: falha técnica real → retry automático → preservação do áudio + reprocessamento manual pela bandeja (ver DECISIONS.md D010/D011, validado ao vivo com um bug real de produção)
- [x] Tratamento de estados impossíveis: dupla ativação durante processamento é ignorada (`AppState.Processing` bloqueia novo trigger); handler global de exceção não derruba o app
- [x] Tratamento de "nenhum microfone disponível": exceção específica (`NoMicrophoneException`) com mensagem clara em vez de erro técnico cru — descoberto e validado ao vivo (o headset do proprietário desconectou/dormiu no meio do teste, cenário real, não hipotético). Seleção manual entre múltiplos microfones ainda não existe (só usa o dispositivo padrão do Windows).

## P2 — Produto
**Status: completo e validado ao vivo (2026-08-08)**, exceto Configurações → Conta/Idioma, propositalmente adiadas (dependem de P3/multi-idioma inexistentes).

- [x] Painel principal (`MainPanelWindow`): status do microfone (detecta ausência em tempo real), atalho ativo, últimas 5 transcrições, atalhos para Histórico/Configurações. Aberto por duplo-clique na bandeja ou menu "Abrir Aionix Scribe". Validado ao vivo.
- [x] Histórico: `HistoryWindow` acessível pela bandeja, lista últimos ditados (até 200, `%LOCALAPPDATA%\AionixScribe\history.json`), copiar/excluir/**limpar tudo** (com confirmação), local de armazenamento documentado na própria janela (§42 minimização/transparência de retenção). Validado ao vivo. Pesquisa/filtro ainda não existe (só relevante quando o histórico crescer de verdade).
- [x] Configurações → Áudio: seleção de microfone (`AudioSettings.cs`, `%LOCALAPPDATA%\AionixScribe\audio-settings.json`) — lista os dispositivos de entrada reais via NAudio, com "Padrão do sistema" como opção; se o dispositivo salvo for removido/desconectado, cai para o padrão automaticamente em vez de falhar. Validado ao vivo.
- [x] Configurações → Inicialização: "Iniciar com o Windows automaticamente" via `HKCU\...\Run` (`StartupSettings.cs`) — funciona mesmo sem instalador, rodando direto do `bin/Release`. Validado ao vivo.
- [x] Configurações → Privacidade: texto real (não placeholder) sobre o que acontece com o áudio (processado em memória no backend, nunca salvo lá) e onde os dados locais ficam, com botão "Abrir pasta de dados". Validado ao vivo.
- [ ] Configurações → Conta / Idioma — propositalmente **não construídas ainda**: dependem de P3 (contas/auth) e suporte multi-idioma real, que não existem; adicionar agora seria UI sem funcionalidade real por trás (proibido pelas regras do projeto).
- [x] Paleta de cores extraída para recursos nomeados compartilhados (base do item de tema Light/Dark abaixo) — as 5 janelas (overlay/histórico/painel/configurações/onboarding) usam os mesmos brushes em vez de hex duplicado.
- [x] Onboarding com primeira transcrição guiada (`OnboardingWindow`): abre automaticamente só na primeira vez que o app roda, mostra o atalho ativo de verdade, aguarda a primeira transcrição real bem-sucedida (evento `App.DictationSucceeded`) e muda para uma tela de confirmação; cobre também o caso de nenhum atalho ter conseguido registrar (direciona pra Configurações). "Pular"/"Concluir"/fechar pelo X marcam como visto (`onboarding.json`), nunca reaparece depois. Validado ao vivo.
- [x] Temas Light/Dark completos (`Theme.Dark.xaml`/`Theme.Light.xaml`) — paleta clara construída como sistema próprio (elevação, contraste calibrado por WCAG, hierarquia de texto e identidade dos botões preservadas, não uma inversão mecânica de cor; ver DECISIONS.md D017 pros valores e o porquê de cada um). Preferência Sistema/Claro/Escuro em Configurações, troca em tempo real via `DynamicResource` sem reiniciar o app; modo Sistema acompanha o Windows (`AppsUseLightTheme` no registro) inclusive em tempo real via `SystemEvents.UserPreferenceChanged`. Validado ao vivo pelo proprietário: troca em tempo real entre os temas sem reiniciar, tema claro legível, tema escuro idêntico ao anterior.

## P3 — Plataforma SaaS

**Arquitetura decidida em DECISIONS.md D018** (Postgres+Drizzle no Railway, OAuth2/PKCE com navegador do sistema + Auth0, metering com reserva-e-libera idempotente). Duas pendências reais do proprietário bloqueiam partes específicas (ver `PENDENCIAS_USUARIO.md`: política de trial, aprovação do Auth0) — o restante avança em paralelo.

- [x] **Passo 0** — Postgres provisionado no Railway (projeto `aionix-scribe`), Drizzle ORM, migração inicial (5 tabelas: `users`/`subscriptions`/`usage_periods`/`usage_events`/`webhook_events` em `backend/src/db/schema.ts`), `GET /health/db`. Migração roda via `preDeployCommand` do Railway (rede privada, nunca da máquina local). Validado contra produção real: `/health` e `/health/db` respondendo, `/api/transcribe` sem regressão (401 sem `X-App-Secret`, como antes).
- [ ] **Passo 1** — Login real via OAuth2/PKCE (navegador padrão + loopback local), `GET /api/me` (provisiona usuário lazy), UI de login no cliente (DPAPI para o refresh token). Bloqueado pela aprovação do provedor de identidade (pendência #2).
- [ ] **Passo 2** — Entitlements (`GET /api/me/entitlement`) como fonte única de verdade; UI exibe tier/quota reais. Bloqueado parcialmente pela política de trial (pendência #1) para o caso "autenticado sem assinatura".
- [ ] **Passo 3** — Cutover de `/api/transcribe` do stopgap `X-App-Secret` (D013) para `Bearer`, **em dois deploys separados** (aceitar os dois em paralelo primeiro, remover o antigo só depois de validado ao vivo) — nunca deixar o app sem nenhum caminho de ditado funcionando.
- [ ] **Passo 4** — Metering completo (reserva/liberação atômica, idempotência via `X-Recording-Id` mintado no cliente, reaper de reservas órfãs) + as regras de negócio do D006 abaixo + **classificação de erro em 3 categorias no cliente** (sucesso / falha técnica retentável / recusa determinística como 402 — hoje `BackendClient` trata tudo não-2xx como problema de conexão, o que faria um usuário sem cota ouvir que a internet está com problema).
- [ ] **Passo 5** — Stripe: checkout, customer portal, upgrade/downgrade/cancelamento, webhook (`STRIPE_WEBHOOK_SECRET` no Railway, corpo cru pra verificação de assinatura, idempotência via `webhook_events`, eventos de subscription criada/atualizada/cancelada/renovada/pagamento falhado). Tarefa de engenharia normal — não é pendência do proprietário.
- [ ] **Passo 6** — Teto anti-abuso de Premium/Ultra (constante nova em `tiers.ts`, valor exato é uma decisão do proprietário — não bloqueia o resto) + polimento dos avisos de quota na UI.

**Regras de negócio da quota Essencial (decisão comercial definitiva, DECISIONS.md D006)**: 300 minutos (18.000s) por ciclo mensal, em segundos internamente, exibido em minutos/horas ao usuário.
- contabilizar somente áudio efetivamente processado (cancelamento antes do processamento não consome; "sem fala detectada" **consome**, porque o áudio foi enviado e processado pela Gemini — custo real incorrido, ver nota em PENDENCIAS_USUARIO.md);
- falha técnica (rede, infra, provedor) não consome quota; retries idempotentes via `X-Recording-Id`, nunca descontando duas vezes o mesmo processamento, mesmo em reprocessamento manual horas depois;
- reset baseado no ciclo real de assinatura (não mês civil), sem rollover;
- UI mostra: consumido, saldo restante, percentual, data de renovação;
- avisos em ~80%, ~95% e 100% de uso; ao atingir 100%, bloquear novos processamentos no Essencial e oferecer upgrade;
- Premium/Ultra sem franquia mensal, sujeitos apenas a proteção razoável contra abuso/fraude (teto diário, Passo 6);
- manter métricas de uso (tokens/modelo/duração, nunca o texto transcrito) para recalibrar o limite futuramente com base em custo real.

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

## Benchmark de performance — medições reais (Aionix Scribe)

| Métrica | Valor medido | Data | Método |
|---|---|---|---|
| RAM idle (working set) | 133 MB | 2026-08-08 | `Get-Process` após ~15s de app aberto sem gravar, build Release não self-contained/não trimmed |
| RAM idle (private memory) | 60 MB | 2026-08-08 | idem |
| CPU idle (janela de 10s) | 0% | 2026-08-08 | idem |
| Threads | 21 | 2026-08-08 | idem |
| Handles | 830 | 2026-08-08 | idem |
| Latência ponta a ponta (fala→texto inserido) | ~2.6–5s | 2026-08-08 | testes reais via `/api/transcribe` em produção, várias amostras (ver DECISIONS.md D004, D011) |
| Cold start do app | não medido ainda | — | pendente |

**Contexto**: 133MB de working set é uma baseline honesta para uma app WPF não otimizada (framework-dependent, sem trimming/ReadyToRun/self-contained ainda). Não é ruim comparado a apps Electron (que Wispr Flow provavelmente usa, dado o relato de alto uso de RAM/CPU nas reviews), mas há espaço claro de otimização antes de declarar "leveza" como diferencial vencido — não apenas presumido. Revisitar depois de: (1) medir cold start, (2) testar build self-contained/trimmed, (3) confirmar que NAudio não mantém buffers alocados fora de uma gravação ativa.
