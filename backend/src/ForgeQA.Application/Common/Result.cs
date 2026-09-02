namespace ForgeQA.Application.Common;

public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized
}

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public ErrorType ErrorType { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, ErrorType errorType, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorType = errorType;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, ErrorType.None, null);
    public static Result<T> Failure(ErrorType errorType, string error) => new(false, default, errorType, error);
}
