using FIAP.TechChallenge.Execution.API.Extensions;
using FIAP.TechChallenge.Execution.Application;
using FIAP.TechChallenge.Execution.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.TechChallenge.Execution.API.Controllers;

public sealed record AdicionarServicoCommand(Guid ServicoId, int Quantidade);
public sealed record AdicionarPecaCommand(Guid PecaInsumoId, int Quantidade);

[ApiController, Authorize, Route("api/execution/v1/ordens")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public sealed class OrdensController(ExecucaoUseCases useCases) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<FilaResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] EstadoExecucao? estado, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10,
        CancellationToken ct = default) => (await useCases.ListarAsync(estado, pageNumber, pageSize, ct)).ToActionResult(this);
    [HttpGet("{id:guid}")]
    [ProducesResponseType<Execucao>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => (await useCases.ConsultarAsync(id, ct)).ToActionResult(this);
    [HttpPost("{id:guid}/iniciar-diagnostico")]
    [ProducesResponseType<Execucao>(StatusCodes.Status200OK)]
    public async Task<IActionResult> IniciarDiagnostico(Guid id, CancellationToken ct) => (await useCases.IniciarDiagnosticoAsync(id, ct)).ToActionResult(this);
    [HttpPost("{id:guid}/concluir-diagnostico")]
    [ProducesResponseType<Execucao>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConcluirDiagnostico(Guid id, CancellationToken ct) => (await useCases.ConcluirDiagnosticoAsync(id, ct)).ToActionResult(this);
    [HttpPost("{id:guid}/servicos")]
    [ProducesResponseType<ServicoDaOrdemDeServico>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddServico(Guid id, AdicionarServicoCommand command, CancellationToken ct) =>
        (await useCases.AdicionarServicoAsync(id, new(command.ServicoId, command.Quantidade), ct)).ToActionResult(this, value => CreatedAtAction(nameof(Get), new { id }, value));
    [HttpPost("{id:guid}/pecas-insumos")]
    [ProducesResponseType<PecaAdicionadaResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddPeca(Guid id, AdicionarPecaCommand command, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct) =>
        (await useCases.AdicionarPecaAsync(id, new(command.PecaInsumoId, command.Quantidade), key, ct)).ToActionResult(this, value => CreatedAtAction(nameof(Get), new { id }, value));
    [HttpPost("{id:guid}/servicos/{itemId:guid}/concluir")]
    [ProducesResponseType<Execucao>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConcluirServico(Guid id, Guid itemId, ConcluirServicoCommand command, CancellationToken ct) =>
        (await useCases.ConcluirServicoAsync(id, itemId, command.TempoGastoMinutos, ct)).ToActionResult(this);
    [HttpPost("{id:guid}/finalizar")]
    [ProducesResponseType<Execucao>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Finalizar(Guid id, CancellationToken ct) => (await useCases.FinalizarAsync(id, ct)).ToActionResult(this);
}
