namespace Chater.App.Services;

public interface IServiceResultFactory
{
    ServiceResult Success();
    ServiceResult<T> Success<T>(T data, string code = "SUCCESS");
    ServiceResult Failure(string error, int statusCode, string code);
    ServiceResult<T> Failure<T>(string error, int statusCode, string code);
}
