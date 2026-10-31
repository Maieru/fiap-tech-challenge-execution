# Evidências da migração Execution

Validação local em 31/10/2026: Windows/PowerShell 7, .NET SDK 10.0.401 e Docker Desktop com containers Linux. Ambientes Testcontainers isolados, MongoDB 8.0 replica set, Kafka 4.4 KRaft e PostgreSQL 18.3 reais. Chassi restaurado do NuGet público, versão 1.0.2.

| Componente | Cenários aprovados | Cobertura de linhas |
| --- | --- | --- |
| Execution | 97 domínio + 45 aplicação + 36 integração | 96,1% (1407/1463) |
| OS | 133 domínio + 57 aplicação + 43 integração | 90,7% (1602/1766) |
| Legado | 703, incluindo HTTP/PostgreSQL e BDD existentes | 95,4% (3500/3668) |
| BDD distribuído | 2, com as duas imagens reais | Fora da medição por processo |
| Imagem OS | 1, incluindo reinício e histórico após soft delete | Fora da medição por processo |

Cada serviço passa individualmente pelo gate de 80% do ReportGenerator 5.5.12. Cobertura coleta Cobertura e OpenCover; exclui testes e código gerado, incluindo migrations geradas. O backfill escrito manualmente permanece na métrica. Os números são de execução local, não representam um resultado Sonar ou uma execução remota do Actions.

## Cenários protegidos

- CRUD/filtros/paginação de catálogos, exclusão lógica e retenção física; snapshots e referências preservados ao alterar/inativar/recriar peça com mesmo código.
- JWT válido/inválido/ausente em todas as operações; autorização indisponível com flag desligada ou em Production; auditoria de operador/origem.
- Estoque insuficiente e disputa entre ordens, repetição de chave, conflito de conteúdo e duas requisições simultâneas com a mesma chave sem crédito/débito duplicado.
- Rollback real quando uma validação MongoDB impede gravar reserva/Outbox; rollback PostgreSQL de ordem/histórico/Inbox.
- Reentrega após commit no banco, reinício antes de confirmação do offset, duplicidade semântica, recuperação de Outbox após indisponibilidade do broker.
- Mensagens inválidas/versão incompatível e lacunas na DLQ; replay preserva envelope/chave/ID, respeita predecessor e não regride estado.
- Upgrade do esquema anterior do OS com dados existentes; backfill idempotente preservando IDs, datas e histórico.
- BDD OS → diagnóstico → autorização → reinício da API → conclusão, consulta após inativar cliente/veículo e cancelamento com devolução única inclusive para peça inativada.

## Artefatos e reprodução

Os comandos estão em [operação](operacao.md). Resultados locais, ignorados pelo Git:

- Execution: `artifacts/verification/simplified`, `artifacts/coverage/acceptance`, `artifacts/e2e-acceptance`, `artifacts/contracts`.
- Repositório OS: `artifacts/execution-migration/os-simplified`, `coverage-acceptance`, `legacy-final`, `legacy-coverage-final`, `os-image-acceptance`.

As duas imagens foram construídas com sucesso. Compose Execution e overlay OS subiram em rede e volumes próprios de validação; liveness/readiness de ambas as APIs retornaram 200. Somente os recursos descartáveis identificados pelo projeto de validação foram removidos; o monólito e os volumes existentes foram preservados. OpenAPI exportado contém 14 paths Execution e 9 OS.

Actions passaram por actionlint 1.7.12, e EF confirmou ausência de mudanças de modelo pendentes. A main do novo repositório exige PR e `ci-required`. A existência de `SONAR_TOKEN` e da chave pública correta de projeto foi conferida no GitHub sem acessar/exibir o valor do token. A análise/Quality Gate remotos precisam ser confirmados pelo primeiro pipeline; publicar uma branch não implica aprovação desses checks.

AWS, produção com alta disponibilidade/TLS/ACLs, Billing e migração de tráfego não foram exercitados e ficam fora deste incremento.
