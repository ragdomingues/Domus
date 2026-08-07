namespace Domus.Application.Common;

public class Result
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }

    public static Result Success() => new() { Succeeded = true };

    public static Result Failure(string error, string? errorCode = null) =>
        new() { Succeeded = false, Error = error, ErrorCode = errorCode };
}

public class Result<T> : Result
{
    public T? Value { get; init; }

    public static Result<T> Success(T value) =>
        new() { Succeeded = true, Value = value };

    public new static Result<T> Failure(string error, string? errorCode = null) =>
        new() { Succeeded = false, Error = error, ErrorCode = errorCode };
}
