# ADR-003 — Autorização administrativa temporária

Status: Aceita. Data: 2026-10-31.

## Contexto e alternativas

Billing e aprovação pelo cliente ainda não existem. Iniciar reparos automaticamente após o diagnóstico eliminaria uma etapa de autorização; implementar pagamento agora excederia o incremento aprovado.

## Decisão

O OS aceita autorização administrativa somente em Development/Testing e com `ManualExecutionAuthorizationEnabled=true`, desabilitada por padrão. JWT real identifica o operador pelo `sub`, preservado no histórico e na mensagem. O Execution conserva o envelope na Inbox e só inicia após consumir a solicitação identificada e validar o estado. Essa autorização não representa pagamento nem aprovação pelo cliente.

Autorizar/cancelar retorna 202 após persistência local. Apenas uma ação fica pendente por OS, protegida por versão otimista. O Execution confirma ou recusa e o OS registra o resultado. Cancelamento é permitido somente em AguardandoAprovacao e devolve reservas uma única vez, mesmo para peças inativas.

## Consequências e evidências

Nenhum serviço emite JWT; ambos validam o emissor administrativo atual sem consultar seu banco. A futura integração Billing deverá substituir a origem administrativa, manter correlação e auditoria e remover o endpoint temporário. Testes verificam flag, ambiente, JWT, concorrência entre decisões e proibição de reparar antes da autorização. `OrdensController` no OS e `ProcessarEventoUseCase` no Execution implementam a fronteira.
