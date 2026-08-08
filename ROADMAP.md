# Roadmap — Aionix Scribe

Hierarquia de valor (não é uma sequência rígida de datas). Cada fase só é "concluída" quando passa pelos quality gates do CLAUDE.md/diretiva mestre (funcional, testado, sem placeholder, sem mock em produção, sem secret exposto).

## P0 — Experiência essencial (capturar → compreender → formatar → inserir)
**Status: em andamento**
- [ ] Spike: hotkey global + overlay always-on-top sem roubo de foco + injeção de texto (Notepad, browser, VS Code, Terminal, janela elevada)
- [ ] Decisão de stack final registrada em DECISIONS.md (D003)
- [ ] Captura de áudio (seleção de microfone, start/stop)
- [ ] Backend mínimo: auth de dispositivo + proxy Gemini (nenhuma chamada direta do cliente)
- [ ] Pipeline: áudio → transcrição → limpeza (hesitações/repetições) → pontuação/formatação
- [ ] Inserção do texto resultante no campo de origem (SendInput + fallback clipboard, com save/restore de clipboard)
- [ ] Overlay comunica estados: ativando/ouvindo/gravando/processando/concluído/erro/cancelado
- **Bloqueio conhecido**: validação end-to-end de transcrição real depende de `GEMINI_API_KEY` (ver PENDENCIAS_USUARIO.md #1)

## P1 — Confiabilidade
- [ ] Execução em background (tray) leve, idle CPU/RAM baixos
- [ ] Hotkey configurável + detecção de conflito
- [ ] Recuperação de falhas: rede cai, API erra, crash, timeout — preservar áudio para retry
- [ ] Tratamento de estados impossíveis (dupla ativação, hotkey durante processamento, troca de janela)

## P2 — Produto
- [ ] Painel principal (status, atalho ativo, últimas transcrições, atividade)
- [ ] Histórico (visualizar, copiar, excluir, pesquisar)
- [ ] Configurações (conta, áudio, atalhos, idioma, privacidade, inicialização)
- [ ] Onboarding com primeira transcrição guiada
- [ ] Temas Light/Dark completos (não é inversão de cor)

## P3 — Plataforma SaaS
- [ ] Contas e autenticação
- [ ] Entitlements (Essential/Premium/Ultra) como fonte única de verdade
- [ ] Stripe: checkout, customer portal, webhooks, upgrade/downgrade/cancelamento
- [ ] Consumo/limites (valor exato do limite Essencial: ver PENDENCIAS_USUARIO.md #4)

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

Preenchida na primeira sessão de trabalho; revisitada conforme o produto evolui. Ver seção correspondente abaixo quando disponível.

| Dimensão | Wispr Flow (observado) | Aionix Scribe | Evidência |
|---|---|---|---|
| ativação | — | — | pendente de teste |
| inserção | — | — | pendente de teste |
| UX de gravação | — | — | pendente de teste |
| recuperação de erro | — | — | pendente de teste |
| memória/CPU idle | — | — | pendente de benchmark |
| precisão | — | — | pendente de golden dataset |

*(Preenchida progressivamente — não é bloqueante para o P0.)*
