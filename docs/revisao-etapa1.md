# Revisão da etapa 1 — contratos e padrão

Verificação local em 2026-11-01. Nenhum commit, push, tag ou publicação NuGet foi executado.
O Execution possui Git local independente, sem remote e sem commits.

## Alterações para revisar

- Chassi 1.1.0: interface IMessagingService, mantendo o envelope existente.
- Pacote Maieru.FiapTechChallenge.Messaging.Kafka 0.1.0: publicação confirmada, JSON camelCase, DI e timeout configurável.
- Pacotes FIAP.TechChallenge.OS.Application.Messaging e FIAP.TechChallenge.Execution.Application.Messaging 0.1.0:
  oito tipos de mensagens e snapshots de diagnóstico, sem dependências de negócio ou Kafka.
- CI, publicação por tags/OIDC, exemplo consumidor NuGet, testes de compatibilidade e documentação de decisões.
- Guia canônico de publicação direta com rollback limitado, consumidores com switch e parada em erro.

O código de negócio OS/legado não foi modificado. As soluções passaram a incluir os projetos de contratos e seus testes.

## Evidências locais

| Verificação | Resultado |
| --- | --- |
| Testes do chassi existente | 11 aprovados |
| Testes do adaptador Kafka | 11 aprovados, incluindo entrega real em Kafka 4.1.0 |
| Contratos OS | 4 aprovados |
| Contratos Execution | 6 aprovados |
| Regressão Application OS | 52 aprovados |
| Regressão Domain OS | 127 aprovados |
| Total executado nessas suítes | 211 aprovados |
| Cobertura de linhas do adaptador Kafka | 100% — 24/24 linhas |
| Cobertura de linhas do chassi | 96,51% — 83/86 linhas |
| Compilação das soluções Chassis e OS | Aprovada |
| Compilação e testes da solução Execution | Aprovados |
| Empacotamento dos quatro NuGets | Aprovado |
| Exemplo Kafka usando PackageReference | Publicou e leu chave, envelope e payload no broker real |
| Consumidor independente dos quatro pacotes | Compilou e validou contratos/envelope/interface, sem ProjectReference |
| Sintaxe dos nove YAMLs alterados/adicionados | Validada com YamlDotNet |
| git diff --check dos repositórios existentes | Sem erros |

O teste Kafka inicialmente detectou resolução IPv6 incompatível com a porta IPv4 exposta.
O broker de testes agora anuncia 127.0.0.1:19092; a suíte completa e o consumidor NuGet passaram após a correção.
O ambiente Kafka é isolado e os tópicos de teste são exclusivos e removidos após cada verificação.

Houve avisos NU1900 de indisponibilidade da consulta de vulnerabilidades NuGet em restores anteriores.
Esses avisos não são resultado de uma análise de segurança concluída.
GitHub Actions, NuGet Trusted Publishing e Sonar não foram executados remotamente.
O build da solução OS não equivale à execução de sua suíte HTTP/PostgreSQL; esta não foi executada nesta etapa.

Relatórios de cobertura/TRX ficam em artifacts/test-results/messaging-final no repositório do chassi.
O executável temporário que consumiu os quatro pacotes fica em artifacts/package-consumer neste repositório;
ele e os pacotes gerados são artefatos locais ignorados pelo Git.

## Limites e próxima etapa

A etapa 1 não implementa API Execution, catálogo/estoque, MongoDB, unidade de trabalho, deduplicação ou consumidores de negócio.
Portanto, ainda não há garantia implementada nem teste de rollback das operações de negócio.
Esses testes serão feitos junto das transações das etapas 2 e 3.

O [padrão canônico](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/arquitetura/mensageria.md)
define esses comportamentos e explicita a possibilidade aceita de mensagem entregue com falha de commit.

Conforme o ponto de revisão aprovado, a próxima etapa é construir o Execution com catálogo, estoque, diagnóstico,
execução e MongoDB. A integração com o OS será a terceira etapa.
