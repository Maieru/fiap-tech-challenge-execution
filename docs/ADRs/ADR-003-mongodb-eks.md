# ADR-003 — MongoDB no EKS do laboratório AWS

- **Status:** Aceita
- **Data:** 2026-11-07

## Contexto e decisão

A conta recusou a criação de DocumentDB por restrição do Free Plan. O usuário escolheu substituir esse serviço por MongoDB no EKS. Execution usa MongoDB 8.0 tanto na AWS quanto localmente. Domínio, aplicação, contratos e invariantes permanecem independentes da tecnologia concreta.

O servidor é um StatefulSet de um membro, replica set rs0, com autenticação, keyfile, TLS/CA própria e PVC EBS gp3 criptografado de 20 GiB. A aplicação usa usuário readWrite exclusivo em fiap_execution, retryWrites=true, leitura do primário e sessões MongoDB nativas. Mongo:CaCertificatePath configura TLS independentemente de Mongo:UseDocumentDb, desabilitado no EKS. Cadeia, validade e hostname continuam verificados. Índices e transações são verificados pela própria imagem em banco temporário antes do deploy.

## Alternativas e consequências

DocumentDB era a escolha inicial, mas exigiria habilitar o plano pago. Aurora PostgreSQL não substitui a API MongoDB existente. Operar MongoDB transfere TLS, armazenamento e disponibilidade à equipe. Um membro permite transações, sem redundância; o laboratório aceita indisponibilidade no reagendamento, vínculo do EBS à AZ e perda dos dados no destroy. Não há migração dos dados antigos nem fallback automático. Senhas/chaves geradas também ficam no state sensível; a API recebe somente sua credencial e a CA pública.

Evidências: PersistenceCompatibility, MongoTls, MongoUnitOfWork, perfil Kubernetes, ensaio real test-mongodb.mjs do Infra e [guia operacional central](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/operacao/plataforma-aws.md). Detalhes técnicos em [RFC-001](../RFCs/RFC-001-mongodb-tls.md). O ensaio local não comprova montagem EBS/IAM no EKS; a execução central valida o ambiente remoto.
