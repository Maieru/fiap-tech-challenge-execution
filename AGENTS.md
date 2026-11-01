# Orientações para agentes

- Não fazer commits sem autorização explícita após apresentar as alterações.
- Toda decisão arquitetural relevante deve acompanhar a implementação em docs/ADRs; escolhas técnicas em docs/RFCs. Atualizar os índices.
- Catálogos de decisões contêm apenas registros com status Aceita; propostas permanecem em discussões ou PRs.
- Manter domínio e aplicação independentes de Kafka e MongoDB concretos. Consumidores delegam para casos de uso.
- Testes NUnit com múltiplos asserts independentes devem usar Assert.Multiple.
- Testes HTTP usam endpoints reais e autenticação real, sem mocks de casos de uso ou repositórios.
- Integrações de persistência e mensageria usam serviços reais e dados isolados; não acessar bancos ou volumes de desenvolvimento.
- A implementação foi dividida em três etapas com revisão ao final de cada uma: contratos/padrão; Execution; integração com OS.
