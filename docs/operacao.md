# Execução local e CI

## Subir os serviços

Requer Docker Linux, portas 8081, 8082, 5433 e 9092 disponíveis. Copie `.env.example` para `.env` em cada serviço e ajuste as credenciais. Use os mesmos `JWT_ISSUER`, `JWT_AUDIENCE` e `JWT_SIGNING_KEY` do emissor administrativo atual. A chave deve ter pelo menos 32 bytes. Os valores de exemplo servem apenas ao desenvolvimento. A senha MongoDB usada na URI deve estar percent-encoded se contiver caracteres reservados.

Na raiz do Execution:

```powershell
docker compose up --build -d
```

Isso cria a rede `fiap-oficina`, MongoDB autenticado com replica set `rs1`, banco `fiap_execution`, Kafka KRaft e API 8082. MongoDB não publica porta no host. Os volumes do projeto guardam MongoDB e Kafka.

Na raiz do repositório principal, após configurar `src/Services/OrdensServico/.env`:

```powershell
docker compose -f src/Services/OrdensServico/compose.yaml -f src/Services/OrdensServico/compose.integration.yaml up --build -d
```

O overlay conecta o OS à mesma rede e habilita Kafka. Seu PostgreSQL, banco, volume e porta continuam os mesmos. A migration atualiza estados/datas, concorrência, Outbox/Inbox e preenche somente eventos de abertura ausentes para ordens Recebida. Aplicar novamente não duplica eventos. Não há importação do monólito. O Compose simples do OS continua disponível com Kafka desabilitado; nesse modo as mensagens aguardam na Outbox.

Para demonstrar autorização, configure `MANUAL_EXECUTION_AUTHORIZATION_ENABLED=true` no `.env` do OS e recrie sua API. A flag padrão é false e Production sempre bloqueia esse endpoint. O `sub` do JWT fica na auditoria. Use `POST /api/usuarios/login` no monólito para obter um token compatível; o Execution/OS não consultam esse banco nem emitem tokens. Os testes geram e assinam JWTs reais com chaves temporárias próprias.

## Demonstração

Use os contratos e exemplos de corpo em [contratos](contratos.md), com `Authorization: Bearer <JWT>`:

1. Cadastre cliente e veículo no novo OS e serviço/peça no Execution, com estoque inicial próprio.
2. Abra a OS; consulte sua fila no Execution até o consumo de OrdemCriada.
3. Inicie diagnóstico; adicione serviço e peça. Use uma nova `Idempotency-Key` na reserva. O estoque já reduz nessa etapa.
4. Conclua diagnóstico e aguarde o OS exibir AguardandoAprovacao.
5. Autorize pelo OS. Aguarde ambos em EmExecucao; conclua cada item de serviço com tempo gasto e finalize.
6. Consulte a OS e seu histórico. A inativação posterior de cliente, veículo ou catálogo preserva a consulta e os snapshots.

Para o caminho de cancelamento, repita até a etapa 4 e solicite `/cancelar` no OS. Aguarde confirmação; o estoque retorna exatamente uma vez, inclusive para peça inativada. `202` significa solicitação persistida, ainda aguardando resultado. Kafka indisponível não impede persistência local; a API continua viva e readiness acusa a dependência.

`docker compose down` preserva dados. Pare o OS integrado antes do Execution, pois ele usa a rede criada pelo Execution. Não remova os volumes para atualizar imagens ou migrations.

## DLQ e replay

Investigue o header `failure` e o estado/InputSequence do agregado. Corrija a causa e identifique o predecessor ausente antes de repetir eventos seguintes da mesma OS. Não edite MessageId, chave, sequência, datas ou payload. O consumidor aplica Inbox e valida sequência novamente; repetir mensagem já aplicada não repete efeitos.

Para inspecionar a DLQ com as ferramentas incluídas na imagem Kafka:

```powershell
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server kafka:19092 --topic fiap.os.events.v1.dlq --from-beginning --property print.key=true --property print.headers=true
```

Prepare manualmente `reviewed-replay.tsv` com apenas as mensagens revisadas, uma por linha: chave original, TAB e JSON original (sem headers). Reenvie ao tópico de origem, em sequência por OS:

```powershell
Get-Content reviewed-replay.tsv | docker compose exec -T kafka /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server kafka:19092 --topic fiap.os.events.v1 --property parse.key=true
```

Para eventos destinados ao OS, use `fiap.execution.events.v1.dlq` e `fiap.execution.events.v1`. Após cada predecessor, confirme aplicação antes do próximo. Não reinicie offsets do grupo para fazer replay. O envio duplicado à própria DLQ também é possível se houver queda entre sua confirmação e o commit do original; a Inbox protege o efeito de replay.

## Testes e CI

```powershell
dotnet build Execution.slnx -c Release
dotnet test Execution.slnx -c Release --no-build --no-restore --filter 'TestCategory!=EndToEnd' --settings coverage.runsettings --collect:'XPlat Code Coverage;Format=cobertura,opencover' --logger trx --results-directory artifacts/test-results
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Testcontainers cria ambientes isolados. Os cenários EndToEnd usam duas imagens reais; `OS_TEST_IMAGE` e `EXECUTION_TEST_IMAGE` são obrigatórios para executá-los. Sem elas, apenas esses cenários são ignorados. O CI constrói as imagens e sempre fornece ambas.

O arquivo `.config/os-compatibility.json` fixa um SHA do OS, obtido pelo checkout externo em `external/os` e compilado isoladamente. Para testar localmente, construa a imagem desse commit com o Dockerfile do OS, construa a do Execution e execute:

```powershell
$env:OS_TEST_IMAGE='fiap-os:execution'
$env:EXECUTION_TEST_IMAGE='fiap-execution:local'
$env:E2E_ARTIFACTS_DIRECTORY=Join-Path $PWD 'artifacts/contracts'
dotnet test tests/FIAP.TechChallenge.Execution.Integration.Tests -c Release --no-build --no-restore --filter 'TestCategory=EndToEnd' --logger trx --results-directory artifacts/e2e
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

CI verifica build, TRX, Cobertura/OpenCover, mínimo de 80% do Execution, imagens, Compose e BDD. O pipeline do repositório OS aplica seu próprio mínimo de 80%, sem compensação. Sonar roda em Windows, JDK 21 Zulu, histórico completo e scanner fixado. Configure no novo repositório o secret `SONAR_TOKEN` autorizado para seu projeto. Organização/projeto são públicos: variables `SONAR_ORGANIZATION` e `SONAR_PROJECT_KEY`, com padrões `maieru` / `Maieru_fiap-tech-challenge-execution`. Crie/importe esse projeto no SonarQube Cloud e desabilite análise automática quando usar o scanner. O job exige aprovação do Quality Gate; a falta de configuração falha explicitamente. Nenhuma chave NuGet é necessária para consumir o pacote público.

Proteja main com PR e check `ci-required`. O workflow deste repositório não provisiona AWS. Atualizar o SHA compatível exige repetir o BDD.

## Observabilidade

A chave JWT é obtida exclusivamente da variável de ambiente `Jwt__SigningKey`, preenchida pelo Compose; issuer/audience continuam configuráveis. Os testes HTTP fornecem uma chave temporária no ambiente de seu próprio processo, mantendo a validação real da assinatura.

Chassi fornece logs JSON, correlação, ProblemDetails, HTTP, liveness e readiness. `Fiap.TechChallenge.Messaging` fornece traces de publicação/consumo e instrumentos `messaging.outbox.pending`, `messaging.consumer_lag`, `messaging.retries`, `messaging.dead_letters`, `execution.stock.compensations`. O último conta cancelamentos compensados após commit; mensagens repetidas não incrementam. São métricas de processo, não um livro contábil durável. OTLP é opcional (`EXECUTION_ENABLE_OTLP`/`OS_ENABLE_OTLP`); New Relic não é obrigatório localmente.

Kafka local usa PLAINTEXT e um broker; MongoDB um nó. A configuração de produção, ACLs/TLS, retenção/limpeza de Inbox/Outbox/idempotência e alta disponibilidade serão definidas com o deploy AWS, fora desta entrega.
