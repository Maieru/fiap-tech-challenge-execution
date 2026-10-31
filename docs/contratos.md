# Contratos HTTP e eventos v1

Prefixo `/api/execution/v1`. JWT administrativo obrigatório em todas as operações, exceto saúde. Identificadores das ordens nas rotas são os IDs do OS.

| Recurso | Métodos e comportamento |
| --- | --- |
| `/servicos` | POST `{descricao, valorUnitario}`, GET paginado |
| `/servicos/{id}` | GET, PUT `{descricao, valorUnitario}`, DELETE lógico |
| `/servicos/{id}/tempo-medio` | GET, média dos itens concluídos pelo Execution |
| `/pecas-insumos` | POST `{nome, codigo, descricao, precoUnitario, quantidadeEstoque}`, GET paginado ou filtro `codigo` |
| `/pecas-insumos/{id}` | GET, PUT `{nome, codigo, descricao, precoUnitario}`, DELETE lógico |
| `/pecas-insumos/{id}/entrada-estoque` | PUT `{quantidade}`, header `Idempotency-Key` obrigatório |
| `/ordens` | GET, `estado`, `pageNumber`, `pageSize` |
| `/ordens/{id}` | GET, dados e snapshots operacionais |
| `/ordens/{id}/iniciar-diagnostico` | POST, sem corpo |
| `/ordens/{id}/servicos` | POST `{servicoId, quantidade}` |
| `/ordens/{id}/pecas-insumos` | POST `{pecaInsumoId, quantidade}`, header `Idempotency-Key` obrigatório |
| `/ordens/{id}/concluir-diagnostico` | POST, congela itens e valores |
| `/ordens/{id}/servicos/{itemId}/concluir` | POST `{tempoGastoMinutos}` |
| `/ordens/{id}/finalizar` | POST, todos os serviços precisam estar concluídos |
| `/health/live`, `/health/ready` | GET, processo e dependências, respectivamente |

Paginação padrão 1/10; fila limita `pageSize` a 100. Edição usa o ID da rota. `201` para criação de catálogo/itens, `200` nas consultas/alterações, `204` na exclusão, `400` para validação, `401` para JWT ausente/inválido, `404` para inexistentes/inativos e `409` para estado, concorrência, unicidade ou estoque insuficiente. Repetir chave e conteúdo reproduz o resultado original; conteúdo diferente com a mesma chave retorna 409. Chaves são isoladas por operação/recurso e ficam persistidas com o efeito, sem expiração automática nesta versão.

O OS adiciona `POST /api/os/v1/ordens/{id}/autorizar-execucao` e `/cancelar`, sem corpo. Retornam `202` com `solicitacaoId`, `acao` e Location da ordem. Consultas expõem `acaoPendente` e `solicitacaoPendenteId` até confirmação/recusa. O histórico contém operador, origem, solicitação e resultado. Autorização exige a flag e o ambiente descritos na operação.

Estados: 1 Recebida → 2 EmDiagnostico → 3 AguardandoAprovacao → 4 EmExecucao → 5 Finalizada. Cancelada = 7, partindo apenas de 3. O valor 6 do enum legado não é produzido pelo Execution.

## Kafka

| Tópico / origem | Tipo | Payload específico |
| --- | --- | --- |
| `fiap.os.events.v1` / OS | OrdemCriada | clienteId, veiculoId, descricaoProblema |
| idem | AutorizacaoExecucaoSolicitada | solicitacaoId, operadorId (`sub`), origem=AdministrativaTemporaria |
| idem | CancelamentoSolicitado | solicitacaoId, operadorId, origem=Administrativa |
| `fiap.execution.events.v1` / Execution | DiagnosticoIniciado | snapshot operacional |
| idem | DiagnosticoConcluido | snapshot congelado, itens e valores |
| idem | ExecucaoIniciada | solicitacaoId |
| idem | ExecucaoFinalizada | snapshot final |
| idem | ExecucaoCancelada | solicitacaoId |
| idem | SolicitacaoRecusada | solicitacaoId, motivo |

Payload comum: `ordemServicoId` e `sequence` (inteiro positivo). Sequências são independentes por direção; OS começa em 1 com OrdemCriada, Execution em 1 com DiagnosticoIniciado. Alterações de itens não geram evento; o diagnóstico concluído transporta o snapshot. A chave Kafka deve corresponder ao GUID textual da OS. Grupos: `fiap-execution-v1` e `fiap-os-v1`.

Envelope JSON camelCase: `messageId`, `type`, `version=1`, `occurredAtUtc` UTC, `correlationId`, `causationId`, `traceParent`, `traceState`, `payload`. `causationId` aponta para o evento consumido quando existe; contexto HTTP/Activity fornece correlação nas operações iniciadas por requisição. Alteração incompatível exige nova versão de contrato e migração dos consumidores. Campos opcionais desconhecidos não alteram a versão 1.

As DLQs são `fiap.os.events.v1.dlq` e `fiap.execution.events.v1.dlq`; preservam envelope/chave e acrescentam headers `original-topic`, `original-partition`, `original-offset` e `failure`. O offset é confirmado após persistir efeito e Inbox ou confirmar publicação na DLQ. Eventos fora de ordem não fazem o estado regredir nem ultrapassam lacunas.

OpenAPI real das duas imagens é anexado aos artefatos de CI, em `contracts/`. Localmente: `/openapi/v1.json` e `/scalar/v1` em Development/Testing.
