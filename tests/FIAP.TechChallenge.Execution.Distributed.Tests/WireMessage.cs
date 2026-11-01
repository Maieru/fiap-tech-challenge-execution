using System.Text.Json;

namespace FIAP.TechChallenge.Execution.Distributed.Tests;

public sealed record WireMessage(string Type, string Json, JsonElement Envelope);
