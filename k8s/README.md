# Kubernetes — Execution

Os manifests desta pasta pertencem à aplicação no namespace fiap-execution. A entrega é executada por manage-platform.yml do principal, que chama deploy-eks.yml com SHA de origem, digest, broker e issuer/audience explícitos.

Banco: DocumentDB gerenciado na AWS; MongoDB replica set local. Credencial exclusiva no Secrets Manager; JWT é compartilhado entre as APIs, sem acesso aos segredos de outros bancos. Namespace, SecretStore/role, RBAC e ConfigMap aws-database-ca pertencem ao Infra. O bundle AWS é montado somente para leitura em /certs.

Os YAMLs fonte contêm placeholders intencionais. scripts/delivery renderiza cópias temporárias, valida namespace/digest, executa dry-run, sincroniza ExternalSecrets e acompanha rollout. Billing aplica migrations por Job --migrate-only antes dos pods; inicialização/compatibilidade Execution ocorre no estágio DB anterior. HPA mantém uma a três réplicas com métricas do metrics-server.

HTTP interno 8080; entrada pública via frontend/Nginx e API Gateway. Readiness verifica dependências. OTLP segue ao Collector compartilhado, sem licença New Relic nas APIs.

A action central cria bancos, Kafka/tópicos e segredos; esses recursos não são provisões manuais avulsas. Consultar o [guia operacional](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/operacao/plataforma-aws.md) para ativação, variables/secrets e destruição diária de dados.
