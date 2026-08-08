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

## D003 — Stack final de UI/hotkey/overlay (PENDENTE — depende do spike)

Status: em andamento. Será preenchido após o spike de hotkey global + overlay always-on-top + injeção de texto validado contra Notepad, navegador, VS Code e Windows Terminal (incluindo caso de janela elevada / UIPI).

---

*Novas decisões de impacto significativo serão adicionadas a este arquivo conforme o projeto avança.*
