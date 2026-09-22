using BrainArena.Api.Extensions;
using BrainArena.Application.Chat;
using BrainArena.Application.Matches;
using BrainArena.Application.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BrainArena.Api.Controllers;

[ApiController]
[Route("api/rooms")]
[Authorize]
public class RoomsController(
    IRoomService roomService,
    IMatchResultsService matchResultsService,
    IChatService chatService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<RoomSummaryDto>>> GetOpenRooms(CancellationToken ct)
    {
        return Ok(await roomService.GetOpenRoomsAsync(ct));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<RoomDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await roomService.GetVisibleRoomDetailAsync(id, User.GetUserIdOrNull(), ct));
    }

    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<RoomDetailDto>> GetByCode(string code, CancellationToken ct)
    {
        return Ok(await roomService.GetRoomByShareCodeAsync(code, ct));
    }

    [HttpPost]
    public async Task<ActionResult<RoomDetailDto>> Create(CreateRoomRequest request, CancellationToken ct)
    {
        var room = await roomService.CreateRoomAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = room.Id }, room);
    }

    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<RoomDetailDto>> Join(Guid id, CancellationToken ct)
    {
        return Ok(await roomService.JoinRoomAsync(User.GetUserId(), id, ct));
    }

    [HttpGet("{id:guid}/results")]
    public async Task<ActionResult<MatchResultsDto>> GetResults(Guid id, CancellationToken ct)
    {
        return Ok(await matchResultsService.GetResultsByRoomAsync(id, ct));
    }

    [HttpGet("{id:guid}/chat")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetChatHistory(Guid id, CancellationToken ct)
    {
        // Guests can read chat but not send — reuse the same private-room visibility rule as GetById.
        await roomService.GetVisibleRoomDetailAsync(id, User.GetUserIdOrNull(), ct);
        return Ok(await chatService.GetHistoryAsync(id, ct));
    }
}
