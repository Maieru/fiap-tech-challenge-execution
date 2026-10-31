# Execution Service

Catálogo de serviços, peças e estoque, diagnóstico e execução de ordens. .NET 10, MongoDB com replica set e integração Kafka com o [OS Service](https://github.com/Maieru/fiap-tech-challenge/tree/inicio-segregacao-servicos/src/Services/OrdensServico). Consome o chassi público `Maieru.FiapTechChallenge.Chassis` **1.0.2**.

```powershell
Copy-Item .env.example .env
# Ajuste as credenciais locais e os mesmos parâmetros JWT usados pelo OS.
docker compose up --build -d
```

API em `http://localhost:8082/api/execution/v1`, documentação em `http://localhost:8082/scalar/v1`. Requer Docker com containers Linux; para desenvolver, .NET SDK 10 e PowerShell 7. `Execution.slnx` reúne quatro projetos de produção e três de testes.

```powershell
dotnet test Execution.slnx -c Release --filter 'TestCategory!=EndToEnd'
```

- [Execução local, demonstração e CI](docs/operacao.md)
- [Rotas e contratos Kafka](docs/contratos.md)
- [Extração e adaptações do legado](docs/reaproveitamento.md)
- [Decisões arquiteturais](docs/ADRs/README.md)
- [Evidências de validação](docs/verificacao.md)

A autorização administrativa temporária exige ativação explícita no OS em Development/Testing. Billing, aprovação pelo cliente, frontend e AWS ficam para os próximos incrementos.
