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

*Novas decisões de impacto significativo serão adicionadas a este arquivo conforme o projeto avança.*
