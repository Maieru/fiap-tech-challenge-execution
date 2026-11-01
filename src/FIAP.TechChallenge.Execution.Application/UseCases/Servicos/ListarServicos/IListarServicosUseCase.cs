using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.ListarServicos;

public interface IListarServicosUseCase
{
    Task<Result<ListarServicosResponse>> ExecuteAsync(ListarServicosCommand command, CancellationToken cancellationToken = default);
}

