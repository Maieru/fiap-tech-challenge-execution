using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Domain.Interfaces;

public interface IServicoDaOrdemDeServicoRepository
{
    Task<Result<IReadOnlyCollection<ServicoDaOrdemDeServico>>> GetConcluidosByServicoIdAsync(Guid servicoId, CancellationToken cancellationToken = default);
}
