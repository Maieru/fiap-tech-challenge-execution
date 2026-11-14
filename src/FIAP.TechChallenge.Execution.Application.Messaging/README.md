# Execution Application.Messaging

Contratos públicos de integração do Execution, em .NET 10. O pacote FIAP.TechChallenge.Execution.Application.Messaging declara versão 0.1.0, sem dependência de domínio, persistência, Kafka ou chassi. A versão do contrato no envelope é 1 e independe da versão NuGet.

## Eventos

| Payload | Type | Tópico padrão |
| --- | --- | --- |
| `DiagnosticoConcluido` | `execution.diagnostico-concluido` | `oficina.execution.events.v1` |
| `DiagnosticoIniciado` | `execution.diagnostico-iniciado` | `oficina.execution.events.v1` |
| `ExecucaoCancelada` | `execution.execucao-cancelada` | `oficina.execution.events.v1` |
| `ExecucaoConcluida` | `execution.execucao-concluida` | `oficina.execution.events.v1` |
| `ExecucaoIniciada` | `execution.execucao-iniciada` | `oficina.execution.events.v1` |

Use ExecutionMessaging.Topic, a constante Version e a constante Type do payload. A chave Kafka é OrdemServicoId no formato UUID com hífens.

O publicador usa `MessageEnvelope<T>` do chassi, com MessageId estável em reentrega, instante UTC, correlação, causação e contexto de trace. JSON é camelCase, IDs são UUID e valores monetários são decimais em BRL. O pacote contém somente payloads/constantes: transporte, serialização, validação, offsets, idempotência e coordenação com banco pertencem aos serviços.

DiagnosticoConcluido inclui snapshots dos serviços e peças, suas quantidades e ValorTotal. Não trate campo monetário ausente como zero: o consumidor exige um número decimal válido; zero explícito é permitido.

## Consumo, build e teste

No projeto consumidor, selecione uma versão disponível no feed:

```powershell
dotnet add package FIAP.TechChallenge.Execution.Application.Messaging --version 0.1.0
```

Na raiz do repositório produtor, com .NET SDK 10:

```powershell
dotnet pack src/FIAP.TechChallenge.Execution.Application.Messaging -c Release -o artifacts/packages
dotnet test tests/FIAP.TechChallenge.Execution.Application.Messaging.Tests -c Release
```

Para testar candidatos, use versão NuGet inédita e passe `-p:PackageVersion=<versao-candidata>` ao pack. A tag de publicação é messaging-vX.Y.Z, no workflow publish-messaging.yml, com commit integrado em main, NUGET_USER e Trusted Publishing configurados. Não confunda gerar o pacote localmente com publicá-lo.

## Compatibilidade

Não renomeie tipos/campos nem altere seu significado dentro da versão 1. Campos novos precisam ser opcionais para os consumidores existentes. Mudança incompatível exige nova versão de contrato e migração explícita. Uma versão NuGet nova não autoriza mudar silenciosamente a semântica do envelope.

Consulte o [padrão de mensageria](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/arquitetura/mensageria.md) para validação, publicação e reconciliação.
