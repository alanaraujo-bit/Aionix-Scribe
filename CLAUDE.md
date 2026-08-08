# Aionix Scribe — Política de Roteamento de Modelos e Subagentes

Este projeto usa roteamento dinâmico de modelos via subagentes definidos em `.claude/agents/`. Esta seção é uma política persistente e vale para toda sessão de trabalho neste diretório.

## Autorização permanente para uso proativo de subagentes

**O usuário autorizou explicitamente, de forma permanente, o uso proativo de subagentes neste projeto.** Isso vale como o "pedido explícito do usuário" para delegar tarefas aos agentes abaixo sempre que a tarefa se encaixar na descrição de um deles — não é necessário perguntar antes de delegar uma tarefa que corresponda claramente ao escopo de um agente listado nesta política. Essa autorização cobre apenas os agentes definidos em `.claude/agents/` deste projeto; não se estende a ações destrutivas, irreversíveis ou que afetem sistemas externos, que continuam exigindo confirmação conforme as regras gerais de segurança.

## Agentes disponíveis

| Agente | Modelo | Papel |
|---|---|---|
| `scribe-explorer` | Haiku | Exploração de codebase, busca de símbolos, mapeamento de dependências, TODOs, inventário — somente leitura |
| `scribe-implementer` | Sonnet | Implementação geral, correção de bugs, refatoração, manutenção |
| `scribe-ui` | Sonnet | UI/UX de landing page e superfícies web |
| `scribe-desktop` | Sonnet | Shell do app Windows, integração nativa, instalador/build |
| `scribe-backend` | Sonnet | API, dados, lógica de negócio, Stripe/billing, infraestrutura |
| `scribe-ai-transcription` | Sonnet | Pipeline de captura de áudio, transcrição e IA |
| `scribe-tester` | Sonnet | Escrita e manutenção de testes |
| `scribe-perf` | Opus | Análise de performance, profiling — somente leitura |
| `scribe-security` | Opus | Revisão de segurança — somente leitura |
| `scribe-architect` | Opus | Decisões arquiteturais e trade-offs de alto impacto — somente leitura |
| `scribe-reviewer` | Opus | Revisão crítica/adversarial antes de fases importantes — somente leitura |

Os agentes Opus de revisão (`scribe-perf`, `scribe-security`, `scribe-architect`, `scribe-reviewer`) não têm acesso a Edit/Write — eles diagnosticam e recomendam; a implementação sempre volta para um agente Sonnet.

## Princípios de roteamento

1. **Subagentes devem ser usados proativamente.** Sempre que uma tarefa se encaixar claramente na descrição de um agente listado acima, delegue a ele em vez de executar a tarefa diretamente na sessão principal.
2. **Haiku cuida do trabalho simples, mecânico e de baixo risco**: localizar arquivos, buscar no código, mapear dependências, inventariar componentes, coletar contexto. Nunca peça a um agente Haiku para tomar decisões de design ou fazer revisão crítica.
3. **Sonnet executa a maior parte da implementação real**: frontend, backend, app Windows, integrações, testes, correção de bugs, infraestrutura, landing page, build, instalador, manutenção. Esse é o nível padrão de trabalho de engenharia deste projeto.
4. **Opus é reservado para onde a inteligência extra realmente compensa o custo**: arquitetura, decisões de alto impacto, debugging particularmente difícil, segurança, performance, revisão adversarial, análise de trade-offs, e validação antes de considerar uma fase crítica concluída. Não use Opus para tarefas que Sonnet ou Haiku resolvem com confiabilidade.
5. **O Advisor (Opus) deve ser usado estrategicamente**: antes de planejar tarefas complexas, em decisões arquiteturais, em problemas ambíguos, quando uma abordagem falhou repetidamente, em avaliação de riscos, antes de ações irreversíveis, e antes de declarar uma fase crítica concluída. Não consulte o Advisor para tarefas triviais.
6. **Não gaste modelos caros em tarefas que um modelo mais barato resolve com segurança.** A regra geral é Haiku → Sonnet → Opus: comece sempre no nível mais barato capaz de executar a tarefa com alta confiabilidade.
7. **Qualidade tem prioridade sobre economia.** Economizar tokens nunca deve comprometer a corretude, a segurança ou a integridade do produto final.
8. **Em caso de incerteza significativa sobre a qualidade do resultado, escale para o próximo nível de modelo** em vez de insistir no nível mais barato.
9. **Depois que um modelo superior (Opus ou o Advisor) fornecer uma análise, direção ou decisão, devolva a execução ao modelo mais econômico** sempre que isso for seguro — Opus decide, Sonnet implementa, Haiku explora.
10. **A seleção de modelo é dinâmica, não uma divisão rígida por área.** Julgue cada tarefa pela complexidade e risco reais, não apenas pelo domínio (ex.: uma mudança trivial em backend pode ir para `scribe-implementer`; um bug de concorrência difícil em backend pode justificar uma consulta ao Advisor antes de delegar a correção).

## Coordenação

O agente principal da sessão continua responsável pela coordenação geral: decidir qual subagente acionar, integrar os resultados, e manter a visão de conjunto do projeto. Os subagentes não se coordenam entre si.

## Infraestrutura: política Remote-First

O SaaS do Aionix Scribe (backend, banco de dados, billing, serviços auxiliares) deve rodar em infraestrutura remota real (atualmente Railway para backend/dados, Vercel para superfícies web), não na máquina local do desenvolvedor. A máquina Windows local é usada apenas para o que exige presença física de Windows: edição de código, Git, compilação do cliente desktop, e teste de recursos nativos (microfone, hotkeys globais, overlay, system tray, inserção de texto em outros apps, instalador).

Ciclo de trabalho preferencial para qualquer mudança de backend: implementar → deploy remoto → testar contra o serviço real → analisar logs reais → corrigir → redeploy → validar. Mocks são aceitáveis em testes unitários/isolados, mas nunca substituem a validação final de uma integração real (Gemini, Stripe, banco de dados).

Projetos de infraestrutura existentes devem ser reaproveitados quando genuinamente correspondem ao escopo do Aionix Scribe; nunca duplicar sem necessidade. Ao inspecionar infraestrutura de nuvem já existente (Railway, Vercel, etc.) antes de decidir reaproveitar ou criar nova, ter cuidado especial com comandos que exibem valores de variáveis de ambiente (ex. `railway variables`) — eles podem expor secrets de projetos não relacionados. Prefira formas de inspeção que não exponham valores (nome do serviço, domínio público, resposta de um endpoint de health) antes de recorrer a uma listagem que mostra segredos em texto claro.
