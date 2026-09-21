using BrainArena.Application.Questions;
using BrainArena.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BrainArena.Api.Controllers;

[ApiController]
[Route("api/questions")]
[Authorize(Roles = "Admin")]
public class QuestionsController(IQuestionService questionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuestionDto>>> GetAll([FromQuery] RoomTopic? topic, CancellationToken ct)
    {
        return Ok(await questionService.GetAllAsync(topic, ct));
    }

    [HttpPost]
    public async Task<ActionResult<QuestionDto>> Create(QuestionUpsertRequest request, CancellationToken ct)
    {
        var created = await questionService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<QuestionDto>> Update(Guid id, QuestionUpsertRequest request, CancellationToken ct)
    {
        return Ok(await questionService.UpdateAsync(id, request, ct));
    }

    [HttpPost("import")]
    public async Task<ActionResult<QuestionImportResult>> Import(List<QuestionUpsertRequest> requests, CancellationToken ct)
    {
        return Ok(await questionService.ImportAsync(requests, ct));
    }
}
