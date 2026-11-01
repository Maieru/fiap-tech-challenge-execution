using FIAP.TechChallenge.Execution.API.Extensions;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.RecuperarExecucao;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ListarExecucoes;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.IniciarDiagnostico;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ConcluirDiagnostico;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.AdicionarServico;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.AdicionarPecaInsumo;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ConcluirServico;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.FinalizarExecucao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.TechChallenge.Execution.API.Controllers;

[ApiController, Authorize, Route("api/execution/v1/ordens")]
public sealed class ExecucoesController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromServices] IListarExecucoesUseCase useCase, [FromQuery] ListarExecucoesCommand command, CancellationToken token) =>
        (await useCase.ExecuteAsync(command, token)).ToActionResult(this);
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromServices] IRecuperarExecucaoUseCase useCase, CancellationToken token) =>
        (await useCase.ExecuteAsync(new(id), token)).ToActionResult(this);
    [HttpPut("{id:guid}/iniciar-diagnostico")]
    public async Task<IActionResult> StartDiagnosis(Guid id, [FromServices] IIniciarDiagnosticoUseCase useCase, CancellationToken token) =>
        (await useCase.ExecuteAsync(new(id), token)).ToActionResult(this);
    [HttpPut("{id:guid}/solicitar-aprovacao")]
    public async Task<IActionResult> CompleteDiagnosis(Guid id, [FromServices] IConcluirDiagnosticoUseCase useCase, CancellationToken token) =>
        (await useCase.ExecuteAsync(new(id), token)).ToActionResult(this);
    [HttpPost("{id:guid}/addservico")]
    public async Task<IActionResult> AddService(Guid id, [FromServices] IAdicionarServicoUseCase useCase, AdicionarServicoCommand command, CancellationToken token) =>
        (await useCase.ExecuteAsync(command with { OrdemServicoId = id }, token)).ToActionResult(this,
            value => CreatedAtAction(nameof(GetById), new { id }, value));
    [HttpPost("{id:guid}/addpecainsumo")]
    public async Task<IActionResult> AddPart(Guid id, [FromServices] IAdicionarPecaInsumoUseCase useCase, AdicionarPecaInsumoCommand command, CancellationToken token) =>
        (await useCase.ExecuteAsync(command with { OrdemServicoId = id }, token)).ToActionResult(this,
            value => CreatedAtAction(nameof(GetById), new { id }, value));
    [HttpPut("{id:guid}/servicos/{itemId:guid}/concluir")]
    public async Task<IActionResult> CompleteService(Guid id, Guid itemId, [FromServices] IConcluirServicoUseCase useCase, ConcluirServicoCommand command, CancellationToken token) =>
        (await useCase.ExecuteAsync(command with { OrdemServicoId = id, ItemId = itemId }, token)).ToActionResult(this);
    [HttpPut("{id:guid}/finalizar")]
    public async Task<IActionResult> Complete(Guid id, [FromServices] IFinalizarExecucaoUseCase useCase, CancellationToken token) =>
        (await useCase.ExecuteAsync(new(id), token)).ToActionResult(this);
}
