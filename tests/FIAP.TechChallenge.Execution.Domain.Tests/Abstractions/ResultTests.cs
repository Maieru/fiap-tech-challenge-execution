using System.Text.Json;
using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Domain.Tests.Abstractions;

public sealed class ResultTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [TestCase("""{"isSuccess":true,"value":"ok","error":null}""")]
    [TestCase("""{"isSuccess":false,"value":null,"error":null}""")]
    public void Deserialize_ShouldRejectMissingErrorBeforeReadingItsCode(string json)
    {
        Assert.That(() => JsonSerializer.Deserialize<Result<string>>(json, Json),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("error"));
    }

    [TestCase("""{"isSuccess":true,"value":"ok","error":{"description":"invalid","code":1}}""")]
    [TestCase("""{"isSuccess":false,"value":"unexpected","error":{"description":"invalid","code":1}}""")]
    [TestCase("""{"isSuccess":false,"value":null,"error":{"description":"none","code":0}}""")]
    public void Deserialize_ShouldRejectContradictoryResult(string json)
    {
        Assert.That(() => JsonSerializer.Deserialize<Result<string>>(json, Json), Throws.TypeOf<JsonException>());
    }

    [Test]
    public void Serialization_ShouldPreserveSuccessfulResult()
    {
        var result = JsonSerializer.Deserialize<Result<string>>(JsonSerializer.Serialize(Result<string>.Success("ok"), Json), Json)!;
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("ok"));
            Assert.That(result.Error, Is.EqualTo(Error.None));
        });
    }

    [Test]
    public void Serialization_ShouldPreserveFailedResult()
    {
        var error = Error.NotFound("Execution");
        var result = JsonSerializer.Deserialize<Result<string>>(JsonSerializer.Serialize(Result<string>.Failure(error), Json), Json)!;
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error, Is.EqualTo(error));
        });
    }
}
