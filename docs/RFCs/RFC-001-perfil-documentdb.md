# RFC-001 — TLS, sessões e verificação DocumentDB

- **Status:** Aceita
- **Data:** 2026-11-07

## Contexto e decisão

MongoDB e DocumentDB não são servidores equivalentes. O perfil Mongo:UseDocumentDb=true configura TLS, retryWrites=false, ReadPreference.Primary e ClientSessionOptions.CausalConsistency=false. Mongo:CaCertificatePath aponta para o bundle público AWS montado pelo Infra. Cadeia, validade e hostname são verificados; não usar AllowInsecureTls.

O modo --verify-persistence não inicia HTTP nem consumidores. Exige banco fiap_compat_<identificador>, inicializa as mesmas coleções/índices e verifica duplicidade parcial, commit, rollback com deduplicação e conflito de escrita. Apaga somente esse banco isolado ao finalizar, usando uma credencial administrativa restrita ao Job de bootstrap. Os pods normais recebem usuário readWrite somente em fiap_execution.

## Alternativas e consequências

Desabilitar TLS/validação reduziria configuração, mas não foi adotado. Ignorar diferenças de sessão transferiria falhas ao rollout; o gate real bloqueia esse cenário. Os testes existentes continuam com MongoDB; o gate não substitui validação funcional ponta a ponta, executada pelo smoke central.

Evidências: DocumentDbTlsTests, PersistenceCompatibility e Job initialize-databases do DB. Resultado remoto depende da execução AWS após publicação/bootstrap.

Os testes locais cobrem também bundle ausente/vazio, configuração DocumentDB habilitada/desabilitada e obrigatoriedade da CA; MongoUnitOfWorkProfileTests confirma a opção de sessão e commit em MongoDB real. PersistenceCompatibilityTests executa o modo CLI sem JWT/Kafka, verifica preservação de bancos fora do prefixo permitido e limpeza do banco temporário em cancelamento ou índice incompatível. Esses testes não substituem o gate no DocumentDB real.
