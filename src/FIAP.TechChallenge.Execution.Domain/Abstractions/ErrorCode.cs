namespace FIAP.TechChallenge.Execution.Domain.Abstractions;

public enum ErrorCode
{
    None = 0,
    BadRequest = 1,
    NotFound = 2,
    Unauthorized = 3,
    Forbidden = 4,
    Conflict = 5,
    UnprocessableEntity = 6,
    InternalServerError = 7
}
