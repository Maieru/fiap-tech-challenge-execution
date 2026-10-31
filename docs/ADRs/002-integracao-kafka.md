# ADR-002 — Kafka e consistência entre serviços

Status: Aceita. Data: 2026-10-31.

## Contexto e alternativas

Os serviços precisam evoluir independentemente e continuar aceitando operações quando o broker estiver indisponível. HTTP síncrono propagaria falhas e não resolveria a gravação atômica com o banco. Uma transação distribuída entre MongoDB, PostgreSQL e Kafka aumentaria o acoplamento.

## Decisão

Usar Confluent.Kafka, chave OrdemServicoId, tópicos direcionais e `MessageEnvelope<T>` do chassi. Cada direção mantém sua própria sequência contígua por OS. Outbox persiste com o efeito local; um dispatcher com lease no banco publica em ordem e marca após confirmação do broker. Produtor idempotente, entrega ao menos uma vez e Inbox transacional protegem os efeitos de reentregas. Não existe atomicidade entre o broker e os bancos.

O consumidor tenta novamente cinco vezes (1, 2, 4, 8 e 16 segundos). Ao esgotar, publica o corpo e chave originais na DLQ com metadados de origem; confirma o offset original apenas após confirmação dessa publicação. Lacunas impedem aplicação de eventos posteriores da mesma OS, mesmo após envio à DLQ. Replay preserva ID, chave, sequência e envelope. Recusas de estado geram `SolicitacaoRecusada`, enquanto erros técnicos seguem retry/DLQ.

## Consequências e evidências

Kafka KRaft de um nó e tópicos com três partições são destinados ao ambiente local/CI. Um dispatcher ativo por serviço prioriza simplicidade e ordem; escalar throughput requer particionar a liderança. Transações não contêm chamadas Kafka. Contratos e transporte são mantidos em cada serviço e validados por BDD entre imagens independentes. `KafkaWorker`, `MongoMessageStore` e `FluxoOrdemRepository` e `PostgresOutbox` materializam a decisão. Logs, traces e métricas expõem retries, DLQ, Outbox pendente, atraso e compensações. O procedimento de [replay](../operacao.md#dlq-e-replay) exige corrigir lacunas antes dos eventos seguintes.

Referências: [cliente .NET oficial](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html), [MongoDB replica set no Testcontainers](https://dotnet.testcontainers.org/modules/mongodb/).
