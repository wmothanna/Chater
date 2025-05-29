using System.Collections;
using System.Text.RegularExpressions;

using Chater.App.Services;
using Chater.Data.DTOs;
using Chater.Data.Mappings;
using Chater.Data.Model.Entities;
using Chater.Data.Repository;
using Microsoft.Extensions.Options;

public class ChatRoomService
(
  IBaseRepository<Room> _roomRepo
  , IServiceResultFactory _resFactory
  , ILogger<ChatRoomService> _logger
  , IOptions<UserOptions> _userOpts
  , IBaseRepository<RoomMember> _roomMemberRepo
) : IChatRoomService
{
  public async Task<ServiceResult> CreateRoom(int uid, ChatRoomRequestDto dto)
  {
    _logger.LogInformation("Started chat room creation.");

    dto.Name = dto.Name.Trim().Normalize();
    dto.Description = dto.Description.Trim().Normalize();
    
    if (_roomRepo.Where(r => r.CreatedById == uid).Count() == _userOpts.Value.ChatRoomsLimit )
      return _resFactory.Failure("User hit chat rooms limit", StatusCodes.Status409Conflict, "ROOM_LIMIT_HIT");

    var checkRoomNameResult = CheckRoomName(dto.Name);
    if (! checkRoomNameResult.IsSuccess)
      return checkRoomNameResult; 

    if (await _roomRepo.GetSingleAsync(r => r.Name == dto.Name) is not null)
      return _resFactory.Failure("Chat room name already used", StatusCodes.Status409Conflict, "ROOM_NAME_USED");

    var room = dto.MapToRoom();
    room.CreatedById = uid;
    await _roomRepo.AddAsync(room);
    RoomMember roomMember = new(){
      MemeberId = uid,
      RoomId = room.Id,
      JoinedAt = DateTime.UtcNow
    };
    await _roomMemberRepo.AddAsync(roomMember);

    _logger.LogInformation("Finished chat room creation.");
    return _resFactory.Success();
  }

  public async Task<ServiceResult> DeleteRoom(int uid, string roomName)
  {
    _logger.LogInformation("Started chat room deletion.");

    roomName = roomName.Trim().Normalize();

    var room = await _roomRepo.GetSingleAsync(r => r.Name.Equals(roomName));
    if (room is null)
      return _resFactory.Failure($"No such room with room name \"{roomName}\"", StatusCodes.Status404NotFound, "ROOM_NOT_FOUND");
    
    if (room.CreatedById != uid)
      return _resFactory.Failure(string.Empty, StatusCodes.Status403Forbidden, "NOT_OWNED_ROOM");
    
    await _roomRepo.RemoveAsync(room);
    _logger.LogInformation("Finished chat room deletion.");
    return _resFactory.Success();
  }


  public async Task<ServiceResult<ChatRoomResponseDto>> GetByName(string roomName)
  {
    var rawRoom = await _roomRepo.GetSingleAsync(r => r.Name.Equals(roomName));
    if (rawRoom is null)
      return _resFactory.Failure<ChatRoomResponseDto>($"No such room with room name \"{roomName}\"", StatusCodes.Status404NotFound, "ROOM_NOT_FOUND");

    var dto = new ChatRoomResponseDto{
      Name = rawRoom.Name,
      Description = rawRoom.Description,
      AvatarUrl = rawRoom.RoomAvatarUrl ?? string.Empty
    };
    return _resFactory.Success(dto);
  }

  public ServiceResult<IEnumerable<ChatRoomResponseDto>> GetJoined(int uid)
  {
    var rawRooms = _roomMemberRepo.GetAll(rm => rm.MemeberId.Equals(uid), [nameof(Room)]);
    var dtos = rawRooms.Select(r => 
    new ChatRoomResponseDto{
      Name = r.Room.Name,
      Description = r.Room.Description,
      AvatarUrl = r.Room.RoomAvatarUrl ?? string.Empty
    });
    return _resFactory.Success(dtos);
  }

  public ServiceResult<IEnumerable<ChatRoomResponseDto>> GetOwned(int uid)
{
  var rawRooms = _roomRepo.GetAll(r => r.CreatedById == uid);
  var dtos = rawRooms.Select(r => 
  new ChatRoomResponseDto{
    Name = r.Name,
    Description = r.Description,
    AvatarUrl = r.RoomAvatarUrl ?? string.Empty
  });
  return _resFactory.Success(dtos);
}

  private ServiceResult CheckRoomName(string name){
    int nameMaxLengthInDb = 128;

    if (string.IsNullOrEmpty(name))
      return _resFactory.Failure($"chat room name can't be empty", StatusCodes.Status400BadRequest, "ROOM_NAME_REQUIRED");

    if (name.Length >= nameMaxLengthInDb)
      return _resFactory.Failure($"chat room name can't be more than {nameMaxLengthInDb}", StatusCodes.Status400BadRequest, "LONG_ROOM_NAME");

    var pattern = @"^[\p{L}\p{M}\p{N} _-]+$"; // regex pattern that allows all characters from all languages, numbers, hyphens, underscors and whitspaces.
    if (! Regex.IsMatch(name, pattern))
      return _resFactory.Failure($"chat room name has invalid format", StatusCodes.Status400BadRequest, "ROOM_NAME_FORMAT_ERR");

    return _resFactory.Success();
  }
}
