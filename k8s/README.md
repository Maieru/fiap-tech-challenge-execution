# Kubernetes — EXECUTION

Manifestos da API para o EKS de laboratório. Service interno `fiap-execution-service.fiap-execution.svc:8080`, namespace `fiap-execution`, ambiente Production e HTTP interno. Não há Ingress, NodePort ou LoadBalancer próprio. Entrada pública: HTTPS → API Gateway → VPC Link → ALB interno → Nginx → API.

## Pré-requisitos

- Namespace `fiap-execution` e SecretStore `aws-secrets-store` criados pelo estágio `kubernetes-configs` do repositório Infra; CRDs/operador External Secrets e metrics-server instalados por `kubernetes-addons`.
- Imagem publicada, acessível aos nodes, com tag imutável ou digest. Os manifestos não criam ECR nem publicam imagens.
- Banco exclusivo e usuário restrito a ele. MongoDB deve permitir transações (replica set ou serviço compatível); conexão autenticada e banco `fiap_execution`. Coleções e índices continuam sendo inicializados pela API.
- Kafka acessível pelos pods e com advertised listeners resolvíveis no cluster. Os quatro tópicos devem existir: `oficina.os.events.v1`, `oficina.execution.events.v1`, `oficina.os.billing.requests.v1` e `oficina.billing.events.v1`. Não há criação automática de tópicos nem provisionamento do broker.
- Collector central e licença New Relic sincronizada no namespace `fiap-observability`. O Collector pertence ao Infra e não deve ser aplicado novamente por esta pasta.

## Segredos e parâmetros

Crie no AWS Secrets Manager o segredo **`fiap-execution-database-credentials`**, JSON com somente a propriedade `Mongo__ConnectionString`, cujo valor é a connection string exclusiva deste banco. Não reutilize usuário/senha dos outros serviços ou o segredo do RDS legado.

O ExternalSecret de JWT lê somente `Jwt__SigningKey` do segredo compartilhado `fiap-secret-manager-jwt-signing-key`. A chave deve ter ao menos 32 bytes UTF-8 e issuer/audience devem coincidir nas três APIs. OS é o emissor administrativo.

A role do operador External Secrets precisa de `secretsmanager:DescribeSecret` e `secretsmanager:GetSecretValue` nos ARNs dos novos segredos; a política atual do Infra ainda precisa dessa concessão antes do rollout. Para CMK própria, providencie também a permissão de descriptografia aplicável. Criação dos segredos, permissões IAM e grants dos bancos são pré-requisitos externos a esta entrega.

Prepare uma cópia local dos YAMLs e substitua **todos** os valores `REPLACE_WITH_*`: imagem em `deployment.yaml`, broker, issuer e audience em `config-maps.yaml`. Imagem de exemplo é intencionalmente inválida. Não coloque senhas, connection strings ou chaves nos ConfigMaps/manifests.

## Aplicação manual

Alternativa automatizada: use os workflows manuais `publish-image.yml` e `deploy-eks.yml` em main, após CI aprovado. Eles publicam no ECR e renderizam/aplicam estes manifestos por digest. Configure as roles/variables conforme o [guia de entrega](../scripts/delivery/README.md) no próprio repositório. Os pré-requisitos de bancos, Kafka, segredos/IAM e migrations permanecem os mesmos.

Confirme o contexto e configure o caminho da cópia preparada. Não use a árvore `legacy`.

```powershell
$manifests = '<caminho-absoluto-da-copia-configurada>'
kubectl config current-context
# Este comando deve terminar sem ocorrências antes do dry-run:
Get-ChildItem -LiteralPath $manifests -Filter '*.yaml' | Select-String 'REPLACE_WITH_|example.invalid'
kubectl apply --dry-run=server -f $manifests
kubectl apply -f "$manifests/external-secrets.yaml"
kubectl -n fiap-execution wait --for=condition=Ready externalsecret/fiap-execution-database-credentials externalsecret/fiap-execution-jwt-signing-key --timeout=180s
kubectl apply -f "$manifests/config-maps.yaml"
kubectl apply -f "$manifests/services.yaml"
kubectl apply -f "$manifests/deployment.yaml"
kubectl apply -f "$manifests/hpa.yaml"
kubectl -n fiap-execution rollout status deployment/fiap-execution-deployment --timeout=360s
kubectl -n fiap-execution get pods,services,hpa
kubectl -n fiap-execution top pods
```

Mudanças em ConfigMap/Secret consumido por variável exigem recriar pods: depois de confirmar a sincronização, use `kubectl -n fiap-execution rollout restart deployment/fiap-execution-deployment` e acompanhe o rollout. Não force `replicas` ao reaplicar: o HPA controla a escala.

## Operação e aceite

HPA: mínimo 1, máximo 3, CPU alvo 70% do request, estabilização de redução de 300 segundos. Deployment inicia com uma réplica e usa RollingUpdate, sem indisponibilidade planejada e com um pod adicional durante a troca. Os nodes precisam comportar a escala e o surge. CPU não representa backlog Kafka; uma única partição limita o paralelismo do consumidor, mesmo com mais pods.

Startup/liveness usam `/api/execution/v1/health/live`; readiness usa `/api/execution/v1/health/ready`. Startup tolera cinco minutos e a janela de encerramento é de 60 segundos. Falha de banco/Kafka retira readiness; o consumidor pode encerrar o host em falha, preservando seu comportamento atual. Reinícios não substituem reconciliação de mensagens problemáticas.

OpenTelemetry está habilitado: `OTEL_EXPORTER_OTLP_PROTOCOL=grpc` e `http://otel-collector.fiap-observability.svc:4317`. Traces, métricas e logs usam o nome **`oficina-execution`**; a licença New Relic não é entregue à API. Recursos iniciais: request 100m/128Mi e limit 500m/512Mi; execução sem root e sem token Kubernetes.

Após disponibilizar as dependências, confirme probes, chamada autenticada via frontend e sinais no New Relic filtrados por `service.name = 'oficina-execution'`. Valide HPA sob carga e retorno ao mínimo após estabilização; em ambiente isolado, confirme readiness indisponível sem reinícios causados pela liveness quando o banco/broker falhar. Nenhuma dessas verificações de cluster é comprovada apenas pelo YAML.

O roteiro integrado e a decisão aceita estão no repositório OS, em `k8s/README.md` e `docs/ADRs/ADR-022-kubernetes-microsservicos.md`.
