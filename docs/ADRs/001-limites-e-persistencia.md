# ADR-001 — Execution independente e MongoDB

Status: Aceita. Data: 2026-10-31.

## Contexto

O monólito concentra cadastros, estoque e todas as etapas da ordem. A migração precisa preservar as regras e permitir coexistência, com persistência NoSQL própria para a operação.

## Decisão

Execution possui catálogo, estoque, reservas e agregado operacional próprios. OS conserva clientes, veículos, estado geral e histórico. Quatro projetos de produção separam domínio, aplicação, infraestrutura e API; nenhum referencia código ou banco dos outros serviços. O chassi 1.0.2 contém somente recursos técnicos.

MongoDB guarda valores como Decimal128, snapshots dos itens, versão otimista e uma execução por OrdemServicoId. Transações em replica set abrangem execução, estoque, reservas, idempotência, Inbox e Outbox. Development/CI usam um nó com dados próprios. Peças têm código normalizado e índice único parcial para ativos. Exclusão lógica permite reutilizar o código com outro ID, preservando reservas e snapshots antigos.

## Alternativas

Compartilhar o banco do OS acoplaria implantação e transações entre serviços. PostgreSQL no Execution não atenderia à escolha NoSQL. Banco MongoDB standalone não oferece as transações necessárias. Um pacote de domínio compartilhado exigiria evolução simultânea dos serviços.

## Consequências e evidências

Duplicação transitória do domínio e casos de uso de catálogo está descrita em [reaproveitamento](../reaproveitamento.md). `MongoSession`, `MongoBootstrap` e repositórios implementam transação, índices e concorrência. Testes HTTP usam MongoDB real com replica set e verificam rollback, disputa por estoque e snapshots. O replica set de um nó é destinado ao ambiente local/CI; não oferece alta disponibilidade de produção.
