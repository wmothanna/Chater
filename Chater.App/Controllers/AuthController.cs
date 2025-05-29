using Chater.App.Services;
using Chater.Data.Model.DTOs;
using Chater.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chater.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService _authService) : ControllerBase
{
  [HttpPost("register")]
  public async Task<IActionResult> RegisterAsync(RegistrationRequestDto dto){
    ServiceResult result = null!;
    result = await _authService.RegisterAsync(dto);

    if (result.StatusCode.Equals(StatusCodes.Status400BadRequest))
      return BadRequest(result);
    else if (result.StatusCode.Equals(StatusCodes.Status409Conflict))
      return Conflict(result);
    else
      return Ok(result);
  }

  [HttpPost("login")]
  public async Task<ActionResult<string>> LoginAsync(LoginRequestDto dto){
    ServiceResult<string> result = null!;
    result = await _authService.LoginAsync(dto);

    if (result.StatusCode.Equals(StatusCodes.Status400BadRequest))
      return BadRequest(result);
    else
      return Ok(result);
    }
}