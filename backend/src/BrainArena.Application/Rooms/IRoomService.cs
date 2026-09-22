namespace BrainArena.Application.Rooms;

public interface IRoomService
{
    Task<IReadOnlyList<RoomSummaryDto>> GetOpenRoomsAsync(CancellationToken ct = default);
    Task<RoomDetailDto> CreateRoomAsync(Guid hostUserId, CreateRoomRequest request, CancellationToken ct = default);
    Task<RoomDetailDto> JoinRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default);
    Task LeaveRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default);
    Task<RoomDetailDto> GetRoomByShareCodeAsync(string shareCode, CancellationToken ct = default);
    Task<RoomDetailDto> GetRoomDetailAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// Same as <see cref="GetRoomDetailAsync(Guid, CancellationToken)"/>, but for call sites
    /// reachable by anonymous or non-member callers (guest viewing): a private room is reported
    /// as not found to anyone who isn't a member, requesting user included.
    /// </summary>
    Task<RoomDetailDto> GetVisibleRoomDetailAsync(Guid roomId, Guid? requestingUserId, CancellationToken ct = default);
}
