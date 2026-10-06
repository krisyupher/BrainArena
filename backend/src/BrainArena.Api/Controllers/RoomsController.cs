using BrainArena.Api.Auth;
using BrainArena.Api.Extensions;
using BrainArena.Api.RateLimiting;
using BrainArena.Application.Chat;
using BrainArena.Application.Matches;
using BrainArena.Application.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    [Authorize(Policy = AuthPolicies.RegisteredUser)]
    public async Task<ActionResult<RoomDetailDto>> GetByCode(string code, CancellationToken ct)
    {
        return Ok(await roomService.GetRoomByShareCodeAsync(code, ct));
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.RoomCreation)]
    public async Task<ActionResult<CreateRoomResult>> Create(CreateRoomRequest request, CancellationToken ct)
    {
        var result = await roomService.CreateRoomAsync(User.GetUserIdOrNull(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Room.Id }, result);
    }

    [HttpPost("{id:guid}/join")]
    [Authorize(Policy = AuthPolicies.RegisteredUser)]
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
