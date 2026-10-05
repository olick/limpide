using Limpide.Core.Answering;
using Limpide.Core.Search;
using Microsoft.Extensions.AI;

namespace Limpide.Core.Tests.Answering;

public class AnswerServiceTests
{
    private static readonly RetrievedPassage Article5 = Passage("Article 5 — Pratiques interdites en matière d’IA", "art_5",
        "1. Les pratiques en matière d’IA suivantes sont interdites:");

    private static readonly RetrievedPassage Recital29 = Passage("Considérant (29)", "rct_29",
        "(29) Les techniques de manipulation fondées sur l’IA peuvent être utilisées...");

    private static readonly GenerationSettings Settings = new("modele-test", new ModelPrice(InputPerMillion: 1m, OutputPerMillion: 2m));

    [Fact]
    public async Task Sends_the_passages_verbatim_with_their_identifier()
    {
        var chat = new FakeChatClient("Interdit [P1].");

        await Service(chat).AskAsync("Qu'est-ce qui est interdit ?", CancellationToken.None);

        var user = chat.LastMessages!.Single(m => m.Role == ChatRole.User).Text;
        Assert.Contains("[P1] Article 5 — Pratiques interdites en matière d’IA (EUR-Lex, AI Act)", user);
        Assert.Contains(Article5.Content, user);
        Assert.Contains("[P2] Considérant (29)", user);
        Assert.EndsWith("Question : Qu'est-ce qui est interdit ?", user);
        Assert.Equal("modele-test", chat.LastOptions!.ModelId);
    }

    [Fact]
    public async Task Marks_cited_passages_and_computes_the_cost()
    {
        var chat = new FakeChatClient("Interdit [P1].", inputTokens: 2_000, outputTokens: 500);

        var result = await Service(chat).AskAsync("?", CancellationToken.None);

        Assert.Equal(["P1"], result.CitedPassages.Select(p => p.Id));
        Assert.Equal((2_000 * 1m + 500 * 2m) / 1_000_000m, result.Usage.EstimatedCost);
        Assert.Empty(result.Guardrails);
        Assert.Equal(AnswerPrompt.Version, result.PromptVersion);
    }

    [Fact]
    public async Task An_invented_citation_triggers_a_guardrail()
    {
        var result = await Service(new FakeChatClient("Interdit [P1] et [P9].")).AskAsync("?", CancellationToken.None);

        Assert.Equal(["citation-inventee"], result.Guardrails.Select(g => g.Code));
    }

    [Fact]
    public async Task An_answer_without_citation_triggers_a_guardrail()
    {
        var result = await Service(new FakeChatClient("C'est interdit.")).AskAsync("?", CancellationToken.None);

        Assert.Equal(["sans-citation"], result.Guardrails.Select(g => g.Code));
    }

    [Fact]
    public async Task Declining_to_answer_is_not_a_missing_citation()
    {
        var result = await Service(new FakeChatClient(AnswerPrompt.Decline)).AskAsync("?", CancellationToken.None);

        Assert.True(result.Declined);
        Assert.Empty(result.Guardrails);
    }

    [Fact]
    public async Task Cost_is_unknown_rather_than_guessed_when_the_price_is_not_configured()
    {
        var service = new AnswerService(new FakeSearch(Article5), new FakeChatClient("[P1]"), Settings with { Price = null });

        var result = await service.AskAsync("?", CancellationToken.None);

        Assert.Null(result.Usage.EstimatedCost);
        Assert.Equal(1_000, result.Usage.InputTokens);
    }

    [Fact]
    public async Task Below_the_relevance_threshold_the_model_is_not_called()
    {
        var chat = new FakeChatClient("[P1]");
        var service = new AnswerService(new FakeSearch(Article5), chat, Settings with { MinScore = 0.8 }); // score 0,7

        var result = await service.AskAsync("Quelle est la recette de la tarte tatin ?", CancellationToken.None);

        Assert.Null(chat.LastMessages);
        Assert.True(result.Declined);
        Assert.Equal(AnswerPrompt.OutOfScope, result.Answer);
        Assert.Equal(["hors-perimetre"], result.Guardrails.Select(g => g.Code));
        Assert.Equal(0m, result.Usage.EstimatedCost);
        Assert.Empty(result.CitedPassages);
    }

    [Fact]
    public async Task At_the_relevance_threshold_the_model_is_called()
    {
        var chat = new FakeChatClient("Interdit [P1].");
        var service = new AnswerService(new FakeSearch(Article5), chat, Settings with { MinScore = 0.7 }); // score 0,7

        var result = await service.AskAsync("?", CancellationToken.None);

        Assert.NotNull(chat.LastMessages);
        Assert.False(result.Declined);
    }

    [Fact]
    public async Task Every_answer_carries_the_fixed_disclaimer()
    {
        var result = await Service(new FakeChatClient("Interdit [P1].")).AskAsync("?", CancellationToken.None);

        Assert.Equal(AnswerPrompt.Disclaimer, result.Disclaimer);
    }

    private static AnswerService Service(FakeChatClient chat) => new(new FakeSearch(Article5, Recital29), chat, Settings);

    private static RetrievedPassage Passage(string heading, string anchor, string content) =>
        new(heading, anchor, content, 0.7, "EUR-Lex", "AI Act", "https://eur-lex.europa.eu", DateTimeOffset.UnixEpoch, "Décision 2011/833/UE");

    private sealed class FakeSearch(params RetrievedPassage[] passages) : IPassageSearch
    {
        public string Strategy => "fausse";

        public Task<SearchOutcome> SearchAsync(string question, int limit, CancellationToken ct) =>
            Task.FromResult(new SearchOutcome(passages.Take(limit).ToList(), TimeSpan.Zero, TimeSpan.Zero));
    }

    private sealed class FakeChatClient(string answer, long inputTokens = 1_000, long outputTokens = 100) : IChatClient
    {
        public IReadOnlyList<ChatMessage>? LastMessages { get; private set; }
        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer))
            {
                Usage = new UsageDetails { InputTokenCount = inputTokens, OutputTokenCount = outputTokens },
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
