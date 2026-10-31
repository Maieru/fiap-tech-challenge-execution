namespace FIAP.TechChallenge.Execution.Domain.Abstractions;

public record Error(string Description, ErrorCode Code = ErrorCode.BadRequest)
{
    public static Error None { get; } = new("No error occurred.", ErrorCode.None);

    public static Error InvalidId(string entity) => new($"Invalid {entity} id.", ErrorCode.BadRequest);

    public static Error NotFound(string entity) => new($"{entity} not found.", ErrorCode.NotFound);

    public static Error NoAccess(string entity) => new($"You do not have access to this {entity}.", ErrorCode.Forbidden);

    public static Error Unauthorized(string description) => new(description, ErrorCode.Unauthorized);

    public static Error Conflict(string description) => new(description, ErrorCode.Conflict);

    public static Error InvalidValue(string entity, string field, string value) => new($"Invalid value '{value}' for field '{field}' in {entity}.", ErrorCode.BadRequest);
}
