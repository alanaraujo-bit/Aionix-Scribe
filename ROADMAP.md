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
