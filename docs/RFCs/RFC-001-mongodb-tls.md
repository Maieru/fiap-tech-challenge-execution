# RFC-001 — TLS e verificação MongoDB

- **Status:** Aceita
- **Data:** 2026-11-07

## Contexto e decisão

MongoDB substitui DocumentDB na AWS por decisão do usuário após a restrição do Free Plan. O perfil implantado usa Mongo:UseDocumentDb=false e preserva retryWrites=true, leitura do primário e sessões nativas do replica set rs0. O suporte ao perfil anterior permanece somente para compatibilidade; não é um fallback de deploy.

Mongo:CaCertificatePath aplica a CA configurada independentemente do perfil de servidor. MongoTls exige cadeia válida e hostname correto, rejeita certificados expirados e não permite AllowInsecureTls. A CA pública própria é montada em /certs/ca.pem pelo ConfigMap mongodb-ca. O cliente não recebe keyfile, chave privada ou senha administrativa do servidor.

O modo --verify-persistence não inicia HTTP nem consumidores. Exige banco fiap_compat_<identificador>, inicializa as mesmas coleções/índices e verifica duplicidade parcial, commit, rollback com deduplicação e conflito de escrita. Apaga somente esse banco isolado ao finalizar, usando uma credencial administrativa restrita ao Job de bootstrap. Os pods normais recebem usuário readWrite somente em fiap_execution.

## Alternativas e consequências

Desabilitar TLS/validação reduziria configuração, mas exporia credenciais e não foi adotado. Usar o perfil DocumentDB para ativar TLS também alteraria retryWrites e consistência causal desnecessariamente; a configuração da CA agora é independente. O replica set de um membro atende às transações locais do laboratório, sem alta disponibilidade.

MongoTlsTests e MongoConfigurationTests cobrem trust, hostname, validade, bundle ausente/vazio e CA com perfil MongoDB nativo. MongoUnitOfWorkProfileTests confirma sessões e commit em MongoDB real. PersistenceCompatibilityTests executa o CLI sem JWT/Kafka e verifica proteção/limpeza do banco temporário. test-mongodb.mjs do Infra testa os scripts reais com TLS, autenticação, grants, transações e reinício em Docker isolado; EXECUTION_TEST_IMAGE inclui o gate real da API. O gate/smoke remoto continua obrigatório após publicação/bootstrap.
