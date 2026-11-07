# ADR-003 — DocumentDB gerenciado no laboratório AWS

- **Status:** Aceita
- **Data:** 2026-11-07

## Contexto e decisão

O usuário escolheu bancos gerenciados, preservando Kafka no EKS. Execution usa DocumentDB 5.0 na AWS e MongoDB 8.0 localmente. Domínio, aplicação, contratos e invariantes permanecem independentes da tecnologia concreta.

TLS valida CA/hostname, retryWrites é desabilitado, leituras usam primário e sessões desabilitam consistência causal no perfil DocumentDB. Índices e transações são verificados pela própria imagem em banco temporário antes do deploy. Não há fallback automático nem relaxamento de rollback, concorrência ou deduplicação.

## Alternativas e consequências

MongoDB no EKS preservaria o servidor existente, mas exigiria operar seu replica set; o usuário manteve a preferência gerenciada. DocumentDB tem diferenças funcionais e requer validação real, além dos testes locais MongoDB. Configuração não comprova compatibilidade operacional antes da execução do gate na AWS.

Evidências: PersistenceCompatibility, DocumentDbTls, MongoUnitOfWork, perfil Kubernetes e [guia operacional central](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/operacao/plataforma-aws.md). Detalhes técnicos em [RFC-001](../RFCs/RFC-001-perfil-documentdb.md).
