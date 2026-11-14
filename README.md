# FIAP Tech Challenge — Execution

> A operação AWS usa `manage-platform.yml` no repositório principal. O ciclo provisiona MongoDB e Kafka no EKS, credenciais, observabilidade e workloads; consulte o [guia operacional](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/operacao/plataforma-aws.md). Publicação e deploy deste repositório são workflows reutilizáveis chamados pelo fluxo central.

Serviço independente de diagnóstico, catálogo, estoque e execução, em .NET 10 com MongoDB próprio e Kafka.

O Execution recebe OrdemServicoCriada, ExecucaoAutorizada e CancelamentoSolicitado por Kafka.
A API OS publica essas entradas e recebe as cinco confirmações do Execution. Os testes isolados produzem
mensagens pelo contrato público; Distributed.slnx verifica as três APIs reais em containers, com provedor de pagamento simulado por HTTP.
O serviço concentra diagnóstico e execução; clientes, veículos e decisões administrativas pertencem ao OS.

Consulte a
[arquitetura](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/arquitetura/execution.md),
o [padrão de mensageria](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/arquitetura/mensageria.md)
e o [estado da migração](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/arquitetura/estado-da-migracao.md)
no repositório principal.

## Ambiente integrado local

O Compose único para OS, Execution, Billing e dependências fica na raiz do repositório OS (`FiapTechChallengeFase1`). Nessa pasta execute `docker compose up -d --build`. A configuração e o roteiro estão no README do OS. Os três repositórios devem estar lado a lado.

## Pré-requisitos

- .NET SDK 10 para build e testes pelo host; os exemplos usam PowerShell 7.
- Docker com Compose e containers Linux para MongoDB replica set, Kafka e Testcontainers.
- Acesso ao NuGet e aos registries das imagens; Node.js 22+ somente para ferramentas de entrega.
- Portas locais 8082, 27018 e 19093 disponíveis no modo isolado.

## Dependências e restauração

Versões fixadas: Maieru.FiapTechChallenge.Chassis 1.1.1, Maieru.FiapTechChallenge.Messaging.Kafka 0.1.1
e FIAP.TechChallenge.OS.Application.Messaging 0.1.0. Os contratos locais do Execution usam ProjectReference dentro deste repositório.
Não há referências de código-fonte nem acesso aos bancos de outros serviços.

O restore padrão usa as versões fixadas do NuGet. Pacotes locais são opcionais para testar candidatos; verifique disponibilidade do feed em um checkout limpo:

```powershell
dotnet restore Execution.slnx
dotnet build Execution.slnx -c Release --no-restore
```

Para testar um contrato OS gerado localmente, opcionalmente use `scripts/Restore-Local.ps1 -OsMessagingPackage <caminho-do-nupkg>`.
O roteiro valida/copia o nupkg para artifacts/packages e gera configuração NuGet local ignorada pelo Git.
Não compila outro repositório nem escolhe versões dinamicamente. O Dockerfile aceita esses pacotes opcionais;
o build padrão e o CI restauram as versões publicadas pelo feed público.

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

O Compose inicializa MongoDB como replica set de um nó e cria quatro tópicos, com uma partição: OS, Execution, pedidos Billing e resultados Billing.
MongoDB local não exige credenciais e Kafka usa PLAINTEXT; ambos são expostos somente em loopback neste ambiente de desenvolvimento.
JWT é exigido nos endpoints administrativos e validado localmente pela API.
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
A conexão MongoDB precisa permitir transações. Coleções e índices são criados na inicialização. O processo pelo SDK não carrega o `.env` automaticamente. Configure `ASPNETCORE_ENVIRONMENT=Development` para Scalar e use `--no-launch-profile --urls http://localhost:8082` se quiser manter a porta do Compose. A URI interna `mongo:27017` anuncia o membro do replica set; um processo no host precisa de DNS/URI compatíveis, por exemplo a conexão loopback com `directConnection=true` indicada na tabela.

## Executar as três APIs e BDD

Mantenha OS, Billing e Execution lado a lado. Na raiz do Execution, construa as imagens prontas dos outros serviços:

```powershell
docker build -t fiap-os:local ../FiapTechChallengeFase1
docker build -t fiap-billing:local ../FiapTechChallengeBilling
docker build -t fiap-execution:local .
```

No .env do Execution, preencha OS_DB_PASSWORD, BILLING_DB_PASSWORD e JWT compartilhado. Para cobranças positivas, informe também MERCADO_PAGO_ACCESS_TOKEN e MERCADO_PAGO_WEBHOOK_SECRET de teste. A criação de checkout não confirma pagamento; é necessário webhook válido e URL HTTPS acessível ao provedor.

```powershell
docker compose -f compose.yaml -f compose.integrated.yaml up --build -d
docker compose -f compose.yaml -f compose.integrated.yaml ps -a
docker compose -f compose.yaml -f compose.integrated.yaml down
```

OS atende 8081, Execution 8082, Billing 8083 e o broker compartilhado 19093. Esse modo não inclui frontend; use o Compose do OS para a interface completa. Os três bancos e o Kafka têm volumes próprios; down os preserva. Não use -v se precisar dos dados, nem execute ao mesmo tempo outro Compose que dispute essas portas.

A suíte distribuída não depende desse ambiente ligado. Ela cria rede, bancos e broker descartáveis, usa JWT obtido pelo login real do OS e simula Mercado Pago por servidor HTTP/webhook assinado:

```powershell
$env:OS_TEST_IMAGE = 'fiap-os:local'
$env:EXECUTION_TEST_IMAGE = 'fiap-execution:local'
$env:BILLING_TEST_IMAGE = 'fiap-billing:local'
dotnet test Distributed.slnx -c Release --logger trx --results-directory artifacts/distributed
```

Os nove cenários cobrem conclusão, cancelamento e replay, decisões concorrentes, retomada de consumidores, espera e recusa de pagamento, JWT e acesso privado/entrega. O cenário de recusa usa um responder Kafka de contrato enquanto Billing está parado; não comprova compra externa ou recusa real no provedor.

O workflow manual distributed.yml recebe as três imagens publicadas acessíveis ao runner; prefira referências imutáveis por digest. A suíte é separada de Execution.slnx e do CI padrão do serviço.

Decisões: [integração OS/Execution](https://github.com/Maieru/fiap-tech-challenge/blob/main/docs/ADRs/ADR-016-integracao-os-execution.md) e [MongoDB próprio](docs/ADRs/ADR-003-mongodb-eks.md).

## Kubernetes / EKS

O fluxo central chama [publish-image.yml e deploy-eks.yml](scripts/delivery/README.md), reutilizáveis e sem disparo manual independente. Publicação exige CI aprovado em main e usa tag por commit; deploy confere o digest e o SHA de origem, sincroniza ExternalSecrets e acompanha rollout.

Os [manifestos](k8s/README.md) usam HTTP interno 8080, HPA de 1 a 3 réplicas, MongoDB 8.0 em replica set com TLS e credencial exclusiva, CA em `/certs/ca.pem` e OTLP para o Collector. Namespace/SecretStore, banco e Kafka são criados pelos estágios Infra/DB. O Job de compatibilidade `--verify-persistence` testa o banco antes do rollout. Não aplique placeholders diretamente.

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
O tempo médio usa os itens de serviço efetivamente concluídos.

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
Configure SONAR_TOKEN (secret) e SONAR_PROJECT_KEY (variable), organização maieru, no repositório Execution.
Os resultados das verificações estão nas evidências de validação do repositório principal.

## Publicação dos contratos

O projeto de contratos declara `FIAP.TechChallenge.Execution.Application.Messaging` 0.1.0. A versão do envelope é 1, independente da versão NuGet. Verifique a disponibilidade no feed antes de atualizar consumidores.
O workflow publish-messaging.yml usa tag messaging-vX.Y.Z em commit revisado de main, variável NUGET_USER e NuGet Trusted Publishing.
O pacote contém somente contratos, sem MongoDB/Kafka/domínio.

## Billing integrado

O Compose integrado também inclui Billing em 8083 e PostgreSQL exclusivo. Forneça BILLING_DB_PASSWORD e BILLING_IMAGE, além das imagens OS/Execution. Aprovação administrativa agora solicita pagamento; somente PagamentoConfirmado autoriza Execution. Contratos Execution permanecem v1. A suíte Distributed exige as três imagens. O cenário de recusa usa um responder Kafka de teste no lugar do Billing parado; a integração do gateway é validada separadamente com servidor HTTP de teste, banco e broker reais no Billing.

O cenário de conclusão consulta diretamente OS, Billing e Execution, compara o orçamento/cobrança/valor, verifica estoque e reentrega pedido de pagamento, confirmação e autorização. Ao terminar, grava as respostas das três APIs em um JSON anexado pelo NUnit ao resultado; o caminho é mostrado no log. O workflow distribuído coleta os JSONs, além do TRX. Consulte o relatório de validação ponta a ponta no repositório principal para evidências e limites.

## JWT emitido pelo OS

O login administrativo agora é `POST /api/os/v1/usuarios/login` no OS. Execution permanece somente validador, com issuer/audience/chave iguais aos do OS. O BDD cadastra um usuário e obtém o token pelo login real, depois o utiliza nas três APIs; um cenário adicional verifica rejeição sem JWT ou com JWT inválido.

## Organização e diagnóstico

- src contém API, Application, Domain, Infrastructure e Application.Messaging. tests separa domínio, aplicação, contratos, integração e BDD distribuído.
- API não inicia: confira JWT com 32 bytes UTF-8, eleição do primário MongoDB e tópicos Kafka. Use docker compose ps -a e docker compose logs api mongo kafka.
- MongoDB rejeita transação: confira replica set, primário, DNS anunciado e, na AWS, CA/TLS.
- Uma ordem 404 logo após abertura pode ser projeção pendente; falha persistente exige conferir eventos e o consumidor.
- Host encerrado após erro Kafka: corrija a causa e reinicie no mesmo grupo; não apague offsets para esconder a mensagem problemática.
- Porta ocupada: encerre a stack que usa a mesma porta ou configure um ambiente isolado.
