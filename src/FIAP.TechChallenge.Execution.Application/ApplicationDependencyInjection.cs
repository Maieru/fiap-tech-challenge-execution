using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.AdicionarPecaInsumo;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.AdicionarServico;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.CancelarExecucao;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ConcluirDiagnostico;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ConcluirServico;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.FinalizarExecucao;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.IniciarDiagnostico;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.IniciarExecucao;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ListarExecucoes;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.RecuperarExecucao;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.RegistrarOrdemRecebida;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.AtualizarPecaInsumo;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.EntradaEstoquePecaInsumo;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ExcluirPecaInsumo;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.IncluirPecaInsumo;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ListarPecasInsumos;
using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.RecuperarPecaInsumo;
using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.AtualizarServico;
using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.CadastrarServico;
using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.ExcluirServico;
using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.ListarServicos;
using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.RecuperarServico;
using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.VerificarTempoMedioServico;
using FIAP.TechChallenge.Execution.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace FIAP.TechChallenge.Execution.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<MessageContext>();
        services.AddScoped<IAdicionarPecaInsumoUseCase, AdicionarPecaInsumoUseCase>();
        services.AddScoped<IAdicionarServicoUseCase, AdicionarServicoUseCase>();
        services.AddScoped<ICancelarExecucaoUseCase, CancelarExecucaoUseCase>();
        services.AddScoped<IConcluirDiagnosticoUseCase, ConcluirDiagnosticoUseCase>();
        services.AddScoped<IConcluirServicoUseCase, ConcluirServicoUseCase>();
        services.AddScoped<IFinalizarExecucaoUseCase, FinalizarExecucaoUseCase>();
        services.AddScoped<IIniciarDiagnosticoUseCase, IniciarDiagnosticoUseCase>();
        services.AddScoped<IIniciarExecucaoUseCase, IniciarExecucaoUseCase>();
        services.AddScoped<IListarExecucoesUseCase, ListarExecucoesUseCase>();
        services.AddScoped<IRecuperarExecucaoUseCase, RecuperarExecucaoUseCase>();
        services.AddScoped<IRegistrarOrdemRecebidaUseCase, RegistrarOrdemRecebidaUseCase>();
        services.AddScoped<IAtualizarPecaInsumoUseCase, AtualizarPecaInsumoUseCase>();
        services.AddScoped<IEntradaEstoquePecaInsumoUseCase, EntradaEstoquePecaInsumoUseCase>();
        services.AddScoped<IExcluirPecaInsumoUseCase, ExcluirPecaInsumoUseCase>();
        services.AddScoped<IIncluirPecaInsumoUseCase, IncluirPecaInsumoUseCase>();
        services.AddScoped<IListarPecasInsumosUseCase, ListarPecasInsumosUseCase>();
        services.AddScoped<IRecuperarPecaInsumoUseCase, RecuperarPecaInsumoUseCase>();
        services.AddScoped<IAtualizarServicoUseCase, AtualizarServicoUseCase>();
        services.AddScoped<ICadastrarServicoUseCase, CadastrarServicoUseCase>();
        services.AddScoped<IExcluirServicoUseCase, ExcluirServicoUseCase>();
        services.AddScoped<IListarServicosUseCase, ListarServicosUseCase>();
        services.AddScoped<IRecuperarServicoUseCase, RecuperarServicoUseCase>();
        services.AddScoped<IVerificarTempoMedioServicoUseCase, VerificarTempoMedioServicoUseCase>();
        return services;
    }
}
