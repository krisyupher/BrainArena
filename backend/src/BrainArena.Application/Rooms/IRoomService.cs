namespace BrainArena.Application.Rooms;

public interface IRoomService
{
    Task<IReadOnlyList<RoomSummaryDto>> GetOpenRoomsAsync(CancellationToken ct = default);

    /// <summary>
    /// hostUserId is null for an anonymous caller — only permitted when request.Kind is Solitary,
    /// in which case a throwaway guest account is created and CreateRoomResult.GuestAuth carries its
    /// freshly-minted session. Every other Kind with a null hostUserId throws 401.
    /// </summary>
    Task<CreateRoomResult> CreateRoomAsync(Guid? hostUserId, CreateRoomRequest request, CancellationToken ct = default);
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
