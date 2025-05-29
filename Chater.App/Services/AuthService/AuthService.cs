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

namespace Chater.Services;

public class AuthService
( IOptions<JwtOptions> _jwtOpts
  , IBaseRepository<User> _userRepo
  , IServiceResultFactory _serviceResultFactory
) : IAuthService
{
  public async Task<ServiceResult> RegisterAsync(RegistrationRequestDto dto)
  {
    dto.Username = dto.Username.Trim();
    dto.Email = dto.Email.Trim();
    dto.Password = dto.Password.Trim();

    if (string.IsNullOrEmpty(dto.Username)) return _serviceResultFactory.Failure("Username required", StatusCodes.Status400BadRequest, "USERNAME_REQUIRED");
    if (string.IsNullOrEmpty(dto.Email)) return _serviceResultFactory.Failure("Email required", StatusCodes.Status400BadRequest, "EMAIL_REQUIRED");
    if (string.IsNullOrEmpty(dto.Password)) return _serviceResultFactory.Failure("Password required", StatusCodes.Status400BadRequest, "PWD_REQUIRED");

    if (!IsValidUsername(dto.Username)) return _serviceResultFactory.Failure("Username must only contain [[0 - 1] | [a - b] | [A - B] | [أ - ي]]", StatusCodes.Status400BadRequest, "USERNAME_FORMAT_ERR");

    dto.Email = dto.Email.ToLower();
    if (!IsValidEmail(dto.Email))  return _serviceResultFactory.Failure("Incorrect email format", StatusCodes.Status400BadRequest, "EMAIL_FORMAT_ERR");

    if (await _userRepo.AnyAsync(u => u.Username == dto.Username || u.Email == dto.Email))
      return _serviceResultFactory.Failure("User with username or email already exists", StatusCodes.Status409Conflict, "USERNAME_USED");

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
  private bool IsValidUsername(string username){
    if (username.Length > 64)
      return false;
    return true;
  }
  private bool IsValidEmail(string email)
  {
    if (email.Length > 255 || email.EndsWith("."))
      return false;

    try {
      var addr = new System.Net.Mail.MailAddress(email);
      return addr.Address == email;
    }
    catch {
      return false;
    }
  }
  private ServiceResult CheckPassword(string Password)
  {
    if (Password.Length < 12)
      return _serviceResultFactory.Failure("Password length must at least consist of 12 degits", StatusCodes.Status400BadRequest, "SHORT_PWD");
    if (Password.Length > 255)
      return _serviceResultFactory.Failure("Password can't be more than 255 degit", StatusCodes.Status400BadRequest, "LONG_PWD");
    return _serviceResultFactory.Success();
  }
}
