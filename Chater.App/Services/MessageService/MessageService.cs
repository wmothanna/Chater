using Chater.Data.DTOs;
using Chater.Data.Mappings;
using Chater.Data.Model.Entities;
using Chater.Data.Repository;

namespace Chater.App.Services;

public class MessageService
(
    IBaseRepository<RoomMember> _roomMemberRepo
    , IBaseRepository<Room> _roomRepo
    , IServiceResultFactory _resFactory
) : IMessageService
{
    public async Task<ServiceResult<IEnumerable<MessageResponseDto>>> GetAll(int uid, string roomName)
    {
        Room? room = await _roomRepo.GetFirstAsync(r => r.Name.Equals(roomName));
        if (room is null)
            return _resFactory.Failure<IEnumerable<MessageResponseDto>>(error: $"no such a room with the name {roomName}", StatusCodes.Status404NotFound, "ROOM_NOT_FOUND");

        var roomMember =  await _roomMemberRepo.GetFirstAsync(rm => rm.MemeberId.Equals(uid) && rm.RoomId.Equals(room.Id), [["Room", "Messages", "Sender"]]);
        bool isMember = roomMember is not null ? true : false;
        if (! isMember)
            return _resFactory.Failure<IEnumerable<MessageResponseDto>>(error: $"You must be a member to access messages", StatusCodes.Status403Forbidden, "NOT_MEMBER");
        var messagesDtos = roomMember!.Room.Messages.Select(m => m.MapToDto());

        return _resFactory.Success<IEnumerable<MessageResponseDto>>(data: messagesDtos);
    }
}
