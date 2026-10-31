using FIAP.TechChallenge.Execution.Domain.Abstractions;
using System.Text.Json.Serialization;

namespace FIAP.TechChallenge.Execution.Domain.Entities;

public enum EstadoExecucao { Recebida = 1, EmDiagnostico = 2, AguardandoAprovacao = 3, EmExecucao = 4, Finalizada = 5, Cancelada = 7 }

public sealed record ExecucaoSnapshot(Guid Id, Guid OrdemServicoId, Guid ClienteId, Guid VeiculoId, string DescricaoProblema,
    EstadoExecucao Estado, DateTime DataCriacao, DateTime? DataInicioDiagnostico, DateTime? DataEnvioAprovacao,
    DateTime? DataInicioExecucao, DateTime? DataFinalizacao, long Version, long StreamSequence, long InputSequence,
    IReadOnlyCollection<ServicoDaOrdemDeServicoSnapshot> Servicos, IReadOnlyCollection<PecaOuInsumoDaOrdemDeServicoSnapshot> Pecas);

public sealed class Execucao
{
    public Guid Id { get; private init; }
    public Guid OrdemServicoId { get; private init; }
    public Guid ClienteId { get; private init; }
    public Guid VeiculoId { get; private init; }
    public string DescricaoProblema { get; private init; } = "";
    public EstadoExecucao Estado { get; private set; }
    public DateTime DataCriacao { get; private init; }
    public DateTime? DataInicioDiagnostico { get; private set; }
    public DateTime? DataEnvioAprovacao { get; private set; }
    public DateTime? DataInicioExecucao { get; private set; }
    public DateTime? DataFinalizacao { get; private set; }
    [JsonIgnore] public long Version { get; private set; }
    [JsonIgnore] public long StreamSequence { get; private set; }
    [JsonIgnore] public long InputSequence { get; private set; }
    private readonly List<ServicoDaOrdemDeServico> _servicos = [];
    private readonly List<PecaOuInsumoDaOrdemDeServico> _pecas = [];
    public IReadOnlyCollection<ServicoDaOrdemDeServico> Servicos => _servicos.AsReadOnly();
    public IReadOnlyCollection<PecaOuInsumoDaOrdemDeServico> PecasInsumos => _pecas.AsReadOnly();
    public decimal ValorTotalServicos => _servicos.Sum(x => x.ValorTotal);
    public decimal ValorTotalPecasInsumos => _pecas.Sum(x => x.ValorTotal);
    public decimal ValorTotal => ValorTotalServicos + ValorTotalPecasInsumos;

    public static Result<Execucao> Create(Guid ordemId, Guid clienteId, Guid veiculoId, string descricao, DateTime dataCriacao)
    {
        if (ordemId == Guid.Empty || clienteId == Guid.Empty || veiculoId == Guid.Empty)
            return Result<Execucao>.Failure(new Error("Os identificadores da ordem, cliente e veiculo devem ser validos."));
        if (string.IsNullOrWhiteSpace(descricao) || descricao.Trim().Length is < 3 or > 1000 || dataCriacao == default)
            return Result<Execucao>.Failure(new Error("A descricao e a data de criacao da ordem sao invalidas."));
        return Result<Execucao>.Success(new Execucao { Id = Guid.NewGuid(), OrdemServicoId = ordemId, ClienteId = clienteId,
            VeiculoId = veiculoId, DescricaoProblema = descricao.Trim(), DataCriacao = dataCriacao, Estado = EstadoExecucao.Recebida, InputSequence = 1 });
    }

    public static Execucao Rehydrate(ExecucaoSnapshot s)
    {
        if (s.Id == Guid.Empty || !Enum.IsDefined(s.Estado) || s.Version < 0 || s.StreamSequence < 0 || s.InputSequence < 1)
            throw new InvalidOperationException("Snapshot de execucao invalido.");
        var valid = Create(s.OrdemServicoId, s.ClienteId, s.VeiculoId, s.DescricaoProblema, s.DataCriacao);
        if (!valid.IsSuccess) throw new InvalidOperationException(valid.Error.Description);
        var entity = new Execucao { Id = s.Id, OrdemServicoId = s.OrdemServicoId, ClienteId = s.ClienteId, VeiculoId = s.VeiculoId,
            DescricaoProblema = s.DescricaoProblema, Estado = s.Estado, DataCriacao = s.DataCriacao, DataInicioDiagnostico = s.DataInicioDiagnostico,
            DataEnvioAprovacao = s.DataEnvioAprovacao, DataInicioExecucao = s.DataInicioExecucao, DataFinalizacao = s.DataFinalizacao,
            Version = s.Version, StreamSequence = s.StreamSequence, InputSequence = s.InputSequence };
        foreach (var item in s.Servicos)
        {
            var value = ServicoDaOrdemDeServico.Rehydrate(item);
            if (!value.IsSuccess) throw new InvalidOperationException(value.Error.Description);
            entity._servicos.Add(value.Value!);
        }
        foreach (var item in s.Pecas)
        {
            var value = PecaOuInsumoDaOrdemDeServico.Rehydrate(item);
            if (!value.IsSuccess) throw new InvalidOperationException(value.Error.Description);
            entity._pecas.Add(value.Value!);
        }
        return entity;
    }

    public ExecucaoSnapshot Snapshot() => new(Id, OrdemServicoId, ClienteId, VeiculoId, DescricaoProblema, Estado, DataCriacao,
        DataInicioDiagnostico, DataEnvioAprovacao, DataInicioExecucao, DataFinalizacao, Version, StreamSequence, InputSequence,
        _servicos.Select(x => new ServicoDaOrdemDeServicoSnapshot(x.Id, x.OrdemServicoId, x.ServicoId, x.Descricao, x.ValorUnitario, x.Quantidade, x.TempoGastoMinutos, x.Concluido)).ToArray(),
        _pecas.Select(x => new PecaOuInsumoDaOrdemDeServicoSnapshot(x.Id, x.OrdemServicoId, x.PecaInsumoId, x.Nome, x.Codigo, x.Descricao, x.PrecoUnitario, x.Quantidade)).ToArray());

    public Result<bool> IniciarDiagnostico(DateTime now)
    {
        if (Estado != EstadoExecucao.Recebida) return Conflict("Somente ordens de serviço recebidas podem iniciar diagnóstico.");
        Estado = EstadoExecucao.EmDiagnostico; DataInicioDiagnostico = now; Changed(true); return Result<bool>.Success(true);
    }
    public Result<ServicoDaOrdemDeServico> AdicionarServico(Servico servico, int quantidade)
    {
        if (Estado != EstadoExecucao.EmDiagnostico) return Result<ServicoDaOrdemDeServico>.Failure(new Error("Somente ordens de servico em diagnostico podem receber servicos.", ErrorCode.Conflict));
        var item = ServicoDaOrdemDeServico.Create(OrdemServicoId, servico, quantidade);
        if (item.IsSuccess) { _servicos.Add(item.Value!); Changed(false); }
        return item;
    }
    public Result<PecaOuInsumoDaOrdemDeServico> AdicionarPeca(PecaInsumo peca, int quantidade)
    {
        if (Estado != EstadoExecucao.EmDiagnostico) return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("Somente ordens de servico em diagnostico podem receber pecas e insumos.", ErrorCode.Conflict));
        var item = PecaOuInsumoDaOrdemDeServico.Create(OrdemServicoId, peca, quantidade);
        if (item.IsSuccess) { _pecas.Add(item.Value!); Changed(false); }
        return item;
    }
    public Result<bool> ConcluirDiagnostico(DateTime now)
    {
        if (Estado != EstadoExecucao.EmDiagnostico) return Conflict("Somente ordens de serviço em diagnóstico podem aguardar aprovação.");
        Estado = EstadoExecucao.AguardandoAprovacao; DataEnvioAprovacao = now; Changed(true); return Result<bool>.Success(true);
    }
    public Result<bool> Autorizar(DateTime now)
    {
        if (Estado != EstadoExecucao.AguardandoAprovacao) return Conflict("Somente ordens de serviço aguardando aprovação podem ser aprovadas.");
        Estado = EstadoExecucao.EmExecucao; DataInicioExecucao = now; Changed(true); return Result<bool>.Success(true);
    }
    public Result<bool> Cancelar()
    {
        if (Estado != EstadoExecucao.AguardandoAprovacao) return Conflict("Somente ordens de serviço aguardando aprovação podem ser canceladas.");
        Estado = EstadoExecucao.Cancelada; Changed(true); return Result<bool>.Success(true);
    }
    public Result<bool> ConcluirServico(Guid itemId, int minutos)
    {
        if (Estado != EstadoExecucao.EmExecucao) return Conflict("Somente ordens de serviço em execução podem concluir serviços.");
        var item = _servicos.SingleOrDefault(x => x.Id == itemId);
        if (item is null) return Result<bool>.Failure(new Error("Servico da ordem de servico nao encontrado.", ErrorCode.NotFound));
        var result = item.Concluir(minutos);
        if (result.IsSuccess) Changed(false);
        return result;
    }
    public Result<bool> Finalizar(DateTime now)
    {
        if (Estado != EstadoExecucao.EmExecucao) return Conflict("Somente ordens de serviço em execução podem ser finalizadas.");
        if (_servicos.Any(x => !x.Concluido)) return Conflict("Somente ordens de serviço com todos os serviços concluídos podem ser finalizadas.");
        Estado = EstadoExecucao.Finalizada; DataFinalizacao = now; Changed(true); return Result<bool>.Success(true);
    }
    public void AcceptInput(long sequence)
    {
        if (sequence != InputSequence + 1) throw new InvalidOperationException("Lacuna na sequencia de eventos da OS.");
        InputSequence = sequence;
    }
    public void RejectInput() => Changed(true);
    private void Changed(bool integrationEvent) { Version++; if (integrationEvent) StreamSequence++; }
    private static Result<bool> Conflict(string message) => Result<bool>.Failure(new Error(message, ErrorCode.Conflict));
}
