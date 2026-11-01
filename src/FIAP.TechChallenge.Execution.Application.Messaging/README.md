# Execution Application.Messaging

Contratos de integração publicados pelo Execution. Pacote .NET 10 sem dependências de domínio, aplicação, persistência ou Kafka.

Use `ExecutionMessaging.Topic`, `ExecutionMessaging.Version` e a constante `Type` da mensagem para construir o `MessageEnvelope<T>` do chassi. A chave Kafka é o `OrdemServicoId` no formato UUID com hífens.

A versão NuGet inicial é 0.1.0; a versão dos contratos no envelope é 1. São versões independentes. O payload usa JSON camelCase, IDs UUID e valores monetários decimais em BRL. Datas e correlação ficam no envelope.

Não renomeie tipos ou campos nem altere seu significado dentro da versão 1. Campos novos precisam ser opcionais para consumidores antigos. Mudanças incompatíveis exigem nova versão de contrato e migração explícita.

[Padrão de mensageria](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/arquitetura/mensageria.md).
