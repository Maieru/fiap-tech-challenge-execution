using System.Text.Json;
using System.Text.Json.Serialization;

namespace FIAP.TechChallenge.Execution.Domain.Abstractions;

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Value { get; private set; }
    public Error Error { get; private set; }

    [JsonConstructor]
    private Result(bool isSuccess, T? value, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (isSuccess && error.Code != ErrorCode.None)
            throw new JsonException("IsSuccess=true exige ErrorCode.None.");

        if (!isSuccess && value is not null)
            throw new JsonException("IsSuccess=false exige Value=null.");

        if (!isSuccess && error.Code == ErrorCode.None)
            throw new JsonException("IsSuccess=false exige um erro com ErrorCode diferente de None.");

        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = Error.None;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        Value = default;
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public static Result<T> Success(T value) => new Result<T>(value);
    public static Result<T> Failure(Error error) => new Result<T>(error);
}
