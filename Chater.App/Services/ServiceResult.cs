namespace Chater.App.Services;

public class ServiceResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public string Code { get; set; } = null!;
    public int StatusCode { get; set; }
}

public class ServiceResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string Code { get; set; } = null!;
    public int StatusCode { get; set; }
}
