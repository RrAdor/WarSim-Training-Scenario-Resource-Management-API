namespace WarSim.DTOs;

public enum ServiceErrorType
{
    None,
    NotFound,
    BadRequest,
    Conflict
}

public class ServiceResult<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public ServiceErrorType ErrorType { get; }

    private ServiceResult(bool success, T? value, string? errorMessage, ServiceErrorType errorType)
    {
        Success = success;
        Value = value;
        ErrorMessage = errorMessage;
        ErrorType = errorType;
    }

    public static ServiceResult<T> Ok(T value) =>
        new(true, value, null, ServiceErrorType.None);

    public static ServiceResult<T> NotFound(string message = "Resource not found.") =>
        new(false, default, message, ServiceErrorType.NotFound);

    public static ServiceResult<T> BadRequest(string message) =>
        new(false, default, message, ServiceErrorType.BadRequest);

    public static ServiceResult<T> Conflict(string message) =>
        new(false, default, message, ServiceErrorType.Conflict);
}

public class ServiceResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public ServiceErrorType ErrorType { get; }

    private ServiceResult(bool success, string? errorMessage, ServiceErrorType errorType)
    {
        Success = success;
        ErrorMessage = errorMessage;
        ErrorType = errorType;
    }

    public static ServiceResult Ok() =>
        new(true, null, ServiceErrorType.None);

    public static ServiceResult NotFound(string message = "Resource not found.") =>
        new(false, message, ServiceErrorType.NotFound);

    public static ServiceResult BadRequest(string message) =>
        new(false, message, ServiceErrorType.BadRequest);

    public static ServiceResult Conflict(string message) =>
        new(false, message, ServiceErrorType.Conflict);
}

