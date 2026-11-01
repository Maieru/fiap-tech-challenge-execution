using FIAP.TechChallenge.Execution.API.Extensions;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.AtualizarPecaInsumo;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.EntradaEstoquePecaInsumo;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ExcluirPecaInsumo;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.IncluirPecaInsumo;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ListarPecasInsumos;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.RecuperarPecaInsumo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.TechChallenge.Execution.API.Controllers;

[ApiController]
[Route("api/execution/v1/pecas-insumos")]
[Authorize]
public sealed class PecasInsumosController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ListarPecasInsumosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(IListarPecasInsumosUseCase useCase, [FromQuery] ListarPecasInsumosCommand command, CancellationToken cancellationToken = default)
    {
        var result = await useCase.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult(this, value =>
        {
            if (!string.IsNullOrWhiteSpace(command.Codigo))
                return Ok(value.PecasInsumos.First());

            return Ok(value);
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RecuperarPecaInsumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, IRecuperarPecaInsumoUseCase useCase, CancellationToken cancellationToken = default)
    {
        var command = new RecuperarPecaInsumoCommand
        {
            PecaInsumoId = id
        };

        var result = await useCase.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(IncluirPecaInsumoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Post(IIncluirPecaInsumoUseCase useCase, IncluirPecaInsumoCommand command, CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult(this, value => CreatedAtAction(nameof(GetById), new { id = value.Id }, value));
    }

    [HttpPut("{id:guid}/entrada-estoque")]
    [ProducesResponseType(typeof(EntradaEstoquePecaInsumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutEntradaEstoque(Guid id, IEntradaEstoquePecaInsumoUseCase useCase, EntradaEstoquePecaInsumoCommand command, CancellationToken cancellationToken)
    {
        var entradaEstoqueCommand = new EntradaEstoquePecaInsumoCommand
        {
            Id = id,
            Quantidade = command.Quantidade
        };

        var result = await useCase.ExecuteAsync(entradaEstoqueCommand, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AtualizarPecaInsumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Put(Guid id, IAtualizarPecaInsumoUseCase useCase, AtualizarPecaInsumoCommand command, CancellationToken cancellationToken)
    {
        var updateCommand = new AtualizarPecaInsumoCommand
        {
            Id = id,
            Nome = command.Nome,
            Codigo = command.Codigo,
            Descricao = command.Descricao,
            PrecoUnitario = command.PrecoUnitario,
            Ativo = command.Ativo
        };

        var result = await useCase.ExecuteAsync(updateCommand, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, IExcluirPecaInsumoUseCase useCase, CancellationToken cancellationToken)
    {
        var command = new ExcluirPecaInsumoCommand { Id = id };
        var result = await useCase.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult(this, _ => NoContent());
    }
}

