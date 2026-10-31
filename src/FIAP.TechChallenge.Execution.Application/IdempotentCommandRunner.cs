using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application;

public sealed class IdempotentCommandRunner(ITransactionRunner transactions, ICommandStore commands)
{
    public Task<Result<T>> ExecuteAsync<T>(string? key, string scope, object payload, Func<CancellationToken, Task<Result<T>>> action, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
            return Task.FromResult(Result<T>.Failure(new Error("Informe Idempotency-Key com ate 128 caracteres.")));
        var id = Hash(scope + ":" + key);
        var hash = Hash(JsonSerializer.Serialize(payload));
        return transactions.ExecuteAsync<T>(async token =>
        {
            var previous = await commands.GetAsync(id, token);
            if (previous is not null)
                return previous.RequestHash == hash ? Result<T>.Success(JsonSerializer.Deserialize<T>(previous.ResponseJson)!)
                    : Result<T>.Failure(new Error("Idempotency-Key ja utilizada com outro conteudo.", ErrorCode.Conflict));
            var result = await action(token);
            if (result.IsSuccess) await commands.AddAsync(id, new StoredCommand(hash, JsonSerializer.Serialize(result.Value)), token);
            return result;
        }, ct);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
