# Entrega Execution

A entrega AWS é chamada pelo workflow manage-platform.yml do repositório principal. publish-image.yml e deploy-eks.yml são reutilizáveis, sem execução manual independente, para compartilhar a trava do ambiente.

Inputs comuns: source_sha completo, role_arn e component. Deploy também recebe image_digest, kafka_bootstrap_servers, jwt_issuer e jwt_audience. REPOSITORIES_TOKEN é encaminhado explicitamente quando necessário. Não usar o GITHUB_SHA do chamador para identificar outro serviço: o catálogo components.json declara o repository e SOURCE_SHA identifica o checkout/CI/imagem/manifestos.

Publicação exige CI aprovado em main, reutiliza a tag sha-<commit> quando presente e retorna image_digest/image_uri. Deploy confere digest, faz dry-run dos YAMLs, sincroniza ExternalSecrets, executa migrations de OS/Billing em Jobs separados e acompanha rollout. Não aplica migrations no startup normal dos pods.

As credenciais são importadas do Secrets Manager; as APIs montam o bundle AWS de CAs criado pelo Infra em /certs. Execution habilita o perfil DocumentDB somente no Kubernetes; local e testes continuam usando MongoDB.

Executar npm ci --prefix scripts/delivery e npm test --prefix scripts/delivery para validar as ferramentas. Consultar o [guia operacional central](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/operacao/plataforma-aws.md) antes de ativar o laboratório e seu agendamento destrutivo.
