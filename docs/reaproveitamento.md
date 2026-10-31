# Reaproveitamento do legado

Origem: `Maieru/fiap-tech-challenge`, projetos `FIAP.TechChallenge.Fase1.*`. A extração copia código para projetos próprios, sem referências de projeto cruzadas. A duplicação permanece até a desativação do fluxo antigo, em incremento futuro.

| Componente | Tratamento | Adaptação |
| --- | --- | --- |
| Servico, PecaInsumo | Mantido/adaptado | Validações e mensagens preservadas; persistência externa ao domínio. |
| ServicoDaOrdemDeServico, PecaOuInsumoDaOrdemDeServico | Mantido/adaptado | Snapshots próprios com reidratação para documentos. |
| Result, Error, ErrorCode | Mantido | Mesmo padrão de resultado e classificação HTTP. |
| Interfaces de catálogo | Mantido | Contratos atendidos por repositórios MongoDB. |
| CRUD de serviços e peças, entrada de estoque, tempo médio | Mantido/adaptado | Casos de uso, commands, responses e testes copiados; duplicidade de código passa a 409. |
| Atualizar peça | Adaptado | PUT preserva ativo; somente DELETE inativa. Não permite reativação pelo contrato HTTP. |
| Controllers de catálogo | Adaptado | Novo prefixo, Location por ID, JWT e idempotência na entrada de estoque. |
| OrdemServico | Substituído no Execution | Agregado Execucao próprio preserva regras operacionais, mensagens, snapshots, diagnóstico e conclusão. Identidade externa é OrdemServicoId. |
| Repositórios EF e entidades relacionais | Substituído | Documentos, Decimal128, CAS e transações MongoDB. |
| Reserva/devolução de estoque | Adaptado | Reserva persistida com débito, execução e comando idempotente; devolução transacional inclui inativos. |
| Testes de domínio e aplicação de catálogo | Mantido/adaptado | NUnit/Moq; namespaces próprios, removida criação de OS não utilizada em cenário de serviço. |
| Integração e BDD | Novo | HTTP/JWT reais, MongoDB replica set, Kafka, PostgreSQL e duas imagens sem mocks de casos de uso. |
| Chassi | Reutilizado por NuGet | Versão explícita 1.0.2; nenhum feed local ou pacote de negócio novo. |

Tempo médio considera itens de serviço concluídos pelo Execution, inclusive quando a execução ainda possui outros itens pendentes. Catálogo alterado/inativado não muda snapshots. As diferenças de persistência, separação de estado, autorização temporária, versionamento e mensagens são novas neste incremento; o legado continua operando.
