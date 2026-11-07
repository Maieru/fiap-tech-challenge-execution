# Publicação ECR e deploy EKS

Este repositório oferece duas Actions independentes, iniciadas em **Actions → Run workflow**, na branch **main**:

- `publish-image.yml` — verifica o último CI do commit, constrói a imagem e publica a tag imutável `sha-<SHA completo>`.
- `deploy-eks.yml` — verifica CI, resolve a imagem no ECR para `repository@sha256:...`, renderiza uma cópia dos manifestos e aplica no EKS. Não constrói ou publica imagem.

O componente é fixo: `execution`.

O CI existente continua executando testes/cobertura/quality gate e agora também exige os testes desta ferramenta. Nenhum workflow de entrega roda automaticamente em push/PR. A publicação/deploy são bloqueados enquanto o último CI do mesmo SHA em main estiver pendente, falho ou cancelado.

## Preparação da infraestrutura

Revisar e aplicar separadamente os estágios Infra na ordem **bootstrap → aws-resources → kubernetes-configs**. O primeiro cria as novas roles OIDC, o segundo cria ECRs/access entries EKS e o terceiro os Roles/RoleBindings dos namespaces. Esta mudança não executa esses applies.

Em `bootstrap`, o output `application_delivery_role_arns` fornece `execution_publish` e `execution_deploy`. Configure o primeiro em `AWS_PUBLISH_ROLE_ARN` e o segundo em `AWS_DEPLOY_ROLE_ARN` nas **Actions variables** do repositório. As roles confiam apenas neste repositório na branch configurada (main no laboratório), com audience sts.amazonaws.com. Não configure GitHub Environment sem adaptar a trust policy OIDC, pois o subject muda.

A role publish escreve apenas no ECR do serviço; deploy apenas consulta imagens/cluster e obtém permissões Kubernetes por grupo RBAC, restritas a fiap-execution. O grupo novo não possui leitura de Secrets nem permissão para criar namespaces/CRDs/RBAC. As roles antigas são preservadas para compatibilidade operacional e não devem ser usadas como as novas variáveis.

O ECR é `fiap-ecr-execution-repository`; o cluster é `fiap-eks-cluster`. Os runners precisam alcançar seu endpoint e as APIs AWS; se o endpoint se tornar privado, forneça runner com conectividade à VPC.

## Variables e pré-requisitos

| Actions variable | Valor |
| --- | --- |
| AWS_PUBLISH_ROLE_ARN | Output `execution_publish` do bootstrap |
| AWS_DEPLOY_ROLE_ARN | Output `execution_deploy` do bootstrap |
| AWS_REGION | Opcional, padrão us-east-1; deve coincidir com o Infra |
| KAFKA_BOOTSTRAP_SERVERS | Brokers e portas alcançáveis pelos pods |
| JWT_ISSUER | Mesmo emissor nas três APIs |
| JWT_AUDIENCE | Mesma audience nas três APIs |

As três últimas variáveis são necessárias para APIs, não para publicar imagens ou entregar somente frontend. Senhas, connection strings e chave JWT continuam exclusivamente nos Secrets sincronizados por External Secrets; não são entradas das Actions.

Antes do deploy, disponibilize bancos/usuários exclusivos, os quatro tópicos Kafka, novos segredos AWS e acesso IAM do operador External Secrets. Para OS/Billing, aplique migrations de schema compatíveis antes do rollout; as réplicas mantêm ApplyMigrations=false. Collector/New Relic, namespaces, SecretStores e metrics-server devem estar prontos. O frontend exige os três upstreams disponíveis. As Actions não provisionam essas dependências.

## Execução e recuperação

1. Aguarde CI aprovado em main e execute **Publish image to ECR** para o commit desejado. O resumo registra tag e digest. Se a tag já existe, sua publicação é verificada e reutilizada; somente ImageNotFound permite construir. Falhas de IAM/rede não são tratadas como tag ausente.
2. Execute **Deploy manifests to EKS**. `image_sha` vazio seleciona a imagem do commit desta execução; para entregar uma imagem anterior, informe seu SHA completo, também com CI aprovado e imagem publicada. Os **manifestos são os do commit da execução atual**; o resumo registra ambas as referências.
3. O deploy instala kubectl da mesma versão minor do cluster, verificando checksum, e usa kubeconfig temporário. Valida todos os YAMLs no servidor antes de aplicar, sincroniza cada ExternalSecret e aguarda um novo refresh com Ready antes de entregar os workloads.
4. ConfigMaps e Services precedem os Deployments; HPA/Ingress vêm depois. Anotações de checksum e execução renovam pods mesmo com a mesma imagem/configuração, para consumir segredos rotacionados. O HPA mantém controle de replicas.
5. Aguarde rollout concluído; valide autenticação, rotas, escala e ingestão New Relic no ambiente.

Concorrência é serializada por componente; uma execução em andamento não é cancelada por outra. Aplicação de vários recursos não é atômica: falha/timeout encerra o job sem rollback automático. Corrija a causa e reexecute; para retornar uma imagem, use `image_sha` anterior somente se for compatível com os schemas e manifestos atuais. Não há downgrade de banco ou retirada de recursos legados.

## Verificação local

```powershell
npm ci --prefix scripts/delivery --ignore-scripts --no-audit --no-fund
npm test --prefix scripts/delivery
```

Os testes cobrem CI pendente/falho/SHA incorreto, erro ECR distinto de ImageNotFound, parâmetros ausentes, quoting de configurações, imagem por digest, preservação dos fontes/HPAs/segredos e renovação dos pods. actionlint valida os workflows; Terraform fmt/validate valida Infra. Testes locais não comprovam execução remota das Actions, IAM efetivo ou rollout no EKS.
