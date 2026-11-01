# ADR-001 — Repositório Execution independente e contratos próprios

- **Status:** Aceita
- **Data:** 2026-11-01
- **Escopo:** organização e distribuição

## Contexto

O Execution terá ciclo de evolução independente do OS e do legado. O planejamento foi aprovado em três etapas com revisão entre elas.

## Decisão

Manter um repositório Git local exclusivo em FiapTechChallengeExecution, solução Execution.slnx e pacote
FIAP.TechChallenge.Execution.Application.Messaging 0.1.0.
O pacote publica os contratos DiagnosticoIniciado, DiagnosticoConcluido, ExecucaoIniciada, ExecucaoConcluida e ExecucaoCancelada.
O diagnóstico transporta snapshots de serviços e peças, sem entidades ou regras compartilhadas.

## Alternativas consideradas

- Novo projeto dentro do monorepo OS/legado: não atende ao repositório próprio solicitado.
- Kernel de domínio comum: acrescentaria acoplamento desnecessário entre serviços.

## Consequências

Build e testes dos contratos não dependem dos outros checkouts. O workflow de publicação usa tag messaging-vX.Y.Z
e NuGet Trusted Publishing, sem chaves permanentes.
A primeira etapa entregou os contratos. API, MongoDB e consumidor foram acrescentados na [etapa 2](../revisao-etapa2.md);
o fluxo distribuído com o OS permanece para a etapa 3.
Nenhum commit, remote, tag ou publicação foi criado durante essas implementações locais.

## Evidências

- [Solução](../../Execution.slnx).
- [Contratos](../../src/FIAP.TechChallenge.Execution.Application.Messaging/README.md).
- [Testes](../../tests/FIAP.TechChallenge.Execution.Application.Messaging.Tests/ContractTests.cs).
- [Padrão canônico](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/arquitetura/mensageria.md).
