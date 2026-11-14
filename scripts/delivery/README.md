# Entrega Execution

Ferramentas Node.js chamadas pelos workflows reutilizáveis publish-image.yml e deploy-eks.yml deste repositório. A entrada operacional é manage-platform.yml no repositório principal; esses dois workflows não têm disparo manual independente, preservando a serialização do ambiente.

## Componentes e fontes

[components.json](components.json) define repositório, workflow de CI, contexto Docker, ECR, manifests e namespace:

| component | Namespace | ECR | Manifestos |
| --- | --- | --- | --- |
| `execution` | `fiap-execution` | `fiap-ecr-execution-repository` | `k8s` |

SOURCE_SHA identifica checkout, CI, imagem e YAMLs do serviço. Não use o GITHUB_SHA do chamador para identificar outro repositório. O código rejeita SHA incompleto, divergência do checkout e entrega fora de main.

## Contrato dos workflows

| Input | Publicação | Deploy |
| --- | --- | --- |
| source_sha | SHA completo da origem | Mesmo SHA aprovado |
| role_arn | Role OIDC de publicação | Role OIDC de deploy |
| component | Chave do catálogo | Chave do catálogo |
| image_digest | Retornado como output | Obrigatório; sha256 |
| kafka_bootstrap_servers | — | Broker interno explícito |
| jwt_issuer / jwt_audience | — | Configuração compartilhada entre APIs |

REPOSITORIES_TOKEN é opcional para fontes privadas. Publicação retorna image_digest e image_uri. As roles, região e cluster são configurados pelo fluxo; os valores completos de variables/secrets estão no [guia central](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/operacao/plataforma-aws.md).

## Sequência e garantias

1. verify-ci exige execução aprovada do workflow do componente no SHA selecionado em main.
2. publish constrói/publica no ECR com tag imutável `sha-<commit>`, ou reutiliza a imagem dessa tag, e resolve seu digest.
3. deploy valida namespace e digest, substitui placeholders em cópias temporárias e faz dry-run no servidor Kubernetes.
4. ExternalSecrets são aplicados, forçados a sincronizar e aguardados antes de iniciar pods.
5. OS/Billing executam Job --migrate-only com a conexão e CA do próprio banco. Os pods normais não fazem migrations no startup. Execution depende do gate --verify-persistence já executado pelo estágio DB.
6. ConfigMaps, Services, Deployments, HPA e Ingresses são aplicados na ordem necessária; o rollout é aguardado. A anotação DELIVERY_RUN atualiza os pods mesmo ao reutilizar a imagem.

OS/Billing montam o bundle RDS em /certs/global-bundle.pem; Execution usa a CA MongoDB em /certs/ca.pem, replica set e TLS tanto na AWS quanto nos testes de compatibilidade. As APIs não recebem credenciais administrativas nem licença New Relic.

## Validação local

Na raiz do repositório, com Node.js 22+:

```powershell
npm ci --prefix scripts/delivery --ignore-scripts --no-audit --no-fund
npm test --prefix scripts/delivery
```

Os testes exercitam renderização, validações e contratos com comandos simulados. Eles não publicam imagens nem comprovam IAM, rollout ou comunicação real com AWS. A CLI oferece verify-ci, publish e deploy, mas depende do contexto autenticado do workflow; não é um atalho para aplicar templates sem os pré-requisitos.

## Falhas e retomada

CI ausente, digest divergente, placeholder, namespace inesperado, SecretStore indisponível ou migration malsucedida devem bloquear a entrega. Investigue o job e corrija a causa. Uma nova execução central seleciona os SHAs atuais; reexecutar jobs de uma execução existente conserva os SHAs já resolvidos. Não altere o state ou offsets para esconder falhas.

O laboratório possui agendamento destrutivo; confira ativação, retenção e ordem dos estágios no guia central antes de usá-lo.
