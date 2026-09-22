using BrainArena.Api.Extensions;
using BrainArena.Application.Tournaments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BrainArena.Api.Controllers;

[ApiController]
[Route("api/tournaments")]
[Authorize]
public class TournamentsController(ITournamentService tournamentService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<TournamentSummaryDto>>> GetOpenTournaments(CancellationToken ct)
    {
        return Ok(await tournamentService.GetOpenTournamentsAsync(ct));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<TournamentDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await tournamentService.GetTournamentDetailAsync(id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<TournamentDetailDto>> Create(CreateTournamentRequest request, CancellationToken ct)
    {
        var tournament = await tournamentService.CreateTournamentAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = tournament.Id }, tournament);
    }

    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<TournamentDetailDto>> Join(Guid id, CancellationToken ct)
    {
        return Ok(await tournamentService.JoinTournamentAsync(User.GetUserId(), id, ct));
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await tournamentService.LeaveTournamentAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        await tournamentService.StartTournamentAsync(id, User.GetUserId(), ct);
        return NoContent();
    }
}
