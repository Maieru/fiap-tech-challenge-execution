using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests;

public sealed class IdempotentCommandRunnerTests
{
    private sealed class Transactions : ITransactionRunner
    { public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> action, CancellationToken ct) => action(ct); }
    public sealed record Response(Guid Id, int Quantidade);
    [TestCase(null)][TestCase("")][TestCase("   ")]
    public async Task Execute_ShouldRejectMissingKeysBeforeRunningBusinessCode(string? key)
    {
        var store = new Mock<ICommandStore>(MockBehavior.Strict); var runner = new IdempotentCommandRunner(new Transactions(), store.Object);
        var called = false;
        var result = await runner.ExecuteAsync(key, "stock", new { Quantidade = 1 }, _ => { called = true; return Task.FromResult(Result<Response>.Success(new(Guid.NewGuid(), 1))); }, default);
        Assert.Multiple(() => { Assert.That(result.IsSuccess, Is.False); Assert.That(called, Is.False); });
    }
    [Test]
    public async Task Execute_ShouldReplayStoredResultAndRejectDifferentPayload()
    {
        var stored = new Dictionary<string, StoredCommand>(); var store = new Mock<ICommandStore>();
        store.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string id, CancellationToken _) => stored.GetValueOrDefault(id));
        store.Setup(x => x.AddAsync(It.IsAny<string>(), It.IsAny<StoredCommand>(), It.IsAny<CancellationToken>())).Callback<string, StoredCommand, CancellationToken>((id, value, _) => stored.Add(id, value)).Returns(Task.CompletedTask);
        var runner = new IdempotentCommandRunner(new Transactions(), store.Object); var response = new Response(Guid.NewGuid(), 2); var calls = 0;
        Task<Result<Response>> Business(CancellationToken _) { calls++; return Task.FromResult(Result<Response>.Success(response)); }
        var first = await runner.ExecuteAsync("key", "stock", new { Quantidade = 2 }, Business, default);
        var second = await runner.ExecuteAsync("key", "stock", new { Quantidade = 2 }, Business, default);
        var mismatch = await runner.ExecuteAsync("key", "stock", new { Quantidade = 3 }, Business, default);
        Assert.Multiple(() =>
        {
            Assert.That(first.Value, Is.EqualTo(response)); Assert.That(second.Value, Is.EqualTo(response)); Assert.That(calls, Is.EqualTo(1));
            Assert.That(mismatch.Error.Code, Is.EqualTo(ErrorCode.Conflict));
        });
    }
    [Test]
    public async Task Execute_ShouldNotRememberRejectedOperations()
    {
        var store = new Mock<ICommandStore>();
        var runner = new IdempotentCommandRunner(new Transactions(), store.Object);
        var result = await runner.ExecuteAsync<Response>("key", "scope", new { Quantidade = 0 }, _ => Task.FromResult(Result<Response>.Failure(new Error("Invalid"))), default);
        Assert.That(result.IsSuccess, Is.False);
        store.Verify(x => x.AddAsync(It.IsAny<string>(), It.IsAny<StoredCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
