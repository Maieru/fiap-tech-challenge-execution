namespace FIAP.TechChallenge.Execution.Domain.Abstractions;

public sealed class ConcurrencyException() : Exception("O registro foi alterado por outra operação. Consulte novamente e tente outra vez.");
