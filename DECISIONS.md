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

---

## D014 — Push-to-talk via low-level keyboard hook (`WH_KEYBOARD_LL`)

- **Contexto**: o modo toggle (`RegisterHotKey`) não avisa quando a tecla é solta, então push-to-talk (segurar para falar, soltar para parar) exige monitorar eventos de tecla diretamente — gap identificado desde o spike (D003) e citado em D010.
- **Decisão**: `PushToTalkHook.cs` instala um hook `WH_KEYBOARD_LL` global, rastreia o estado de teclas pressionadas por conta própria (`HashSet<int>` de VKs, checando os dois lados de cada modificador — o hook só entrega `VK_LCONTROL`/`VK_RCONTROL` etc., nunca o genérico), dispara `Pressed`/`Released` e suprime o evento nativo enquanto o combo está ativo (mesmo comportamento de "não vaza pro app em foco" que `RegisterHotKey` já dava no toggle). O modo (Toggle/PushToTalk) é escolhido em `SettingsWindow` e persistido em `settings.json`, com retrocompatibilidade: arquivo antigo sem o campo carrega como Toggle.
- **Padrão de troca segura**: `App.RegisterCombo` sempre constrói o novo mecanismo (HotkeyManager ou PushToTalkHook) antes de descartar o atual — se a criação falhar (conflito ou falha ao instalar o hook), o mecanismo anterior continua funcionando, mesmo princípio já usado em `TryChangeHotkey` (D012).
- **Limitações conhecidas e aceitas**: combos interceptados pelo próprio Windows antes de qualquer hook em user-mode (Alt+Tab, Ctrl+Alt+Del, Win+L) não podem ser usados como push-to-talk — limitação do SO, não do código. A ordem de pressão importa (modificadores antes da tecla principal), espelhando o comportamento que o toggle já tinha.
- **Validado ao vivo pelo proprietário**: segurar/soltar grava apenas durante o período pressionado; alternância entre os dois modos funciona sem deixar o app sem nenhum mecanismo de ativação registrado.

---

## D015 — Configurações reais (áudio, inicialização, privacidade) + extração de tema

- **Contexto**: a seção "Configurações" só tinha o atalho (D012). "Conta" e "Idioma" foram deliberadamente adiadas — construí-las agora seria UI sem funcionalidade real por trás (dependem de P3/multi-idioma), o que a diretiva mestre proíbe explicitamente. Só entraram seções com funcionalidade real disponível hoje.
- **Decisão**: três seções novas, todas reais:
  - **Áudio**: `AudioSettings.cs` persiste o índice do microfone escolhido; `AudioRecorder` usa `WaveInEvent.DeviceNumber`, com fallback automático pro dispositivo padrão se o índice salvo não existir mais (dispositivo removido/desconectado) — mesma filosofia defensiva de D012.
  - **Inicialização**: `StartupSettings.cs` liga/desliga `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, funciona mesmo sem instalador (P5). Compara o caminho gravado com o caminho atual do executável para não mostrar "ativado" depois do app ser movido.
  - **Privacidade**: texto verificado contra o código real do backend (áudio processado em memória, nunca persistido em disco/banco) e lista onde os dados locais ficam, com botão para abrir a pasta.
- **Refactor acompanhante**: `Theme.xaml` consolida a paleta de cores (antes hex duplicado em 4 arquivos XAML) em brushes nomeados na `Application.Resources`. Puramente mecânico — sem mudança visual — mas necessário antes de um tema Light/Dark real (ainda em aberto) para não multiplicar a duplicação.
- **Validado**: build limpo, todas as chaves `StaticResource` conferidas manualmente contra as definidas em `Theme.xaml` (sem órfãs), app reaberto sem crash. Teste ao vivo confirmado pelo proprietário: as três seções novas funcionam e nenhuma janela existente mudou visualmente após o refactor de tema.

---

## D016 — Onboarding guiado até a primeira transcrição real

- **Contexto**: benchmark competitivo (Wispr Flow) apontou onboarding "polido" como ponto forte deles; o Aionix Scribe não tinha nenhum até agora — usuário novo só descobria o atalho por um balão de notificação.
- **Decisão**: `OnboardingWindow` abre automaticamente só na primeira execução (`OnboardingSettings`/`onboarding.json`), mostra o atalho ativo de verdade (não texto fixo) e espera a primeira transcrição real bem-sucedida — sinalizada por um evento novo, `App.DictationSucceeded`, disparado só quando texto de verdade foi inserido (não para "nenhuma fala detectada"). Ao detectar sucesso, a janela troca para uma tela de confirmação. Não é modal (`Show()`, não `ShowDialog()`) — o usuário pode ignorá-la e usar o app normalmente.
- **Caso de borda coberto**: se todos os candidatos de atalho conflitarem e nenhum registrar (`HasActiveHotkey == false`, novo em `App`), a janela troca a mensagem para direcionar à tela de Configurações em vez de mostrar um atalho inexistente.
- **Fechar sempre marca como visto**: "Pular", "Concluir" e o X da janela convergem no mesmo `Closed` → `OnboardingSettings.MarkCompleted()` — decisão deliberada para não forçar ninguém a completar o fluxo, mas também não reaparecer a cada abertura do app só porque o usuário nunca clicou em nada.
- **Validado ao vivo pelo proprietário**: janela abriu na primeira execução real, mudou para o estado de sucesso após um ditado real, e não reapareceu numa reabertura seguinte do app.

---

## D017 — Tema Light/Dark completo (último item de P2)

- **Contexto**: §36 da diretiva mestre proíbe explicitamente tratar Light Mode como inversão mecânica do Dark Mode. Esse era o último item aberto do P2; P6 (landing page) está gated em "P0-P2 maduros" no roadmap.
- **Decisão**: `Theme.Dark.xaml` preserva exatamente os valores já validados ao vivo (nenhuma mudança); `Theme.Light.xaml` é uma paleta desenhada com critérios próprios, documentados inline no arquivo:
  - Elevação de superfície sobe na mesma direção (card mais claro que a janela) a partir de um ponto de partida diferente — card vai a branco puro em vez de "mais um degrau de cinza".
  - Cores de estado (sucesso/erro/aviso) não são os mesmos tons pastel do escuro — viraram versões mais saturadas/escuras, calibradas por contraste (WCAG) para igualar ou superar o contraste que a versão escura tem no fundo escuro.
  - Hierarquia de texto (muted mais proeminente que footnote) preservada como *relação*, não como valor — no escuro "mais proeminente" = mais claro, no claro = mais escuro.
  - Botões continuam um "chip" escuro preenchido com texto claro nos dois temas — decisão deliberada de manter isso como elemento de identidade, não uma sobra do modo escuro.
- **Troca em tempo real**: todas as 5 janelas passaram de `StaticResource` para `DynamicResource`; `App.ApplyTheme()` troca `Application.Resources.MergedDictionaries` (não recria janelas). Modo "Sistema" lê `HKCU\...\Themes\Personalize\AppsUseLightTheme` e observa `SystemEvents.UserPreferenceChanged` pra acompanhar uma troca de tema do Windows enquanto o app está aberto.
- **Corrigido no mesmo lote** (achado pelo Advisor antes de começar): `OverlayWindow.SetState`/`MainPanelWindow.Refresh` setavam cor via hex literal em código, ignorando qualquer dicionário de tema — corrigido para `FindResource` antes desta feature ser construída, senão o dot do overlay/mic ficaria preso à cor escura mesmo em tema claro.
- **Validado**: build limpo, todas as chaves conferidas manualmente entre os dois arquivos de tema (idênticas, sem órfãs), nenhum `StaticResource` remanescente, app reaberto sem erro. **Validação visual ao vivo (as duas paletas, a troca em tempo real, e o modo Sistema) ainda pendente do proprietário** — é o tipo de coisa que só existe de verdade quando alguém vê na tela.

---

## D018 — Fundação da plataforma SaaS (P3): persistência, identidade, entitlements e metering

*Produzida por `scribe-architect`, revisada pelo Advisor antes de commitar. P0-P2 completos; D004 previu Postgres "quando entitlements/billing exigirem" — esse momento chegou.*

### Decisão 0 (transversal) — P3 não depende de P6
P3 entrega sem nenhuma página web própria. Login usa a página hospedada do IdP; checkout/gestão de assinatura usam Stripe Checkout + Customer Portal (hospedados pela Stripe). Única página própria: `GET /api/billing/return`, HTML estático de retorno do Checkout. Nada disso vai para a Vercel — D007 continua valendo só para a landing page (P6).

### Decisão 1 — Persistência: Postgres no Railway + Drizzle ORM
Drizzle (`drizzle-orm`/`drizzle-kit`/`pg`) em vez de Prisma: migrations são `.sql` legíveis em review, `db.execute(sql\`...\`)` é first-class, `drizzle-kit migrate` roda bem como pre-deploy command do Railway. Os dois caminhos quentes do sistema (reserva atômica de quota, insert idempotente de evento) são **SQL cru independente do ORM escolhido** — nenhum ORM expressa bem `UPDATE ... WHERE <saldo> RETURNING` ou `INSERT ... ON CONFLICT DO NOTHING RETURNING`.

Schema (5 tabelas): `users` (identidade, IdP trocável via coluna `auth_provider`), `subscriptions` (espelho read-model do Stripe, fonte de verdade continua sendo a Stripe), `usage_periods` (janela de quota alinhada ao ciclo real da assinatura, existe pra todos os tiers com `quota_seconds NULL` = ilimitado, permite telemetria de custo universal), `usage_events` (uma linha por gravação, não por tentativa — tabela de idempotência, guarda tokens/modelo/duração e **nenhum texto**, preservando a postura de privacidade "histórico só local"), `webhook_events` (dedupe de retries da Stripe).

### Decisão 2 — Autenticação: OAuth 2.0 Authorization Code + PKCE, navegador do sistema, loopback
App desktop nativo segue RFC 8252: abre o navegador padrão, `redirect_uri = http://127.0.0.1:<porta efêmera>/callback` via `HttpListener` local. Access token JWT curto (~1h) só em memória; **refresh token cifrado com DPAPI** em `%LOCALAPPDATA%\AionixScribe\auth.dat` (único arquivo do app que foge do padrão "JSON em claro" — proposital, é um refresh token). Backend valida JWT via JWKS do IdP, nunca vê senha, nunca emite token próprio. Provisionamento de usuário lazy no primeiro request autenticado com `sub` desconhecido.

IdP recomendado: **Auth0** (documenta loopback redirect pra apps nativos, aceita `http://127.0.0.1:*` como callback, refresh token rotation, free tier cobre P3-P5) — **verificar isso no painel antes de escrever código**; se falhar, trocar de IdP é barato (coluna `auth_provider` existe pra isso). Descartado: auth própria (arrasta reset de senha/verificação de email, um subprojeto inteiro), custom URI scheme (dependeria do instalador/P5, sequestrável), Device Code Flow (UX pior sem necessidade, já tem navegador local disponível).

**Aposentadoria do `X-App-Secret` (D013)**: os dois mecanismos coexistem só durante o cutover (ver sequenciamento, Passo 3 dividido em dois deploys). Ao final, `DESKTOP_SHARED_SECRET` é removido do Railway, `server.ts`, `BackendClient.cs` e `.env.example`. D013 passa a **superseded by D018**.

### Decisão 3 — Idempotência e metering
**Chave de idempotência mintada no cliente**: um `Guid` gerado uma vez logo após `_recorder.Stop()`, enviado como `X-Recording-Id` nas duas tentativas automáticas, e **gravado no nome do arquivo pendente** (`{timestamp}__{guid}.wav`) — crítico porque hoje `PendingRecordings.Save(wav)` só é chamado depois das duas tentativas, e o reprocessamento manual pode acontecer horas depois.

Duração calculada **no servidor** (parse do chunk `fmt` do WAV) — nunca confiar em duração informada pelo cliente, é o valor cobrado. Fluxo "reserva-e-libera": insere `usage_events` com `ON CONFLICT (user_id, recording_id) DO NOTHING` (idempotência); reserva atômica via `UPDATE usage_periods SET consumed_seconds = consumed_seconds + $d WHERE ... AND consumed_seconds + $d <= quota_seconds RETURNING` (zero linhas = 402); chama a Gemini; sucesso ou "nenhuma fala detectada" (`finishReason STOP` sem texto, D011) = `billed`, consome quota (áudio foi "efetivamente processado" per D006, custo real incorrido); qualquer falha técnica real = libera a reserva, não consome. Reaper periódico libera `in_flight` órfãos (~10min) pra quota nunca vazar. Teto anti-abuso de Premium/Ultra vira uma constante nova em `tiers.ts` (`ABUSE_DAILY_CEILING_SECONDS`), não fica em prosa.

Resposta de `/api/transcribe` ganha um bloco `quota: { tier, consumedSeconds, quotaSeconds, percent, periodEnd, warningLevel }` — os avisos de 80/95/100% (D006) não custam round-trip extra.

### Decisão 4 — Provisionamento do Postgres no Railway
`railway status` confirma link no projeto `aionix-scribe`; `railway add --database postgres` (**conferir a flag exata com `railway add --help` antes de rodar** — sintaxe pode diferir de `railway add --service`, já usado antes); `DATABASE_URL` no serviço `aionix-scribe-api` como **referência de variável** (`${{Postgres.DATABASE_URL}}`), nunca copiado/colado; usar a URL de rede privada, não a pública; nunca `railway variables` sem `--set` (foi esse comando que vazou secrets de outro projeto em D005); migrations no pre-deploy command, nunca da máquina local contra a URL pública.

### Decisão 5 — Sequenciamento (cada passo testável ao vivo)

| # | Entrega | Prova ao vivo |
|---|---|---|
| 0 | Postgres + Drizzle + migration inicial + `GET /health/db` | curl real retorna `select 1`; zero mudança de comportamento no fluxo existente |
| 1 | IdP + `GET /api/me` (provisiona lazy). Cliente: PKCE loopback + DPAPI + "Entrar" na bandeja | Login real; `/api/transcribe` **intocado** (ainda `X-App-Secret`) |
| 2 | `GET /api/me/entitlement`. `subscriptions` do dono semeada por SQL manual | UI real lendo dados reais sem billing existir ainda |
| 3a | Backend aceita `Bearer` **e** `X-App-Secret` em paralelo (deploy 1) | Teste de voz real fim-a-fim com o mecanismo novo, sem remover o antigo |
| 3b | Remove `X-App-Secret` de tudo (deploy 2, separado) — **só depois que 3a for validado ao vivo** | Confirma que o cutover não deixou o app sem caminho de ditado funcionando |
| 4 | Metering completo + **classificação de erro no cliente em 3 categorias** (ver nota abaixo) | `ESSENCIAL_MONTHLY_QUOTA_SECONDS=60` temporário: 80/95/100%, 402, retry sem cobrança dupla, reprocessar arquivo antigo sem consumo extra |
| 5 | Stripe checkout/portal/webhook + `STRIPE_WEBHOOK_SECRET` | Checkout real modo teste, portal real, Stripe Test Clocks pra renovação de ciclo |
| 6 | Teto anti-abuso Premium/Ultra + polimento de avisos na UI | Teste com teto rebaixado temporariamente |

**Correção do Advisor ao plano original do architect — passo 3 dividido em 3a/3b** (não um só): o architect propunha cutover em um único passo; isso deixaria o app sem nenhum caminho de ditado funcionando se a validação do JWT contra o JWKS real se comportasse mal. Dois deploys separados garantem que sempre existe um mecanismo funcionando enquanto o outro é validado.

**Gap real encontrado pelo Advisor, adicionado ao escopo do Passo 4**: hoje `BackendClient.TranscribeAsync` lança exceção em qualquer resposta não-2xx, e `TryTranscribeAndInsertAsync` trata qualquer exceção como falha técnica — retry automático + `PendingRecordings.Save` + mensagem "não consegui transcrever, verifique sua conexão". Isso significa que os novos códigos determinísticos do D018 (402 quota esgotada, 401 token expirado, 429 teto de abuso, 415 WAV ilegível) cairiam todos nesse balde errado: usuário sem minutos ouviria que a internet está com problema, e `pending/` acumularia gravações que vão dar 402 pra sempre. `BackendClient` precisa expor o status code (não só embutir na mensagem da exceção) e `TryTranscribeAndInsertAsync` precisa de 3 categorias de resultado — sucesso, falha técnica retentável, e recusa determinística (sem retry, sem preservar, mensagem específica, com caminho de upgrade no caso do 402). Isso bloqueia considerar o Passo 4 concluído, não os passos 0-3.

### Handoff
`scribe-backend`: passos 0, 2, 3 (servidor), 4 (servidor), 5, 6. `scribe-desktop`: passo 1 (PKCE/DPAPI/UI login), passo 2 (exibição tier/quota), passo 3 (troca de header no cliente), passo 4 (mintagem do guid + as 3 categorias de erro), passo 5 (abrir navegador + polling de entitlement). `scribe-security`: revisão antes de fechar 3b e depois de 5. `scribe-reviewer`: revisão adversarial antes de declarar P3 concluído.

### Pendências reais do proprietário (ver PENDENCIAS_USUARIO.md)
Política de trial para "autenticado sem assinatura" e aprovação do Auth0 como novo fornecedor SaaS são bloqueios genuínos — só o proprietário decide. A dúvida sobre "áudio sem fala consome quota" **não bloqueia**: D006 já cobre isso via "efetivamente processado" (o áudio foi enviado à Gemini, custo real incorrido) — adotado o default do architect (consome), sinalizado pra confirmação, revertível numa linha se o proprietário discordar.

O Passo 0 (Postgres/Drizzle/migração inicial) não depende de nenhuma das duas pendências acima — começa já nesta sessão.

*Novas decisões de impacto significativo serão adicionadas a este arquivo conforme o projeto avança.*

---

## D019 — Política de idioma na transcrição e portão local de silêncio

*Motivada por dois pedidos do proprietário: (a) o texto às vezes saía misturando português e inglês, sem lógica clara; (b) "quando for algo muito simples, não mandar pra IA arrumar, pra economizar".*

### Constatação que reformula o pedido (b)
Não existe um passo de "arrumar" separado para pular. `/api/transcribe` faz **uma única** chamada à Gemini que transcreve e limpa ao mesmo tempo — sem ela não há texto nenhum. O custo dominante é o áudio de entrada, pago independentemente de o resultado ser simples ou complexo. Logo, "detectar que é simples e não chamar a IA" não é implementável sem trocar a arquitetura por um estágio de STT local + limpeza condicional (mudança grande, território de P4, não feita agora).

O que **é** evitável: a chamada que nunca deveria ter saído da máquina. Implementado como portão local antes do HTTP.

### Decisão 1 — Prompt transcreve, nunca traduz
`TRANSCRIPTION_PROMPT` dizia "Transcreva o áudio a seguir **para** português do Brasil" — instrução de *tradução*, causa provável tanto de fala em inglês sair em português quanto de estrangeirismos serem "corrigidos" de forma inconsistente. Substituída por uma seção IDIOMA explícita: escrever no mesmo idioma falado; nunca traduzir; termos técnicos, nomes de produto, siglas e nomes próprios ficam exatamente como ditos ("fiz o deploy", "abre um pull request") — isso é o vocabulário real da pessoa, não mistura de idiomas; fora esses termos, a estrutura da frase segue um idioma só; fallback pt-BR quando o idioma não for identificável com confiança.

Custo: o prompt ficou ~3x maior, algumas centenas de tokens de entrada a mais por chamada. Provavelmente irrelevante perto do áudio — mas é medível agora (ver Decisão 3), não estimado.

### Decisão 2 — Portão local de fala (`AudioRecorder.HasLikelySpeech`)
Rejeita antes do HTTP: áudio vazio, gravação < 0,6s, ou pico de energia abaixo do limiar. Isso é uma **mudança de regra de negócio, não só otimização**: pelo D006, "nenhuma fala detectada" consome cota — então até agora um toque acidental no atalho custava minutos do usuário. Áudio que não sai da máquina não pode consumir cota.

Duas armadilhas encontradas e resolvidas (a primeira só apareceu porque o Advisor exigiu prova offline em vez de aceitar build limpo):
- **Cabeçalho WAV não tem 44 bytes.** O `WaveFileWriter` do NAudio grava o chunk `fmt ` com 18 bytes (inclui `cbSize`), então o cabeçalho real é **46**. Pular 44 fixo desalinha cada amostra em um byte, o byte baixo vira byte alto, e silêncio digital lê como energia altíssima — o portão aprovaria tudo, para sempre, sem erro e sem nada estranho no log. Corrigido percorrendo os chunks RIFF até achar `data`. Medido, não deduzido: teste offline imprime 46.
- **RMS global é a estatística errada.** Uma gravação de 25s com fala só nos últimos 3s tem a média diluída pelo silêncio e seria descartada — fala real perdida, exatamente o oposto do objetivo. Critério trocado para **pico de RMS em janelas de 100ms**.

Limiar deliberadamente conservador (RMS 90; fala fica na casa dos milhares, ruído de sala abaixo de ~50): preferimos deixar passar um áudio duvidoso a engolir fala baixa.

Validado offline com WAVs gerados pelo mesmo caminho `WaveFileWriter`/`WaveFormat(16000,16,1)` do `AudioRecorder.Start`: silêncio digital, ruído baixo, tom alto, tom curto demais, silêncio longo com fala só no fim, e WAV só com cabeçalho — 6/6.

### Decisão 3 — Instrumentar antes de otimizar mais
`BackendClient` passa a registrar `usage` (prompt/candidate/total tokens + bytes de áudio) no `DebugLog`, sem nenhum texto transcrito. Sem isso, qualquer discussão futura sobre economia (baixar `thinkingBudget`, modelo mais barato para clipes curtos, STT local) seria baseada em estimativa. Duas gravações reais — uma curta, uma longa — respondem qual fração é áudio de entrada versus saída/thinking.

### Pendências desta decisão
- **Validação ao vivo obrigatória antes de considerar fechado**: o golden dataset não existe (P0), então esta mudança de prompt pode regredir remoção de hesitação/pontuação sem nada detectar. Precisa de 4 gravações reais do proprietário: pt-BR **curta** (célula onde o D011 já viu resposta vazia, e o prompt novo pede mais deliberação com `thinkingBudget` ainda em 256), pt-BR com termos técnicos em inglês, inglês puro, e uma gravação de silêncio (confirmar que não sai da máquina — `debug.log` mostra "descartado sem enviar").
- **`GEMINI_MODEL` usa o alias flutuante `gemini-flash-latest`**: o comportamento do produto pode mudar sem deploy nosso, e "às vezes" inconsistente combina tanto com prompt vago quanto com alias mudando debaixo do pé. Fixar uma versão explícita é barato e para de depurar alvo móvel — não feito aqui por não ser decisão de engenharia isolada (muda o modelo em produção).

### Fechamento do D019 (mesma sessão)

- **Rotação do `DESKTOP_SHARED_SECRET` concluída e verificada** contra produção: valor novo passa (400 por falta de corpo), valor antigo rejeitado (401). O valor antigo, presente no commit `217353e` de um repositório agora público, está queimado.
- **`GEMINI_MODEL` fixado em `gemini-3.6-flash`.** Ficava pendente por "não é decisão de engenharia isolada" — resolvido porque o teste fim-a-fim devolveu `modelVersion: gemini-3.6-flash` no campo de resposta, ou seja, o alias `gemini-flash-latest` já havia migrado para a família 3.x em algum momento sem nenhum deploy nosso. Isso é candidato direto ao "às vezes mistura idioma" que originou este D019, e é exatamente o risco que um alias flutuante cria: o par prompt+modelo é a qualidade inteira do produto. Versão confirmada em produção antes de fixar, nunca chutada.
- **Validado fim-a-fim em produção** com uma gravação real de fala humana: transcrição correta, inteiramente em português, sem tradução.
- **Release v0.1.0 publicada** no GitHub (`AionixScribe-v0.1.0-win-x64.exe`, self-contained, não exige .NET instalado). Link estável: `/releases/latest/download/AionixScribe-v0.1.0-win-x64.exe`.

**Continua pendente**: as quatro gravações de validação de idioma (pt-BR **curta** — a célula de risco do D011 —, pt-BR com termos em inglês, inglês puro, e silêncio) não foram feitas; o teste fim-a-fim usou um áudio longo, que não é o caso arriscado. E o binário publicado, embora compilado depois da rotação (ordem de build conferida), nunca falou com o backend rotacionado — só um download real do release e um ditado confirmam isso.

---

## D020 — Shell de janela única substitui o modelo de três janelas

*Pedido do proprietário com referência visual anexada (Wispr Flow): "uma única tela, e dentro dela várias telas".*

### Decisão
`MainPanelWindow` deixa de ser uma tela e passa a ser um **shell**: barra de título própria, navegação lateral e uma área de conteúdo que troca de seção. `HistoryWindow` e `SettingsWindow` foram **convertidas em UserControls** (`HistorySection`, `SettingsSection`) e os arquivos de janela, removidos — não coexistem duas implementações da mesma tela para não divergirem. `DictationSection` nasce do conteúdo antigo do painel. Janela agora é redimensionável (900x620, mínimo 760x520) em vez de `SizeToContent`.

Os itens da bandeja ("Histórico...", "Configurações...") continuam existindo: agora chamam `OpenMainPanel(PanelSection)` e abrem a janela já na seção certa, em vez de abrir janelas separadas.

Navegação usa `RadioButton` com `GroupName` e o `NavItemStyle` novo — "exatamente um ativo" e navegação por teclado saem de graça do WPF, em vez de estado de seleção reimplementado à mão.

### Conta e Plano ficaram de fora, de propósito
A referência tem perfil, "0 words remaining" e "Upgrade to Pro" na lateral. Nada disso existe aqui: autenticação, entitlements e billing são P3, não construídos. A regra do projeto proíbe UI sem funcionalidade real por trás (mesma razão pela qual Configurações → Conta/Idioma continua não existindo, ver P2 no ROADMAP). A lateral já está estruturada para recebê-los como irmãos dos itens atuais quando o P3 entrar — o comentário no XAML registra isso para quem mexer depois.

### Três armadilhas de migração tratadas
1. **Captura de atalho quebraria em silêncio.** `SettingsWindow` assinava `PreviewKeyDown` em si mesma e chamava `Focus()`. Num `UserControl`, `PreviewKeyDown` só dispara com o foco dentro dele e `Focus()` é no-op sem `Focusable` — o botão ficaria preso em "Pressione o novo atalho..." para sempre, sem erro visível. Agora a seção assina o evento **da janela** no `Loaded` e solta no `Unloaded` (que também destrava uma captura interrompida ao trocar de seção).
2. **Seções são reaproveitadas, não recriadas.** `HistoryWindow` só recarregava no construtor; como seção, ficaria congelada enquanto ditados novos chegam pelo atalho global com a janela fechada. Cada seção expõe `Refresh()`, chamado ao navegar.
3. **`Close()` e `MessageBox.Show(this)` não valem em `UserControl`.** Fechar é responsabilidade do shell; o diálogo de confirmação passou a usar `Window.GetWindow(this)` como dono, senão pode abrir atrás da janela e travar a interação sem explicação.

### Pendente de validação ao vivo
Aparência das três seções e, principalmente, **trocar o atalho de verdade** — é a regressão que nenhum print revela e nenhum build detecta.

---

## D021 — Instalador Windows real (Inno Setup), substituindo o executável solto

*Pedido do proprietário: "não deve ficar um executável, deve ser um instalador... bem personalizado, temático ao nosso projeto".*

### Por que existia um .exe solto
Não foi decisão de arquitetura, foi sequência: quando o pedido era "sobe e deixa baixável pra eu testar", o caminho mais curto até algo instalável-e-testável era um `publish` self-contained single-file. O instalador sempre foi P5. Este D021 traz o P5 (parcialmente) para frente porque a atualização automática pedida depende dele.

### Decisão 1 — Inno Setup, não Velopack/MSIX
Velopack resolveria instalador **e** atualização de uma vez, mas seu instalador é um splash mínimo sem personalização real — perde no requisito explícito de "temático ao nosso projeto". MSIX exige assinatura de código (pendência #5, inexistente). Inno dá controle total do assistente (arte, textos, páginas) e sua reinstalação silenciosa (`/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS`) já é um mecanismo de atualização adequado — o que elimina a gambiarra de um `.exe` sobrescrevendo a si mesmo em execução.

### Decisão 2 — Instalação POR USUÁRIO, não em Arquivos de Programas
`PrivilegesRequired=lowest`, destino `{localappdata}\Programs\AionixScribe`. Esta é a decisão que **viabiliza a atualização automática**: sob Arquivos de Programas, toda atualização dispararia UAC — o usuário veria pedido de administrador a cada versão, ou a atualização falharia silenciosamente. Também mantém intacta a inicialização automática via `HKCU\...\Run`, que guarda o caminho do executável.

### Decisão 3 — Pacote em pasta, não single-file
O instalador empacota o publish self-contained **sem** `PublishSingleFile`. Resultado: instalador de **49 MB** contra os 156 MB do executável solto (LZMA2 sólido comprime muito melhor a pasta), e sem o custo de extração para o temp a cada inicialização que o single-file impõe.

### Detalhes que quebrariam depois se ficassem para depois
- **`AppId` GUID fixo** (`CDE0D1BA-…`): é o que faz uma instalação nova *substituir* a anterior. Trocá-lo depois criaria um produto novo aos olhos do Windows e deixaria a versão antiga órfã na máquina de quem já instalou.
- **`CloseApplications`/`RestartApplications`**: sem isso, atualizar por cima do app aberto falharia com arquivo bloqueado — exatamente o erro que aparece ao recompilar com o app rodando.
- **Dados do usuário sobrevivem à desinstalação**: `%LOCALAPPDATA%\AionixScribe\` (histórico, atalho, tema, pendências) só é apagado se a pessoa disser sim numa pergunta explícita. Coerente com o texto de Privacidade dentro do app: aquela pasta é do usuário.
- **Ativo de download com nome fixo**: o release publica o instalador duas vezes — `AionixScribe-Setup-<versão>.exe` (histórico) e `AionixScribe-Setup.exe` (nome estável). O segundo torna `/releases/latest/download/AionixScribe-Setup.exe` um link permanente; com só o nome versionado, o botão do site quebraria a cada release.

### Versão: uma origem só
`<Version>` no `.csproj` → carimbada no binário pelo compilador → lida pelo `.iss` (`GetStringFileInfo`) e pelo `scripts/release.ps1`. Antes disso o assembly nascia como `1.0.0.0`, **maior** que a `v0.1.0` publicada — qualquer comparação de versão para atualização automática responderia "já está atualizado" para sempre. `IncludeSourceRevisionInInformationalVersion=false` impede que o hash do commit apareça na versão exibida.

### Validado
Compilação, instalação silenciosa (código de saída 0), executável em `{localappdata}\Programs\AionixScribe` com `ProductVersion 0.2.0`, entrada de inicialização automática apontando para o caminho instalado, atalho no menu Iniciar, desinstalador registrado, e o app **abrindo a partir do local instalado**.

**Pendente do proprietário**: rodar o instalador de forma interativa para julgar a identidade visual do assistente (a captura automatizada não pegou a janela), e o teste de desinstalação com a pergunta sobre apagar dados.

### Ainda não feito (próximo passo)
O mecanismo de atualização automática em si — verificação de versão nova, painel do "o que mudou", adiar/lembrar depois e o selo permanente de atualização. Foi para o passo seguinte porque não dá para testar atualização sem existir primeiro uma instalação de onde atualizar.

---

## D022 — Atualização automática: verificação, painel de novidades e instalação silenciosa

*Pedido do proprietário: avisar dentro do app quando sai versão nova, com um painel explicando o que melhorou, opção de adiar, e um selo permanente de atualização.*

### Decisão 1 — Manifesto estruturado, não texto do release
Cada release publica um `update.json` com **nome fixo**, então `/releases/latest/download/update.json` sempre aponta para a versão mais recente. Duas alternativas descartadas: a API do GitHub (limite de requisições por IP, e o app verifica de 6 em 6 horas em milhares de máquinas) e interpretar o corpo em markdown do release (renderizar markdown em WPF é trabalhoso e frágil). Com manifesto, o "o que mudou" chega **estruturado** — título e explicação por item — que é exatamente o que o painel precisa para não ser só "há uma atualização".

### Decisão 2 — Fonte única do "o que mudou"
`releases/<versão>.json` é escrito à mão e o `scripts/release.ps1` gera **as duas coisas** dele: o corpo em markdown do release no GitHub e o `update.json` lido pelo app. Se cada um fosse escrito separadamente, divergiriam e o painel dentro do app passaria a mentir sobre o que mudou. O script também recusa publicar se a versão das notas diferir da versão compilada.

### Decisão 3 — Segurança do caminho "baixar e executar"
Este é o único ponto do produto que baixa um binário e o executa. Três travas:
- **SHA-256 obrigatório**: hash ausente falha igual a hash errado. Se pudesse ser omitido, bastaria um manifesto sem o campo para desligar a verificação inteira.
- **Lista de hosts permitidos** (`github.com`, `objects.githubusercontent.com`, `release-assets.githubusercontent.com`) + exigência de HTTPS: um manifesto pode declarar qualquer `setupUrl`, e sem essa lista um erro futuro na hospedagem do manifesto viraria execução remota de código em toda máquina instalada.
- **`setupUrl` aponta para o ativo versionado**, não para `/latest/`: com `/latest/`, o manifesto da 0.3.0 passaria a apontar para o instalador da 0.4.0 assim que ela saísse, e a verificação de integridade falharia.

### Decisão 4 — Instância única passou a ser obrigatória
O instalador reabre o app ao terminar (`RestartApplications`) e a chave `HKCU\...\Run` também pode disparar. Duas instâncias competiriam pelo **mesmo atalho global**, e a segunda falharia em registrá-lo — o usuário veria "sem atalho disponível" sem explicação nenhuma. Mutex nomeado no startup resolve; sem a atualização automática isso era só higiene, com ela é carga estrutural.

### Decisão 5 — Adiar silencia o aviso, nunca o selo
Exatamente o que foi pedido: "pode marcar pra depois e relembrar, mas sempre ia ficar uma taginha". Adiar guarda `versão + prazo de 24h` e suprime só o balão da bandeja; o selo no topo da janela fica enquanto houver versão nova. Guardar a **versão** junto com o prazo faz uma release mais nova avisar na hora, em vez de herdar o silêncio da anterior. Não existe "pular esta versão" — seria o oposto do pedido.

### Comparação de versão
Normalizada para 3 componentes antes de comparar: o assembly carimba `0.4.0.0` e o manifesto traz `"0.4.0"`; comparar direto faria `0.4.0 < 0.4.0.0`. Versão remota **menor ou igual** não é atualização — sem isso, um manifesto em cache velho ou um release publicado errado reinstalaria a mesma versão em laço. Falha de rede, JSON inválido ou versão ilegível resultam em "sem atualização": "não sei" nunca vira "tem atualização".

### Incidente durante a validação (vale registrar)
A verificação passou a falhar com 404 e a causa não era o código: **o repositório tinha sido tornado privado**. Ativos de release em repositório privado exigem autenticação, e o app de um usuário qualquer não tem token. Isso vale para o botão de download do futuro site também. Conclusão que fica: **o que precisa ser público são os ativos do release**, não necessariamente o código — se um dia o código fechar, os releases precisam migrar para um repositório público separado ou outra hospedagem.

### Validado ao vivo (duas releases reais, 0.3.0 → 0.4.0)
- 0.3.0 instalada busca o manifesto e compara certo com ela mesma: `update: local=0.3.0 remoto=0.3.0`, sem falso positivo.
- Publicada a 0.4.0, a mesma instalação 0.3.0 detectou sozinha: `update: local=0.3.0 remoto=0.4.0`.
- SHA-256 do manifesto confere com o instalador realmente publicado (baixado e conferido).

**Pendente do proprietário**: o clique final — abrir o app, ver o selo, ler o painel e mandar atualizar, confirmando que o app fecha, instala e reabre na 0.4.0.

### D022 — correção: o app não reabria após a atualização (0.5.0)

Relatado ao vivo pelo proprietário: a atualização baixava, instalava e **fechava o app sem reabrir**.

Causa: `RestartApplications=yes` delega a reabertura ao Restart Manager do Windows, que só reabre programas registrados via `RegisterApplicationRestart()` — o Aionix Scribe não se registra. E a entrada `[Run]` que abre o app ao fim da instalação tinha `skipifsilent`, justamente o modo usado pela atualização automática. Ou seja: os dois caminhos possíveis de reabertura estavam desligados ao mesmo tempo.

Corrigido com uma segunda entrada `[Run]` com `Check: WizardSilent`, que reabre explicitamente no modo silencioso. Uma reabertura dupla (Restart Manager + esta linha) é inofensiva porque o mutex de instância única encerra a segunda cópia — o D022 já tinha adicionado o mutex por outro motivo, e ele pagou aqui.

Adicionada também confirmação visível: ao voltar, se a versão em execução difere da última registrada, o app avisa na bandeja em qual versão está agora. Sem isso, o usuário via o app fechar e reabrir sem nenhuma pista de que deu certo. **Limitação honesta**: quem atualizar a partir da 0.4.0 não verá esse aviso, porque a 0.4.0 nunca gravou a versão em execução — o aviso passa a funcionar da 0.5.0 em diante.

Validado ao vivo pelo proprietário: atualização instalada com o app aberto, reabertura automática, e uma única instância em execução ao final.

---

## D023 — Superfícies nativas do Windows substituídas pela interface do app

*Pedido do proprietário, a partir de uma captura do menu da bandeja branco sobre um app escuro: "o que for nosso e a gente puder mudar, vamos mudar... não deve ficar a nossa interface mais a do Windows. Ou a gente coloca a nossa e bloqueia a do Windows, ou deixa só do Windows."*

Regra adotada: **nunca as duas ao mesmo tempo**. Onde a nossa entra, a do Windows sai junto.

### As três superfícies e o grau real de controle
| Superfície | Controle possível | Decisão |
|---|---|---|
| Menu da bandeja (`ContextMenuStrip`) | Total, via renderer e color table próprios | Nosso |
| Confirmação (`MessageBox`) | Nenhum — o Windows não aceita personalização | Substituído por `ConfirmDialog` |
| Avisos (`ShowBalloonTip`) | Nenhum — são notificações desenhadas pelo shell | Substituídos por `ToastWindow` |

### Menu da bandeja
É WinForms (o `NotifyIcon` exige `ContextMenuStrip`), então **não enxerga os dicionários de tema do WPF** — nasce branco padrão. As cores são lidas dos brushes do tema **em tempo de execução**, não copiadas como literais, e `App.ApplyTheme` repinta o menu a cada troca; sem isso, quem saísse do escuro para o claro ficaria com o menu escuro para sempre. A margem de ícone foi desligada (teima em ficar clara e nenhum item usa ícone) e a cor de item **desabilitado** é definida explicitamente: o renderer padrão usa o cinza do sistema, que sobre fundo escuro faz "Reprocessar pendências (0)" sumir em vez de apenas parecer apagado. A fonte Sora do app precisou ser carregada em memória via `PrivateFontCollection` — ela é recurso embutido e o GDI+ não enxerga fonte não instalada no Windows.

### Avisos próprios: o que se ganha e o que se perde
Notificações do Windows aparecem com o app fechado e ficam guardadas na Central de Ações; as nossas, não. Aceitamos a troca porque **o estado durável já é representado em lugares permanentes do app** — contador de pendências no menu da bandeja, selo de atualização na janela. Estes avisos são anúncios passageiros, não a única pista de que algo aconteceu.

`ShowActivated="False"` no `ToastWindow` não é estética, é requisito: o app existe para inserir texto no aplicativo em que a pessoa está trabalhando, e um aviso que rouba o foco mudaria o destino do ditado no meio do uso.

**Bug encontrado na captura de tela, não em teoria**: posicionar o aviso logo após `Show()` usa `ActualHeight` ainda zero (o layout não rodou), e ele nasce com o rodapé cortado na borda inferior. Corrigido com `UpdateLayout()` antes de posicionar, mais reposicionamento em `ContentRendered`/`SizeChanged` para o caso de o texto quebrar em mais linhas do que a primeira medição previa.

### Confirmação própria
`ConfirmDialog` é modal de verdade (`ShowDialog` com `Owner`), aceita Esc (não há barra de título para fechar) e **começa com o foco em "Cancelar"** — numa ação destrutiva, um Enter distraído não pode ser o que apaga os dados do usuário.

### Fora de alcance, e por quê
O assistente do instalador já está no limite do que o Inno Setup permite personalizar, e o aviso do SmartScreen é do sistema operacional — só desaparece com assinatura de código (pendência #5). Nenhum dos dois é "nossa interface mais a do Windows": são momentos em que o app ainda não está em execução.

### D022 — correção: manifesto de atualização vindo de cache (0.7.0)

Logo após publicar a 0.6.0, o app instalado continuava lendo `remoto=0.5.0` enquanto o `curl` no mesmo instante já recebia `0.6.0` do mesmo endereço. Causa: o redirecionamento de `/releases/latest/download/` passa por CDN, e a resposta ficava em cache — o app enxergava o manifesto anterior e concluía "já estou atualizado".

Consequência se tivesse passado: parte dos usuários simplesmente não receberia uma versão recém-publicada, sem erro nenhum aparecendo em lugar algum — o pior tipo de defeito, o que se parece com funcionamento normal.

Corrigido com cache-busting na consulta (parâmetro de tempo na URL + `Cache-Control: no-cache, no-store` e `Pragma: no-cache`). Ressalva honesta: o cache podia ter expirado sozinho no intervalo do teste, então a verificação pós-correção não é prova definitiva de causa — mas a correção é obviamente certa e sem risco.

---

## D024 — Site do produto (P6): primeira versão pública

*Pedido do proprietário: site premium, persuasivo, com elementos 3D e integrado/público.*

### Decisão 1 — O CTA principal é "Baixar grátis", não "Assinar"
Os preços existem de verdade no Stripe (`tiers.ts`), mas **o checkout não**: P3 Passo 5 não foi construído, não há autenticação nem entitlements. Vender uma assinatura que o app não sabe conceder seria pior do que não vender. Payment Links do Stripe foram **descartados como atalho** exatamente por isso — receberiam dinheiro sem que o produto pudesse liberar nada.

Decisão do proprietário entre as opções apresentadas: **download grátis + lista de espera com endpoint real**. Nada de formulário decorativo.

### Decisão 2 — Lista de espera é funcionalidade de verdade
Tabela `waitlist_signups` (e-mail como chave primária, `ON CONFLICT DO NOTHING` = reenvio idempotente) e `POST /api/waitlist` no backend do Railway. A resposta é idêntica para e-mail novo e repetido: dizer "você já está na lista" revelaria a terceiros quem se cadastrou. Validação de formato deliberadamente permissiva — a única prova real de que um e-mail existe é enviar mensagem para ele.

CORS liberado só para o site: origens `*.vercel.app` por **regex**, porque cada deploy de preview ganha um subdomínio novo e uma lista fixa quebraria todo preview antes de ir para produção. O cliente desktop não passa por CORS (não é navegador), então liberar geral não traria nada e só aumentaria a superfície.

### Decisão 3 — 3D sem sacrificar desempenho
"Com elementos 3D" e "performático" brigam entre si; a tensão foi resolvida explicitamente:
- `next/dynamic` com `ssr:false` — o three.js não entra no HTML inicial nem bloqueia a primeira pintura;
- alternativa estática (gradiente + anéis) mostrada enquanto o 3D carrega **e permanentemente** para quem tem `prefers-reduced-motion`;
- `dpr` limitado a 1.5 (renderizar em 3x num objeto orgânico e desfocado é custo sem diferença visível);
- `IntersectionObserver` pausa o loop de renderização quando o orbe sai da viewport — animar fora da tela é bateria e CPU jogados fora enquanto a pessoa lê o resto da página.

### Decisão 4 — Nada de prova social inventada
Sem depoimentos, sem contagem de usuários, sem logotipos de empresas. Não existe esse dado no projeto, e fabricá-lo seria inventar registro. A persuasão vem de afirmações **verificáveis**: escreve no idioma falado sem traduzir, limpa hesitações, funciona em qualquer aplicativo, histórico só local, silêncio nunca sai da máquina.

### Infraestrutura
Projeto `aionix-scribe` no time `aionixdev` da Vercel (D007). Produção pública em `https://aionix-scribe.vercel.app` — domínio próprio adiado por decisão do proprietário, e trocar depois não gera retrabalho. O primeiro deploy nasceu com o nome `web` (a Vercel deriva do diretório) e foi relinkado: o nome do projeto define o domínio.

### Validado
Build de produção limpo; site público respondendo 200 com o título correto; link de download apontando para o ativo permanente do release; os três preços reais renderizados na página; e o endpoint da lista de espera aceitando POST com a origem de produção (CORS, gravação e idempotência conferidos).

**Pendente do proprietário**: julgar o visual — é o critério que só existe quando alguém vê na tela.
