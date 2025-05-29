using System.Security.Claims;
using Chater.App.Services;
using Chater.Data.DTOs;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chater.App.Controllers;

[ApiController]
[Route("api/chat-room")]
[Authorize]
public class ChatRoomController(IChatRoomService _roomSrvc, IMessageService _msgSrvc) : ControllerBase
{
  [HttpGet("owned")]
  public ActionResult<IEnumerable<ChatRoomResponseDto>> GetOwned(){
    int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var result = _roomSrvc.GetOwned(uid);
    return Ok(result.Data);
  }
  [HttpGet("joined")]
  public ActionResult<IEnumerable<ChatRoomResponseDto>> GetJoined(){
    int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var result = _roomSrvc.GetJoined(uid);
    return Ok(result.Data);
  }

  [HttpGet]
  public async Task<ActionResult<ChatRoomResponseDto>> GetByName([FromQuery] string roomName){
    var result = await _roomSrvc.GetByName(roomName);
    if (result.StatusCode.Equals(StatusCodes.Status404NotFound))
      return NotFound(result);
    return Ok(result.Data);
  }

  [HttpPost]
  public async Task<IActionResult> Create([FromBody] ChatRoomRequestDto dto){
    int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var result = await _roomSrvc.CreateRoom(uid, dto);

    if (result.StatusCode.Equals(StatusCodes.Status400BadRequest))
      return BadRequest(result);
    if (result.StatusCode.Equals(StatusCodes.Status409Conflict))
      return BadRequest(result);

    return Created();
  }
  [HttpDelete]
  public async Task<IActionResult> Delete(string roomName){
    int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var result = await _roomSrvc.DeleteRoom(uid, roomName);

    if (result.StatusCode.Equals(StatusCodes.Status404NotFound))
      return NotFound(result);
    if (result.StatusCode.Equals(StatusCodes.Status403Forbidden))
      return Forbid(BearerTokenDefaults.AuthenticationScheme);

    return Ok();
  }

  [HttpGet("{roomName}")]
  public async Task<ActionResult<IEnumerable<MessageResponseDto>>> GetAll([FromRoute] string roomName){
      int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
      var result = await _msgSrvc.GetAll(uid, roomName);
      if (result.StatusCode.Equals(StatusCodes.Status404NotFound))
          return NotFound(result);
      if (result.StatusCode.Equals(StatusCodes.Status403Forbidden))
          return Forbid();
      return Ok(result.Data);
  }
}
