namespace AktieKoll.Dtos;

public record ServiceResult<T>(T? Value, string? Error = null, int StatusCode = 200)
{
    public bool IsSuccess => Error is null;
    public static ServiceResult<T> Ok(T value) => new(value);
    public static ServiceResult<T> Fail(string error, int code) => new(default, error, code);
}