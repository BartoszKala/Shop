using System.Diagnostics.CodeAnalysis;

namespace ShopService.Domain.Common;

public enum ErrorType
{
    Validation,
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict
}

public sealed record Error(ErrorType Type, string Code, string Message, IReadOnlyDictionary<string, string[]>? Details = null)
{
    public const string ValidationCode = "validation";

    public static Error Validation(string field, string message) =>
        new(ErrorType.Validation, ValidationCode, message, new Dictionary<string, string[]> { [field] = [message] });

    public static Error BadRequest(string code, string message) => new(ErrorType.BadRequest, code, message);
    public static Error Unauthorized(string code, string message) => new(ErrorType.Unauthorized, code, message);
    public static Error Forbidden(string code, string message) => new(ErrorType.Forbidden, code, message);
    public static Error NotFound(string code, string message) => new(ErrorType.NotFound, code, message);
    public static Error Conflict(string code, string message) => new(ErrorType.Conflict, code, message);
}

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static implicit operator Result(Error error) => new(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;

    private Result(Error error) : base(error)
    {
    }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public Result<TOut> Map<TOut>(Func<T, TOut> map) => IsSuccess ? map(Value) : Error;

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}
