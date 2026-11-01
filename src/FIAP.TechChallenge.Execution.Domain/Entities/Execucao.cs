using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Domain.Entities;

public enum StatusExecucao { Recebida = 1, EmDiagnostico = 2, AguardandoAprovacao = 3, EmExecucao = 4, Finalizada = 5, Cancelada = 7 }

public sealed record ExecucaoSnapshot(
    Guid OrdemServicoId, Guid ClienteId, Guid VeiculoId, string DescricaoProblema,
    StatusExecucao Status, DateTimeOffset DataCriacaoUtc, DateTimeOffset? InicioDiagnosticoUtc,
    DateTimeOffset? FimDiagnosticoUtc, DateTimeOffset? InicioExecucaoUtc, DateTimeOffset? FimExecucaoUtc,
    DateTimeOffset? CancelamentoUtc, IReadOnlyList<ServicoDaOrdemDeServicoSnapshot> Servicos,
    IReadOnlyList<PecaOuInsumoDaOrdemDeServicoSnapshot> PecasInsumos);

public sealed class Execucao
{
    private ExecucaoSnapshot state;
    private readonly List<ServicoDaOrdemDeServico> servicos;
    private readonly List<PecaOuInsumoDaOrdemDeServico> pecas;
    private Execucao(ExecucaoSnapshot snapshot, List<ServicoDaOrdemDeServico> services, List<PecaOuInsumoDaOrdemDeServico> parts)
    { state = snapshot; servicos = services; pecas = parts; }

    public Guid OrdemServicoId => state.OrdemServicoId;
    public StatusExecucao Status => state.Status;
    public IReadOnlyList<ServicoDaOrdemDeServico> Servicos => servicos.AsReadOnly();
    public IReadOnlyList<PecaOuInsumoDaOrdemDeServico> PecasInsumos => pecas.AsReadOnly();
    public decimal ValorTotal => servicos.Sum(x => x.ValorTotal) + pecas.Sum(x => x.ValorTotal);

    public static Result<Execucao> Create(Guid ordemId, Guid clienteId, Guid veiculoId, string descricao, DateTimeOffset createdAt) =>
        Rehydrate(new(ordemId, clienteId, veiculoId, descricao, StatusExecucao.Recebida, createdAt,
            null, null, null, null, null, [], []));

    public static Result<Execucao> Rehydrate(ExecucaoSnapshot snapshot)
    {
        if (snapshot.OrdemServicoId == Guid.Empty || snapshot.ClienteId == Guid.Empty || snapshot.VeiculoId == Guid.Empty)
            return Result<Execucao>.Failure(new Error("Os identificadores da ordem, cliente e veículo são obrigatórios."));
        if (string.IsNullOrWhiteSpace(snapshot.DescricaoProblema) || snapshot.DescricaoProblema.Trim().Length is < 3 or > 1000)
            return Result<Execucao>.Failure(new Error("A descrição deve ter entre 3 e 1000 caracteres."));
        if (snapshot.DataCriacaoUtc == default || !Enum.IsDefined(snapshot.Status))
            return Result<Execucao>.Failure(new Error("Data ou status da execução inválido."));
        if ((snapshot.Status != StatusExecucao.Recebida && snapshot.InicioDiagnosticoUtc is null)
            || (snapshot.Status is StatusExecucao.AguardandoAprovacao or StatusExecucao.EmExecucao or StatusExecucao.Finalizada or StatusExecucao.Cancelada && snapshot.FimDiagnosticoUtc is null)
            || (snapshot.Status is StatusExecucao.EmExecucao or StatusExecucao.Finalizada && snapshot.InicioExecucaoUtc is null)
            || (snapshot.Status == StatusExecucao.Finalizada && snapshot.FimExecucaoUtc is null)
            || (snapshot.Status == StatusExecucao.Cancelada && snapshot.CancelamentoUtc is null))
            return Result<Execucao>.Failure(new Error("As datas não correspondem ao estado da execução."));
        var services = new List<ServicoDaOrdemDeServico>();
        foreach (var item in snapshot.Servicos)
        {
            var result = ServicoDaOrdemDeServico.Rehydrate(item);
            if (!result.IsSuccess || item.OrdemServicoId != snapshot.OrdemServicoId)
                return Result<Execucao>.Failure(new Error("Serviço histórico inválido."));
            services.Add(result.Value!);
        }
        var parts = new List<PecaOuInsumoDaOrdemDeServico>();
        foreach (var item in snapshot.PecasInsumos)
        {
            var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(item);
            if (!result.IsSuccess || item.OrdemServicoId != snapshot.OrdemServicoId)
                return Result<Execucao>.Failure(new Error("Peça histórica inválida."));
            parts.Add(result.Value!);
        }
        if (services.Select(x => x.Id).Distinct().Count() != services.Count
            || parts.Select(x => x.Id).Distinct().Count() != parts.Count
            || (snapshot.Status == StatusExecucao.Finalizada && services.Any(x => !x.Concluido)))
            return Result<Execucao>.Failure(new Error("Itens inconsistentes na execução."));
        return Result<Execucao>.Success(new(snapshot with { DescricaoProblema = snapshot.DescricaoProblema.Trim() }, services, parts));
    }

    public Result<bool> IniciarDiagnostico(DateTimeOffset now)
    {
        if (Status != StatusExecucao.Recebida)
            return Invalid("Somente ordens recebidas podem iniciar diagnóstico.");
        state = state with { Status = StatusExecucao.EmDiagnostico, InicioDiagnosticoUtc = now };
        return Result<bool>.Success(true);
    }

    public Result<ServicoDaOrdemDeServico> AdicionarServico(Servico servico, int quantidade)
    {
        if (Status != StatusExecucao.EmDiagnostico)
            return Result<ServicoDaOrdemDeServico>.Failure(new Error("Serviços só podem ser adicionados em diagnóstico."));
        var result = ServicoDaOrdemDeServico.Create(OrdemServicoId, servico, quantidade);
        if (result.IsSuccess)
            servicos.Add(result.Value!);
        return result;
    }

    public Result<PecaOuInsumoDaOrdemDeServico> AdicionarPeca(PecaInsumo peca, int quantidade)
    {
        if (Status != StatusExecucao.EmDiagnostico)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("Peças só podem ser adicionadas em diagnóstico."));
        if (!peca.Ativo)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("Peça inativa."));
        var result = PecaOuInsumoDaOrdemDeServico.Create(OrdemServicoId, peca, quantidade);
        if (result.IsSuccess)
            pecas.Add(result.Value!);
        return result;
    }

    public Result<bool> ConcluirDiagnostico(DateTimeOffset now)
    {
        if (Status != StatusExecucao.EmDiagnostico)
            return Invalid("Somente ordens em diagnóstico podem aguardar aprovação.");
        state = state with { Status = StatusExecucao.AguardandoAprovacao, FimDiagnosticoUtc = now };
        return Result<bool>.Success(true);
    }

    public Result<bool> IniciarExecucao(DateTimeOffset now)
    {
        if (Status != StatusExecucao.AguardandoAprovacao)
            return Invalid("Somente ordens aguardando aprovação podem iniciar execução.");
        state = state with { Status = StatusExecucao.EmExecucao, InicioExecucaoUtc = now };
        return Result<bool>.Success(true);
    }

    public Result<bool> ConcluirServico(Guid itemId, int tempoGastoMinutos)
    {
        if (Status != StatusExecucao.EmExecucao)
            return Invalid("Serviços só podem ser concluídos durante a execução.");
        var item = servicos.SingleOrDefault(x => x.Id == itemId);
        return item is null ? Result<bool>.Failure(Error.NotFound("Serviço da ordem")) : item.Concluir(tempoGastoMinutos);
    }

    public Result<bool> Finalizar(DateTimeOffset now)
    {
        if (Status != StatusExecucao.EmExecucao || servicos.Any(x => !x.Concluido))
            return Invalid("A execução deve estar em andamento e todos os serviços precisam estar concluídos.");
        state = state with { Status = StatusExecucao.Finalizada, FimExecucaoUtc = now };
        return Result<bool>.Success(true);
    }

    public Result<bool> Cancelar(DateTimeOffset now)
    {
        if (Status != StatusExecucao.AguardandoAprovacao)
            return Invalid("Somente ordens aguardando aprovação podem ser canceladas.");
        state = state with { Status = StatusExecucao.Cancelada, CancelamentoUtc = now };
        return Result<bool>.Success(true);
    }

    public ExecucaoSnapshot Snapshot() => state with
    {
        Servicos = servicos.Select(x => new ServicoDaOrdemDeServicoSnapshot(x.Id, x.OrdemServicoId,
            x.ServicoId, x.Descricao, x.ValorUnitario, x.Quantidade, x.TempoGastoMinutos, x.Concluido)).ToArray(),
        PecasInsumos = pecas.Select(x => new PecaOuInsumoDaOrdemDeServicoSnapshot(x.Id, x.OrdemServicoId,
            x.PecaInsumoId, x.Nome, x.Codigo, x.Descricao, x.PrecoUnitario, x.Quantidade)).ToArray()
    };

    private static Result<bool> Invalid(string message) => Result<bool>.Failure(new Error(message));
}
