# Registro de Decisões Arquiteturais

Formato: contexto → alternativas → decisão → razão → consequência.

---

## D001 — Backend proxy obrigatório para Gemini (nenhuma chamada direta do desktop)

- **Contexto**: a diretiva (§6) proíbe absolutamente qualquer secret no cliente/instalador. O pipeline de voz precisa chamar a Gemini API.
- **Alternativas**: (a) desktop chama Gemini diretamente com a chave embutida; (b) desktop chama um backend próprio, que guarda a chave e faz proxy/orquestração para a Gemini.
- **Decisão**: (b). Um backend mínimo de auth + proxy de IA é parte do caminho crítico do P0, não uma fase posterior de infraestrutura (P3).
- **Razão**: (a) violaria diretamente §6 e tornaria a chave extraível do binário/instalador por qualquer usuário.
- **Consequência**: o "produto funcionando" mínimo já exige: desktop app + backend com endpoint de transcrição/formatação + autenticação básica de dispositivo/usuário. Isso adianta partes de P3 (contas) para dentro do P0.

---

## D002 — Ambiente de desenvolvimento: .NET 8 SDK instalado localmente

- **Contexto**: nenhuma engine de build para Windows nativo estava disponível (sem .NET SDK, sem MSBuild, sem Visual Studio). Node.js e Rust já estavam presentes.
- **Alternativas**: (a) instalar .NET SDK; (b) usar Rust + Tauri com bindings manuais via `windows-rs`; (c) Electron + módulos nativos (ffi-napi/koffi) para Win32.
- **Decisão**: instalado .NET 8 SDK via winget (ação local, reversível, sem custo).
- **Razão**: o app depende pesadamente de APIs Win32 de baixo nível (hotkey global, `SendInput`, UIPI, janelas overlay `WS_EX_NOACTIVATE`/`WS_EX_TRANSPARENT`, clipboard). .NET tem a interoperabilidade Win32 mais madura e documentada dessas três opções, reduzindo risco no primitivo mais arriscado do produto.
- **Consequência**: a escolha final da stack de UI (WPF vs WinUI 3) ainda depende do resultado do spike técnico (ver D003, pendente). Rust permanece disponível como alternativa caso o spike com .NET revele um bloqueio sério.

---

## D003 — Stack final de UI/hotkey/overlay + estratégia de inserção de texto

- **Contexto**: precisávamos validar, antes de comprometer a stack, se é possível (a) registrar um hotkey global, (b) mostrar um overlay always-on-top sem roubar foco da janela ativa, e (c) inserir texto de forma confiável na janela que estava em foco — os três pré-requisitos do fluxo central do produto (§18–22 da diretiva).
- **Método**: spike descartável em `.NET 8 + WPF` (`desktop/spike/HotkeySpike/`), testado com automação real (clique de mouse + `SendInput`) contra Notepad, uma janela isolada do VS Code, uma janela isolada do Brave (Chromium) e o Windows Terminal, no ambiente real do usuário (não headless).
- **Decisão**: `.NET 8 + WPF` para a janela overlay, com interoperabilidade Win32 via P/Invoke para hotkey (`RegisterHotKey`), estilo de janela (`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_LAYERED`) e injeção de texto.
- **Resultados validados**:
  1. **Overlay não rouba foco**: confirmado (janela com os estilos estendidos acima permaneceu visível enquanto `GetForegroundWindow()` continuou apontando para o app do usuário em 100% das amostras, em múltiplos ciclos).
  2. **Bug real encontrado e corrigido**: o struct `INPUT` do Win32 exige que o `union` interno tenha o tamanho do maior membro nativo (`MOUSEINPUT`, 32 bytes), não apenas do `KEYBDINPUT` (24 bytes) — sem isso, `SendInput` falha inteiro com `ERROR_INVALID_PARAMETER`. Tamanho correto do `INPUT` em x64: 40 bytes. Corrigido e confirmado via `sizeof`.
  3. **Digitação caractere-a-caractere via `SendInput` é frágil para blocos de texto**: uma rajada de eventos `KEYEVENTF_UNICODE` sem pausa corrompeu texto com acentuação PT-BR (parte do texto virou caracteres repetidos). Causa raiz não isolada (hipótese plausível: interferência de autocomplete/corretor ortográfico do app de destino) — a decisão de usar colagem via clipboard não depende de confirmar a causa, mas a causa em si fica como investigação em aberto, não como fato estabelecido.
  4. **Colagem via clipboard é o método confiável**: salvar clipboard → definir texto → `Ctrl+V` sintético → restaurar clipboard original. Validado com sucesso e fidelidade total de acentuação em três categorias de app distintas: controle nativo Win32 (Notepad), editor Electron/Monaco (VS Code), conteúdo web Chromium (Brave).
  5. **`RegisterHotKey` + `WM_HOTKEY` validados**: registro de um combo (Ctrl+Alt+Shift+F13) retornou `true` e dois disparos sintéticos do combo foram corretamente recebidos via `WM_HOTKEY` (contagem incrementou de 1 para 2). **Detecção de conflito confirmada**: com uma segunda instância tentando registrar o mesmo combo enquanto a primeira ainda o detém, o registro retornou `false` — exatamente o comportamento que §17 exige detectar e comunicar ao usuário.
  6. **`SetForegroundWindow` chamado por um processo em background é bloqueado pelo Windows** (proteção de foreground-lock) — não é um problema para o produto real (a janela do usuário já está em foreground quando o hotkey é pressionado), mas exigiu clique de mouse sintético para validar o teste de forma automatizada.
- **Windows Terminal — resolvido em retest isolado**: a primeira tentativa reportou `confirmedForeground=False`, mas era instabilidade de timing do harness de teste (cliques disparados em sequência rápida sem assentar), não incompatibilidade real. Isolado com uma pausa adequada, a ativação por clique confirmou foreground corretamente e a colagem via clipboard foi enviada sem erro. Considerado coberto pelo mesmo mecanismo validado nos outros apps; risco específico de terminal (bracketed paste mode alterando o texto colado) não foi descartado e deve ser verificado quando o inserter real for implementado.
- **Limitações conhecidas (não resolvidas neste spike)**:
  - **Janela elevada (UIPI)**: não foi possível automatizar um teste real contra uma janela rodando como administrador, pois isso exigiria interação humana no prompt do UAC (automatizar isso derrotaria o propósito do UIPI). Ficou como item de verificação manual. Consequência arquitetural assumida: o Aionix Scribe **nunca deve rodar elevado** (perderia acesso de injeção a todas as janelas normais), e deve **detectar falha de inserção** (nenhuma mudança confirmável após tentar inserir) e comunicar isso claramente ao usuário em vez de falhar silenciosamente.
- **Consequência para a arquitetura do injetor**: o inserter de texto do Aionix Scribe usará colagem via clipboard como caminho primário (com save/restore do clipboard original), reservando simulação de tecla (`SendInput`) apenas para teclas de controle isoladas (Enter, Backspace, setas) quando necessário para comandos de voz — nunca para o corpo do texto ditado.

---

---

## D004 — Backend: Node.js + TypeScript + Fastify, sem ORM/banco ainda

- **Contexto**: D001 exige um backend que guarde `GEMINI_API_KEY` e faça proxy para a Gemini (nenhuma chamada direta do desktop). Precisava de uma stack rápida de implementar, com bom suporte a deploy no Railway e SDKs maduros para Gemini/Stripe.
- **Decisão**: Node.js + TypeScript + Fastify, sem ORM (Prisma) e sem banco de dados por enquanto.
- **Razão**: o P0 precisa de exatamente um endpoint (`POST /api/transcribe`: áudio → texto formatado), sem nenhum dado a persistir ainda. Adicionar ORM/migrations antes de ter um schema real seria abstração prematura. Node/TS foi escolhido sobre Python/Go por: SDKs de primeira classe para Stripe e Gemini, deploy trivial no Railway (Railpack detecta e builda automaticamente via Nixpacks), e por ser a linguagem mais rápida de iterar para esta equipe de agentes.
- **Validado**: deploy real no Railway (projeto `aionix-scribe`, serviço `aionix-scribe-api`), endpoint `/api/transcribe` testado com áudio PT-BR real (sintetizado via SAPI do Windows) contra o endpoint de produção — pipeline completo áudio→Gemini→texto limpo funcionando, ~4.9s de latência ponta a ponta (quase toda ela é a própria chamada à Gemini, overhead do proxy é desprezível).
- **Consequência**: Postgres/Prisma entram quando histórico (P2) ou entitlements/billing (P3) exigirem persistência real — não antes.

## D005 — Infraestrutura Railway: projeto dedicado, não reaproveitado

- **Contexto**: já existia um projeto Railway chamado "Aionix.Backup" na conta do usuário. Antes de criar infraestrutura nova, o CLAUDE.md (política Remote-First) exige checar duplicação.
- **Descoberta**: "Aionix.Backup" é um produto completamente diferente e não relacionado (backup/storage com OAuth do Google, JWT próprio, Postgres próprio, frontends em `aionix-backup-*.vercel.app`). Confirmado com o usuário que não deve ser tocado.
- **Decisão**: criado projeto Railway novo e dedicado `aionix-scribe`, com serviço `aionix-scribe-api` (Node/TS/Fastify, sem banco por enquanto — ver D004).
- **Nota de segurança**: ao inspecionar "Aionix.Backup" para decidir se era reaproveitável, o comando `railway variables` expôs secrets reais desse outro projeto (JWT_SECRET, GOOGLE_CLIENT_SECRET, senha do Postgres) no output/transcript desta sessão. Não afeta o Aionix Scribe, mas foi registrado como recomendação de rotação em `PENDENCIAS_USUARIO.md` (item 6). Lição incorporada ao CLAUDE.md: preferir inspeção sem exposição de valores (nome do serviço, domínio, endpoint de health) antes de listar variáveis com valores.

---

## D006 — Quota do plano Essencial: 300 min (18.000s) por ciclo mensal

- **Decisão do proprietário (definitiva)**: plano Essencial = 300 minutos (18.000 segundos) de processamento de voz por ciclo mensal, sem rollover. Premium e Ultra permanecem sem franquia mensal, sujeitos apenas a proteção razoável contra abuso/fraude.
- **Implementação**: constante centralizada em `backend/src/config/tiers.ts` (`ESSENCIAL_MONTHLY_QUOTA_SECONDS = 18_000`, com override via env var para recalibração futura sem refactor).
- **Regras de negócio a respeitar quando o sistema de consumo for implementado (P3)**: só contabilizar áudio efetivamente processado; cancelamento antes do processamento não consome quota; falhas técnicas (rede/infra/provedor) não consomem quota; retries idempotentes (nunca descontar duas vezes); reset pelo ciclo real de assinatura (não mês civil); avisos em ~80%/~95%/100%; bloqueio de novos processamentos no Essencial ao atingir 100%, com oferta de upgrade.
- **Consequência**: como ainda não há persistência (D004 adiou banco de dados para quando entitlements/histórico exigirem), a contagem de consumo em si ainda não está implementada — só a constante de configuração. Ver ROADMAP.md P3 para a lista completa de regras a implementar.

## D007 — Conta Vercel confirmada: `alanarauj0` / time `Aionixdev`

- **Decisão do proprietário (definitiva)**: a conta Vercel CLI já autenticada (`alanarauj0`) e o time `Aionixdev` são o destino correto para os workloads web do Aionix Scribe (landing page, P6). Não requer nova confirmação.

## D008 — Code signing do instalador Windows: dispensado nesta etapa

- **Decisão do proprietário (definitiva)**: não adquirir/configurar certificado de assinatura de código agora. Isso não impede considerar o Aionix Scribe concluído nas fases atuais — builds não assinados (com aviso de "editor desconhecido" do SmartScreen) são aceitáveis até segunda ordem.
- **Consequência para o pipeline de build (P5)**: estruturar o processo de build/instalador de forma que a assinatura possa ser adicionada depois sem grande refatoração (ex.: um passo de assinatura opcional e isolado no pipeline), mas sem tratar a ausência de certificado como bloqueio de qualquer fase.

## D009 — Escopo de infraestrutura: exclusivamente Aionix Scribe

- **Decisão do proprietário (definitiva)**: ao operar contas com múltiplos projetos (Railway, Vercel, etc.), listar recursos apenas o necessário para localizar/criar a infraestrutura do Aionix Scribe. Nunca investigar, auditar, modificar ou fazer recomendações sobre outros projetos do proprietário (ex.: Aionix.Backup) — mesmo que algo pareça digno de nota. Ver também a seção "Infraestrutura: política Remote-First" em `CLAUDE.md`.

---

## D010 — App real construído; hotkey com cadeia de fallback automática

- **Contexto**: o app real (`desktop/AionixScribe/`) foi construído reaproveitando os padrões validados no spike (D003), com captura de áudio via NAudio e chamada ao backend real (D004). No primeiro teste ao vivo, o combo padrão escolhido (Ctrl+Alt+Espaço) estava de fato em conflito com outro aplicativo já em uso na máquina do proprietário — a detecção de conflito (validada no spike) funcionou como esperado, mas um único combo fixo sem alternativa deixaria o app inutilizável até o usuário liberar manualmente o atalho.
- **Decisão**: em vez de falhar com um único combo, o app tenta uma lista ordenada de candidatos (`Ctrl+Alt+Espaço` → `Ctrl+Alt+Shift+Espaço` → `Ctrl+Win+Espaço` → `Ctrl+Alt+Shift+D`) e usa o primeiro que registrar com sucesso, avisando o usuário via balão de notificação qual foi ativado.
- **Resultado**: no teste real, o segundo candidato (`Ctrl+Alt+Shift+Espaço`) registrou com sucesso, e o fluxo completo (hotkey → gravação real via headset → backend em produção → Gemini → texto limpo → colagem no campo em foco) funcionou de primeira, validado pelo proprietário com a própria voz.
- **Consequência**: uma UI para configurar/exibir o atalho manualmente continua necessária (P1/P2) — o mecanismo atual de fallback é resiliente, mas o usuário só descobre qual atalho está ativo por um balão de notificação ou um arquivo de log, o que não é uma solução de produto aceitável a longo prazo.
- **Gap identificado durante o teste**: o ambiente de desenvolvimento inicial não tinha nenhum dispositivo de microfone (`WaveInEvent.DeviceCount == 0`) até o proprietário conectar um headset — isso expôs que o app não tinha tratamento para "sem microfone" (§29). Adicionado tratamento básico (try/catch com aviso ao usuário), mas ainda não testado a fundo (ver ROADMAP.md P1).

---

## D011 — Bug real em produção: Gemini retornava conteúdo vazio para áudio curto/ambíguo

- **Contexto**: durante teste ao vivo do app real (D010), tanto uma gravação de teste automatizada quanto uma gravação manual do proprietário produziram erro 502 do backend ("Resposta da Gemini sem texto utilizável"). Os logs de produção (`railway logs`) mostraram a causa exata: `"candidates":[{"content":{},"finishReason":"STOP"}]` — a Gemini retornava sucesso (`finishReason: STOP`) mas com `content` vazio, tendo gasto todo o orçamento de tokens de saída em `thoughtsTokenCount` (raciocínio interno invisível) sem nunca emitir o texto final.
- **Diagnóstico**: o modelo (`gemini-3.6-flash`, via alias `gemini-flash-latest`) é um modelo com "thinking" habilitado por padrão sem limite, e para uma tarefa direta de transcrição/limpeza (não uma tarefa de raciocínio complexo), esse "pensar" podia consumir o orçamento inteiro antes de produzir a resposta, especialmente em áudio curto ou ambíguo.
- **Decisão**: adicionar `generationConfig.thinkingConfig.thinkingBudget: 256` na chamada à Gemini (`backend/src/gemini.ts`). Testado empiricamente: `thinkingBudget: 0` é rejeitado pela API (`400 INVALID_ARGUMENT` — este modelo não permite desabilitar thinking completamente), mas um orçamento pequeno e positivo (128–512) funciona e resolve o problema sem prejudicar a qualidade da transcrição.
- **Defesa adicional**: mesmo com o orçamento limitado, se a Gemini ainda retornar `finishReason: STOP` sem texto, o backend agora trata isso como "nenhuma fala detectada" (resultado `text: ""`, HTTP 200) em vez de erro 502 — esse é um resultado plausível e não deveria disparar o mecanismo de retry/preservação de áudio do app (que é para falhas técnicas reais, não para "o usuário não falou nada compreensível"). Qualquer outro `finishReason` (SAFETY, RECITATION, etc.) sem texto continua sendo tratado como falha real.
- **Validado em produção**: a mesma gravação que causava o 502 foi reprocessada com sucesso após o fix e deploy, retornando uma transcrição real e coerente.

---

## D012 — UI de configuração do atalho + tratamento específico de "sem microfone"

- **Contexto**: D010 identificou que o usuário só sabia qual atalho estava ativo por um balão de notificação (fácil de perder). Construída `SettingsWindow` (WPF) acessível pelo menu da bandeja: mostra o atalho atual, permite capturar um novo ao vivo (pressione as teclas, exige pelo menos um modificador), valida conflito antes de aplicar (mantém o atalho anterior funcionando se o novo falhar), e persiste a escolha em `%LOCALAPPDATA%\AionixScribe\settings.json`. "Restaurar padrão" volta para a cadeia de fallback automática.
- **Validado ao vivo**: proprietário testou capturar e aplicar um novo atalho pela UI, funcionou.
- **Achado paralelo real**: durante o teste, o headset do proprietário desconectou/entrou em suspensão (comportamento comum de headsets sem fio), e o app reportava um erro genérico de microfone. Confirmado com `WaveInEvent.DeviceCount == 0` que era uma ausência real de dispositivo, não um bug. Adicionada `NoMicrophoneException` com mensagem específica e acionável ("verifique se está conectado, ligado e não em suspensão") em vez de repassar a mensagem técnica crua da exceção do NAudio.
- **Consequência**: seleção manual entre múltiplos microfones (quando há mais de um dispositivo) continua não implementada — hoje o app sempre usa o dispositivo padrão do Windows (índice 0 do NAudio). Fica para quando isso for um caso real relatado, não antes.

---

## D013 — Stopgap de autenticação no `/api/transcribe` (header compartilhado)

- **Contexto**: `/api/transcribe` estava em produção sem nenhuma verificação — qualquer pessoa que descobrisse a URL pública (já hardcoded em `BackendClient.cs`, D010) podia queimar a cota da chave `GEMINI_API_KEY` sem limite. Contas/autenticação de usuário real é P3 e ainda não existe; esperar por isso deixaria o endpoint exposto por semanas.
- **Decisão**: adicionado um shared secret (`DESKTOP_SHARED_SECRET`, 32 bytes aleatórios) verificado no header `X-App-Secret`. O backend rejeita com 401 qualquer chamada sem o header correto. O mesmo valor está embutido como constante em `BackendClient.cs` e configurado como variável de ambiente no Railway (nunca impresso em log/commit/resposta).
- **Limitação conhecida e aceita**: isso não é autenticação de usuário/dispositivo — é um segredo de app cliente, extraível por quem descompilar o binário do Aionix Scribe. Eleva a barreira de "qualquer um que ache a URL via tráfego de rede" para "quem inspecionar o binário distribuído", o que é suficiente enquanto o app não tem distribuição pública em massa (ainda em P0-P2, sem instalador/P5). Não substitui contas reais (P3) — este item deve ser revisitado e removido/complementado quando entitlements por usuário existirem.
- **Validado**: chamada sem header → `401`; chamada com header correto → passa da autenticação (erro 502 subsequente foi por áudio inválido de teste, não por auth). Desktop recompilado e relançado com o header.

*Novas decisões de impacto significativo serão adicionadas a este arquivo conforme o projeto avança.*
