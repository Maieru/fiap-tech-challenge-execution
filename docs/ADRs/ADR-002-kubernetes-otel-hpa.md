# ADR-002 — Execution no EKS com HPA e Collector compartilhado

- **Status:** Aceita
- **Data:** 2026-11-02

## Contexto e decisão

O serviço precisa de implantação independente com MongoDB exclusivo e integração ao frontend existente. Adotar os manifestos em `k8s`: Service ClusterIP HTTP em 8080, Production, HPA entre 1 e 3 réplicas por CPU a 70% e atualização RollingUpdate. Namespace e SecretStore pertencem ao Infra.

A connection string vem somente de `fiap-execution-database-credentials`, com usuário exclusivo do banco `fiap_execution` e suporte a transações. JWT mantém a chave compartilhada; a licença New Relic fica na observabilidade. OpenTelemetry exporta via gRPC ao Collector central sob o nome `oficina-execution`.

A decisão transversal aceita e suas alternativas estão na ADR-022 do repositório OS. TLS interno e Collector dedicado foram descartados nesta etapa para evitar gestão adicional de certificados e duplicação de infraestrutura. A entrada pública continua HTTPS.

## Consequências e evidências

Em 2026-11-07, a ADR-023 central acrescenta os [workflows manuais ECR/EKS](../../scripts/delivery/README.md) e roles OIDC próprias. As referências de imagem são resolvidas para digest; RBAC de deploy fica restrito a fiap-execution.

As réplicas mantêm o mesmo grupo Kafka; partições limitam o paralelismo. Inicialização de coleções/índices permanece no código existente. Uma réplica mínima não oferece redundância e escala depende dos nodes. Nenhum banco/broker é criado por esses manifests.

Ver [manifestos e operação](../../k8s/README.md). A configuração não comprova rollout nem ingestão no New Relic; ambos exigem validação no ambiente.
