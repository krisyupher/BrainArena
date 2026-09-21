namespace BrainArena.Application.Rooms;

public interface IRoomService
{
    Task<IReadOnlyList<RoomSummaryDto>> GetOpenRoomsAsync(CancellationToken ct = default);
    Task<RoomDetailDto> CreateRoomAsync(Guid hostUserId, CreateRoomRequest request, CancellationToken ct = default);
    Task<RoomDetailDto> JoinRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default);
    Task LeaveRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default);
    Task<RoomDetailDto> GetRoomByShareCodeAsync(string shareCode, CancellationToken ct = default);
    Task<RoomDetailDto> GetRoomDetailAsync(Guid roomId, CancellationToken ct = default);
}
