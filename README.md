# FIAP Tech Challenge — Execution

Serviço independente de diagnóstico, catálogo, estoque e execução, em .NET 10 com MongoDB próprio e Kafka.

O Execution recebe OrdemServicoCriada, ExecucaoAutorizada e CancelamentoSolicitado por Kafka.
A API OS publica essas entradas e recebe as cinco confirmações do Execution. Os testes isolados produzem
mensagens pelo contrato público; Distributed.slnx verifica as duas imagens reais juntas.
O serviço concentra diagnóstico e execução; clientes, veículos e decisões administrativas pertencem ao OS.

Consulte a
[arquitetura](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/arquitetura/execution.md),
o [padrão de mensageria](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/arquitetura/mensageria.md)
e as [evidências de validação](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/operacao/validacao-execution.md)
no repositório principal.

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
Issuer e audience usam a seção Jwt de IConfiguration. A chave vem explicitamente da variável Jwt__SigningKey,
com pelo menos 32 bytes UTF-8; ela é registrada uma única vez na DI e injetada na validação JWT.
No Compose, JWT_SIGNING_KEY do .env é mapeada para Jwt__SigningKey; não há fallback da chave para arquivo de configuração.
Provisione segredos externamente; não versione a chave.
OpenAPI/Scalar são habilitados somente em Development/Testing. OTLP é opcional.

Os volumes fiap-execution_execution_mongo_data e fiap-execution_execution_kafka_data pertencem a este ambiente.
docker compose down preserva os dados. O consumidor não reinicia automaticamente: corrija a causa e reinicie a API,
mantendo o grupo para reprocessar o offset pendente.

Para executar fora do container, configure Mongo__ConnectionString, Mongo__DatabaseName,
Kafka__BootstrapServers e Jwt__Issuer/Jwt__Audience/Jwt__SigningKey, depois use dotnet run --project src/FIAP.TechChallenge.Execution.API.
A conexão MongoDB precisa permitir transações. Coleções e índices são criados na inicialização.

## Executar as duas APIs e BDD

Gere/restaure os contratos locais conforme os READMEs de cada repositório enquanto os dois pacotes 0.1.0 não estiverem públicos.
Na raiz do repositório principal, construa a imagem OS:

```powershell
docker build -f src/Services/OrdensServico/Dockerfile -t fiap-os:local .
```

Na raiz deste repositório, preencha também OS_DB_PASSWORD no .env, mantendo JWT igual nas duas APIs:

```powershell
docker compose -f compose.yaml -f compose.integrated.yaml up --build -d
# OS: localhost:8081; Execution: localhost:8082; Kafka compartilhado: localhost:19093
docker compose -f compose.yaml -f compose.integrated.yaml down

docker build -t fiap-execution:local .
$env:OS_TEST_IMAGE = 'fiap-os:local'
$env:EXECUTION_TEST_IMAGE = 'fiap-execution:local'
dotnet test Distributed.slnx -c Release --logger trx --results-directory artifacts/distributed
```

O Compose integrado recebe OS_IMAGE como imagem pronta, mantém bancos/volumes exclusivos e compartilha somente Kafka.
Não use o Compose independente do OS ao mesmo tempo: ele tem outro broker e usa a mesma porta HTTP.
Down preserva os três volumes; não use -v para manter dados locais.

O BDD usa Testcontainers com rede/volumes descartáveis, portas aleatórias e JWT real.
Não exige os Composes de desenvolvimento ligados nem referências a projetos das APIs.
Os quatro cenários verificam conclusão, cancelamento/replay, decisões concorrentes e retomada do Execution,
incluindo histórico, estoque, snapshots e correlação/causação/trace.
O workflow manual distributed.yml aceita imagens publicadas acessíveis ao runner. Prefira referências por digest.
Os pipelines locais de cada serviço continuam independentes. Os dois contratos precisam estar publicados para CI limpo.

O padrão e as decisões da integração ficam no repositório principal:
[ADR-016](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/ADRs/ADR-016-integracao-os-execution.md),
[RFC-009](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/RFCs/RFC-009-integracao-os-execution.md)
e [ADR-015](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/ADRs/ADR-015-execution-mongodb.md).

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
Os jobs com MongoDB usam Ubuntu 22.04 para compatibilidade com o kernel do runner.
O CI separa o download das imagens, registra a inicialização dos containers e limita esperas de startup/testes.
Consulte o [diagnóstico de CI](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/operacao/diagnostico-ci-execution.md).
Configure SONAR_TOKEN (secret) e SONAR_PROJECT_KEY (variable), organização maieru, no repositório Execution.
Os resultados das verificações estão nas evidências de validação do repositório principal.

## Publicação dos contratos

O pacote de contratos Execution.Application.Messaging usa a versão 0.1.0.
O workflow publish-messaging.yml usa tag messaging-vX.Y.Z em commit revisado de main, variável NUGET_USER e NuGet Trusted Publishing.
O pacote contém somente contratos, sem MongoDB/Kafka/domínio.
