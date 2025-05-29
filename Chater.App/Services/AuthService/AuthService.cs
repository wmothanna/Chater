using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Chater.Options;
using Chater.Data.Repository;
using Chater.Data.Model.Entities;
using Chater.App.Services;
using Chater.Data.Model.DTOs;
using Chater.Data.Mappings;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;

namespace Chater.Services;

public class AuthService
( IOptions<JwtOptions> _jwtOpts
  , IBaseRepository<User> _userRepo
  , IServiceResultFactory _serviceResultFactory
) : IAuthService
{

  private static readonly Regex _usernameRegex = new Regex(@"^[\p{L}\p{Mn}0-9_-]+$", RegexOptions.Compiled);
  private static readonly Regex _passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$", RegexOptions.Compiled);

  public async Task<ServiceResult> RegisterAsync(RegistrationRequestDto dto)
  {
    dto.Username = dto.Username.Trim();
    dto.Email = dto.Email.Trim();
    dto.Password = dto.Password.Trim();

    if (string.IsNullOrEmpty(dto.Username)) return _serviceResultFactory.Failure("Username required", StatusCodes.Status400BadRequest, "USERNAME_REQUIRED");
    if (string.IsNullOrEmpty(dto.Email)) return _serviceResultFactory.Failure("Email required", StatusCodes.Status400BadRequest, "EMAIL_REQUIRED");
    if (string.IsNullOrEmpty(dto.Password)) return _serviceResultFactory.Failure("Password required", StatusCodes.Status400BadRequest, "PWD_REQUIRED");

    var validateUsername = ValidateUsername(dto.Username);
    if (!validateUsername.IsSuccess) return validateUsername;

    dto.Email = dto.Email.ToLower();
    var validateEmail = ValidateEmail(dto.Email);
    if (!validateEmail.IsSuccess) return validateEmail;

    if (await _userRepo.AnyAsync(u => u.Username == dto.Username || u.Email == dto.Email))
      return _serviceResultFactory.Failure("User with username or email already exists", StatusCodes.Status409Conflict, "USER_EXISTS");

    var pwdCheckRes = CheckPassword(dto.Password);
    if (! pwdCheckRes.IsSuccess) return pwdCheckRes;

    var user = dto.MapToUser();
    var HashedPwd = new PasswordHasher<User>().HashPassword(user, dto.Password);
    user.PasswordHash = HashedPwd;

    try {
      await _userRepo.AddAsync(user);
    }
    catch{
      return _serviceResultFactory.Failure("Something went wrong in registration process", StatusCodes.Status409Conflict, "GENERIC_ERR");
    }
    return _serviceResultFactory.Success();
  }
  public async Task<ServiceResult<string>> LoginAsync(LoginRequestDto dto)
  {
    dto.UsernameOrEmail = dto.UsernameOrEmail.Trim();
    dto.Password = dto.Password.Trim();

    if (string.IsNullOrEmpty(dto.UsernameOrEmail)) return _serviceResultFactory.Failure<string>("Username Or Email field required", StatusCodes.Status400BadRequest, "USERNAME_EMAIL_REQUIRED");
    if (string.IsNullOrEmpty(dto.Password)) return _serviceResultFactory.Failure<string>("Password field required", StatusCodes.Status400BadRequest, "PWD_REQUIRED");
    
    User? user = await _userRepo.GetSingleAsync(u => u.Username == dto.UsernameOrEmail || u.Email == dto.UsernameOrEmail.ToLower());
    if
    (
      user is null
      || (new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
    ){ return _serviceResultFactory.Failure<string>("Invalid username, email or password", StatusCodes.Status400BadRequest, "INVALID_CREDENTIALS");}

    string jwt = GenerateJwt(user);    
    return _serviceResultFactory.Success(jwt);
  }

  private string GenerateJwt(User user)
  {
    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenDescriptor = new SecurityTokenDescriptor {
      Issuer = _jwtOpts.Value.Issuer,
      Audience = _jwtOpts.Value.Audience,
      SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOpts.Value.SigningKey)), SecurityAlgorithms.HmacSha512),

      Subject = new ClaimsIdentity(
        new List<Claim> {
          new (ClaimTypes.NameIdentifier, user.Id.ToString()),
          new (ClaimTypes.Name, user.Username),
          new (ClaimTypes.Email, user.Email),
        }
      )
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
  }
  private ServiceResult ValidateUsername(string username){
    if (username.Length > 64)
      return _serviceResultFactory.Failure("username length must not exceed 64 digit", StatusCodes.Status400BadRequest, "LONG_USERNAME");

    if (!_usernameRegex.IsMatch(username))
      return _serviceResultFactory.Failure("Username allowed characters are [[0 - 9] [lettern in any language] [_, -]]", StatusCodes.Status400BadRequest, "USERNAME_FORMAT_ERR");

    return _serviceResultFactory.Success();
  }
  private ServiceResult ValidateEmail(string email)
  {
    if (email.Length > 255)
      return _serviceResultFactory.Failure("email length must not exceed 255 digit", StatusCodes.Status400BadRequest, "LONG_EMAIL");

    (string msg, int status, string code) emailFailure = ("email format error", StatusCodes.Status400BadRequest, "EMAIL_FORMAT_ERR");

    if (email.EndsWith("."))
      return _serviceResultFactory.Failure(emailFailure.msg, emailFailure.status, emailFailure.code);

    try {
      var addr = new System.Net.Mail.MailAddress(email);
      if (addr.Address == email)
        return _serviceResultFactory.Success();
      else
        return _serviceResultFactory.Failure(emailFailure.msg, emailFailure.status, emailFailure.code);
    }
    catch {
      return _serviceResultFactory.Failure(emailFailure.msg, emailFailure.status, emailFailure.code);
    }
  }
  private ServiceResult CheckPassword(string password)
  {
    if (password.Length < 12)
      return _serviceResultFactory.Failure("Password length must at least consist of 12 digits", StatusCodes.Status400BadRequest, "SHORT_PWD");
    if (password.Length > 255)
      return _serviceResultFactory.Failure("Password can't be more than 255 degit", StatusCodes.Status400BadRequest, "LONG_PWD");
    if (!_passwordRegex.IsMatch(password))
      return _serviceResultFactory.Failure("password is weak", StatusCodes.Status400BadRequest, "WEAK_PWD");
    return _serviceResultFactory.Success();
  }
}
