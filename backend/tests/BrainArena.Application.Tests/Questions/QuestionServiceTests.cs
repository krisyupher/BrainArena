using BrainArena.Application.Common;
using BrainArena.Application.Questions;
using BrainArena.Application.Tests.Fakes;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tests.Questions;

public class QuestionServiceTests
{
    private static QuestionUpsertRequest Request(RoomTopic topic = RoomTopic.Chemistry, string text = "What is H2O?") => new(
        Topic: topic,
        Difficulty: 1,
        Text: text,
        Options: ["Salt", "Water", "Sugar", "Oil"],
        CorrectOptionIndex: 1,
        Explanation: "H2O is the chemical formula for water.",
        Language: "en");

    private static (QuestionService Service, FakeQuestionRepository Repo) BuildService()
    {
        var repo = new FakeQuestionRepository();
        return (new QuestionService(repo), repo);
    }

    [Fact]
    public async Task CreateAsync_AddsAValidQuestion()
    {
        var (service, repo) = BuildService();

        var created = await service.CreateAsync(Request());

        Assert.Equal(1, await repo.CountAsync());
        Assert.Equal("What is H2O?", created.Text);
    }

    [Fact]
    public async Task CreateAsync_RejectsAnInvalidQuestion()
    {
        var (service, _) = BuildService();
        var invalid = Request() with { Options = ["only one"] };

        await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(invalid));
    }

    [Fact]
    public async Task UpdateAsync_ChangesAnExistingQuestion()
    {
        var (service, _) = BuildService();
        var created = await service.CreateAsync(Request());

        var updated = await service.UpdateAsync(created.Id, Request(text: "What is the boiling point of water?"));

        Assert.Equal("What is the boiling point of water?", updated.Text);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsForAnUnknownId()
    {
        var (service, _) = BuildService();

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.UpdateAsync(Guid.NewGuid(), Request()));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByTopicWhenProvided()
    {
        var (service, _) = BuildService();
        await service.CreateAsync(Request(RoomTopic.Chemistry, "Chem question"));
        await service.CreateAsync(Request(RoomTopic.Math, "Math question"));

        var chemistryOnly = await service.GetAllAsync(RoomTopic.Chemistry);

        var q = Assert.Single(chemistryOnly);
        Assert.Equal("Chem question", q.Text);
    }

    [Fact]
    public async Task ImportAsync_AddsValidItemsAndReportsErrorsForInvalidOnes()
    {
        var (service, repo) = BuildService();
        var requests = new List<QuestionUpsertRequest>
        {
            Request(text: "Valid question one"),
            Request(text: "Valid question two"),
            Request() with { Options = ["too", "few"] }
        };

        var result = await service.ImportAsync(requests);

        Assert.Equal(2, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Equal(2, await repo.CountAsync());
    }
}
