# FIAP Tech Challenge — Execution

Serviço independente de diagnóstico, catálogo, estoque e execução, em .NET 10 com MongoDB próprio e Kafka.

## Estado da entrega

| Etapa | Conteúdo | Estado |
| --- | --- | --- |
| 1 | Contratos NuGet, interface e padrão comum | Revisada |
| 2 | API, domínio, MongoDB, catálogo, estoque e consumidor Execution | Implementada para revisão |
| 3 | Publicação/recepção no OS, aprovação administrativa e BDD distribuído | Pendente |

O Execution recebe OrdemServicoCriada, ExecucaoAutorizada e CancelamentoSolicitado por Kafka.
A API OS ainda não publica nem consome esses eventos. Os testes desta etapa produzem as mensagens pelo contrato público,
sem exigir o OS em execução. Billing, pagamentos, frontend, migração de dados e AWS ficam para depois.

Consulte as [evidências da etapa 2](docs/revisao-etapa2.md), a
[arquitetura](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/arquitetura/execution.md),
o [padrão de mensageria](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/arquitetura/mensageria.md)
e a [ADR-015](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/ADRs/ADR-015-execution-mongodb.md).

## Dependências e restauração

Versões fixadas: Maieru.FiapTechChallenge.Chassis 1.1.1, Maieru.FiapTechChallenge.Messaging.Kafka 0.1.1
e FIAP.TechChallenge.OS.Application.Messaging 0.1.0. Os contratos locais do Execution usam ProjectReference dentro deste repositório.
Não há referências de código-fonte nem acesso aos bancos de outros serviços.

O contrato OS 0.1.0 ainda precisa ser publicado para build/CI em ambiente limpo.
Até essa publicação, use o nupkg gerado no repositório principal:

```powershell
# Na raiz do repositório principal, gere somente o contrato:
dotnet pack src/Services/OrdensServico/FIAP.TechChallenge.OS.Application.Messaging -c Release -o artifacts/packages

# Na raiz deste repositório:
./scripts/Restore-Local.ps1 -OsMessagingPackage ../FiapTechChallengeFase1/artifacts/packages/FIAP.TechChallenge.OS.Application.Messaging.0.1.0.nupkg
dotnet build Execution.slnx -c Release --no-restore
```

O roteiro apenas valida/copia o nupkg para artifacts/packages e gera configuração NuGet local ignorada pelo Git.
Não compila outro repositório nem escolhe versões dinamicamente.
Depois da publicação do contrato OS, dotnet restore funciona com NuGet.Config público, sem preparação local.
O Dockerfile aceita os pacotes opcionais de artifacts/packages; uma imagem em ambiente limpo também exige o contrato publicado.

## Executar com Compose

Requer Docker Desktop com containers Linux, PowerShell 7 e .NET SDK 10 para testes.

```powershell
Copy-Item .env.example .env
# Preencha issuer, audience e signing key iguais ao emissor administrativo.
docker compose up --build -d
```

| Recurso | Endereço |
| --- | --- |
| API | http://localhost:8082/api/execution/v1 |
| Scalar | http://localhost:8082/scalar/v1 |
| OpenAPI | http://localhost:8082/openapi/v1.json |
| Liveness | /api/execution/v1/health/live |
| Readiness | /api/execution/v1/health/ready |
| MongoDB local | mongodb://127.0.0.1:27018/?directConnection=true&replicaSet=execution-rs |
| Banco exclusivo | fiap_execution |
| Kafka para processos no host | 127.0.0.1:19093 |
| Kafka para a API no Compose | kafka:9092 |

O Compose inicializa MongoDB como replica set de um nó e cria os dois tópicos, com uma partição.
MongoDB local não exige credenciais e Kafka usa PLAINTEXT; ambos são expostos somente em loopback neste ambiente de desenvolvimento.
JWT é exigido nos endpoints administrativos e validado sem chamar o legado.
OpenAPI/Scalar são habilitados somente em Development/Testing. OTLP é opcional.

Os volumes fiap-execution_execution_mongo_data e fiap-execution_execution_kafka_data pertencem a este ambiente.
docker compose down preserva os dados. O consumidor não reinicia automaticamente: corrija a causa e reinicie a API,
mantendo o grupo para reprocessar o offset pendente.

Para executar fora do container, configure Mongo__ConnectionString, Mongo__DatabaseName,
Kafka__BootstrapServers e Jwt__Issuer/Jwt__Audience/Jwt__SigningKey, depois use dotnet run --project src/FIAP.TechChallenge.Execution.API.
A conexão MongoDB precisa permitir transações. Coleções e índices são criados na inicialização.

## API

Prefixo /api/execution/v1. IDs da rota prevalecem sobre IDs enviados no corpo.

| Rota | Operações |
| --- | --- |
| /servicos | POST e GET paginado |
| /servicos/{id} | GET, PUT e DELETE lógico |
| /servicos/{id}/tempo-medio | GET |
| /pecas-insumos | POST e GET paginado; filtro codigo |
| /pecas-insumos/{id} | GET, PUT e DELETE lógico |
| /pecas-insumos/{id}/entrada-estoque | PUT com quantidade positiva |
| /ordens | GET; status, pageNumber e pageSize |
| /ordens/{id} | GET |
| /ordens/{id}/iniciar-diagnostico | PUT |
| /ordens/{id}/addservico | POST com servicoId e quantidade |
| /ordens/{id}/addpecainsumo | POST com pecaInsumoId e quantidade |
| /ordens/{id}/solicitar-aprovacao | PUT |
| /ordens/{id}/servicos/{itemId}/concluir | PUT com tempoGastoMinutos |
| /ordens/{id}/finalizar | PUT |

Paginação começa em 1/10 e aceita pageSize entre 1 e 100.
Respostas usam 201 para criações, 200 para consultas/alterações, 204 para exclusões,
400 para regras inválidas, 404 para inexistentes/inativos e 409 para duplicidades/conflitos de concorrência.
Erros de negócio usam error/errorCode; falhas técnicas usam ProblemDetails.

A consulta da ordem retorna execucao (snapshot, itens e datas) e valorTotal.
Não há endpoint HTTP para criar a ordem, autorizar ou cancelar execução: essas entradas pertencem ao OS e chegam por Kafka.

Serviços e peças só entram durante diagnóstico; a inclusão da peça e sua baixa são atômicas.
Alterações/inativação do catálogo preservam os snapshots da ordem.
Cancelar enquanto aguarda aprovação devolve estoque uma única vez, inclusive para peças inativadas.
Concluir serviço exige tempo positivo; finalizar exige todos os serviços concluídos.
O tempo médio usa os itens de serviço efetivamente concluídos, conforme a regra extraída do legado.

## Mensageria e transação

O BackgroundService usa switch explícito, escopo por mensagem e casos de uso na Application.
Tópicos padrão: oficina.os.events.v1 e oficina.execution.events.v1; grupo oficina-execution-v1.
Kafka:InputTopic e Kafka:GroupId permitem isolamento nos testes.

Casos de uso com publicação gravam na transação MongoDB, aguardam o Kafka e depois fazem commit.
Falha de publicação aborta os dados locais. Se a mensagem for entregue e o commit falhar, ela pode ficar órfã:
esse limite é aceito e está coberto por teste, sem promessa de commit distribuído.
Deduplicação é gravada junto dos dados; offset só é confirmado depois do commit.
Em erro, o host encerra com saída não zero e registra tópico/partição/offset/MessageId/correlação.
Sem DLQ ou retry de aplicação. Consulte o padrão canônico para correção e reconciliação.

## Testes, imagem e CI

```powershell
dotnet test Execution.slnx -c Release --no-restore --filter 'TestCategory!=ContainerImage' --settings coverage.runsettings --collect:'XPlat Code Coverage;Format=cobertura,opencover' --results-directory artifacts/test-results

docker build -t fiap-execution:local .
$env:EXECUTION_TEST_IMAGE = 'fiap-execution:local'
$env:EXECUTION_OPENAPI_OUTPUT = (Join-Path (Get-Location) 'artifacts/execution-openapi.json')
dotnet test tests/FIAP.TechChallenge.Execution.Integration.Tests -c Release --filter 'TestCategory=ContainerImage'
```

NUnit/Moq nas unidades; HTTP com JWT, MongoDB replica set e Kafka reais via Testcontainers na integração.
Preparação de catálogo ocorre por HTTP e ordens entram por Kafka. Os testes não usam os volumes de desenvolvimento.

CI tem build, testes, cobertura mínima de 80%, imagem, exportação OpenAPI e análise Sonar.
Configure SONAR_TOKEN (secret) e SONAR_PROJECT_KEY (variable), organização maieru, no repositório Execution.
Os workflows não foram executados remotamente nesta revisão.

## Publicação e Git

O pacote de contratos Execution.Application.Messaging permanece na versão 0.1.0.
O workflow publish-messaging.yml usa tag messaging-vX.Y.Z em commit revisado de main, variável NUGET_USER e NuGet Trusted Publishing.
O pacote contém somente contratos, sem MongoDB/Kafka/domínio.

O Git deste checkout é independente. Nenhum commit, push, tag ou publicação foi feito nesta etapa.
O remote do Execution ainda não foi configurado.
