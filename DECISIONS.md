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
  3. **Digitação caractere-a-caractere via `SendInput` é frágil para blocos de texto**: uma rajada de eventos `KEYEVENTF_UNICODE` sem pausa corrompeu texto com acentuação PT-BR (parte do texto virou caracteres repetidos), provavelmente por interferência de autocomplete/corretor ortográfico do app de destino.
  4. **Colagem via clipboard é o método confiável**: salvar clipboard → definir texto → `Ctrl+V` sintético → restaurar clipboard original. Validado com sucesso e fidelidade total de acentuação em três categorias de app distintas: controle nativo Win32 (Notepad), editor Electron/Monaco (VS Code), conteúdo web Chromium (Brave).
  5. **`SetForegroundWindow` chamado por um processo em background é bloqueado pelo Windows** (proteção de foreground-lock) — não é um problema para o produto real (a janela do usuário já está em foreground quando o hotkey é pressionado), mas exigiu clique de mouse sintético para validar o teste de forma automatizada.
- **Limitações conhecidas (não resolvidas neste spike)**:
  - **Windows Terminal**: ativação por clique não foi confirmada de forma consistente (`confirmedForeground=False` em uma tentativa) — precisa de investigação dedicada antes do P0 dar esse app como suportado.
  - **Janela elevada (UIPI)**: não foi possível automatizar um teste real contra uma janela rodando como administrador, pois isso exigiria interação humana no prompt do UAC (automatizar isso derrotaria o propósito do UIPI). Ficou como item de verificação manual. Consequência arquitetural assumida: o Aionix Scribe **nunca deve rodar elevado** (perderia acesso de injeção a todas as janelas normais), e deve **detectar falha de inserção** (nenhuma mudança confirmável após tentar inserir) e comunicar isso claramente ao usuário em vez de falhar silenciosamente.
- **Consequência para a arquitetura do injetor**: o inserter de texto do Aionix Scribe usará colagem via clipboard como caminho primário (com save/restore do clipboard original), reservando simulação de tecla (`SendInput`) apenas para teclas de controle isoladas (Enter, Backspace, setas) quando necessário para comandos de voz — nunca para o corpo do texto ditado.

---

*Novas decisões de impacto significativo serão adicionadas a este arquivo conforme o projeto avança.*
