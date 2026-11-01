# Revisão da etapa 2 — Execution

Verificação local em 2026-11-01. Implementação encerrada no ponto de revisão combinado.
Nenhum commit, push, tag, publicação de pacote ou deploy foi executado.

## Entrega

- API autenticada, OpenAPI/Scalar, health checks e chassi.
- Extração de domínio/casos de uso/testes de serviços e peças; catálogo próprio, estoque e soft delete.
- Agregado de execução com diagnóstico, snapshots, autorização por mensagem, conclusão por item e finalização.
- MongoDB exclusivo: transação explícita, versões para concorrência, índice único parcial de código e deduplicação.
- Consumidor Kafka com switch e caso de uso por entrada; publicação por IMessagingService.
- Compose local, Dockerfile, roteiro de restauração de pacote OS e CI com cobertura/Sonar.
- Decisões e arquitetura no repositório principal: ADR-015, RFC-008 e docs/arquitetura/execution.md.

## Evidências

| Verificação | Resultado |
| --- | --- |
| Domain.Tests | 81 aprovados |
| Application.Tests | 44 aprovados |
| Application.Messaging.Tests | 6 aprovados |
| Integration.Tests | 17 aprovados com HTTP, MongoDB replica set e Kafka reais |
| Imagem | 1 aprovado: JWT real, OpenAPI e catálogo preservado após reinício |
| Total | 149 aprovados |
| Cobertura consolidada final | 93,8% — 1487/1584 linhas |
| Build da imagem | Aprovado, tag local fiap-execution:etapa2 |
| actionlint | Workflows validados localmente |
| Compose isolado | MongoDB, tópicos, Kafka e API iniciados; readiness Healthy; recursos de teste removidos |
| Restore-Local.ps1 | Executado com nupkg OS 0.1.0 e restore concluído |

Os testes de concorrência confirmam apenas uma baixa para duas tentativas sobre a última unidade.
Os testes de cancelamento verificam devolução única após redelivery, incluindo peça inativada.
Os testes transacionais verificam abort real de dados/estoque/deduplicação quando a publicação falha,
e offset pendente após o consumidor encerrar.
Um teste unitário verifica que uma falha de gravação impede publicação e commit.

A entrega seguida de falha no commit também foi reproduzida: a mensagem ficou no broker e o estado local foi abortado.
A falha de commit é injetada apenas na unidade de trabalho de um teste direto do caso de uso;
a API/DI de produção não tem flags, endpoints ou dependências substituídas para isso.

O teste da imagem inicialmente usou a porta antiga depois de reiniciar o container.
O teste agora consulta novamente a porta mapeada; a verificação final passou.
O Compose foi ajustado para montar o volume Kafka no diretório da imagem pertencente ao appuser,
em vez de montar um diretório temporário criado com proprietário incompatível.

Relatórios finais: artifacts/test-results/etapa2-final, artifacts/test-results/image-export e artifacts/coverage-final.
OpenAPI exportado: artifacts/execution-openapi.json.

## Dependências externas e limites

Chassi 1.1.1 e adaptador Kafka 0.1.1 foram restaurados do NuGet público.
OS.Application.Messaging 0.1.0 ainda não estava publicado e foi consumido como nupkg local,
sem referências de código-fonte entre repositórios. CI/Docker em clone limpo exigem a publicação desse contrato.

Não houve execução remota de GitHub Actions/Sonar, criação de remote Execution ou infraestrutura AWS.
A API OS e o legado não receberam mudanças funcionais nesta etapa.
O fluxo distribuído com as duas APIs reais e os cenários BDD ainda será feito na etapa 3,
junto da aprovação/cancelamento administrativos e projeção de estado/histórico no OS.
