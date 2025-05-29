using System.Security.Permissions;

namespace Chater.App.Services;

public class ServiceResultFactory : IServiceResultFactory
{
    public ServiceResult Success() => new() { IsSuccess = true };
    
    public ServiceResult<T> Success<T>(T data, string code = "SUCCESS") => new() { 
        IsSuccess = true, 
        Data = data ,
        Code = code
    };
    
    public ServiceResult Failure(string error, int statusCode, string code) => new() { 
        IsSuccess = false, 
        ErrorMessage = error, 
        StatusCode = statusCode,
        Code = code
    };
    
    public ServiceResult<T> Failure<T>(string error, int statusCode, string code) => new() { 
        IsSuccess = false, 
        ErrorMessage = error, 
        StatusCode = statusCode,
        Code = code 
    };
}