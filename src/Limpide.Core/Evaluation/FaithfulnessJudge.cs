using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Limpide.Core.Answering;
using Microsoft.Extensions.AI;

namespace Limpide.Core.Evaluation;

public enum FaithfulnessVerdict
{
    /// <summary>Rien à signaler.</summary>
    Faithful,

    /// <summary>Écarts mineurs, sans conséquence sur le sens (omission d'un détail, glose).</summary>
    Reservation,

    /// <summary>Déformation, affirmation non soutenue, fausse exhaustivité ou qualification de la situation.</summary>
    Unfaithful,

    /// <summary>Le juge n'a pas rendu de JSON exploitable : ni succès ni échec, à relire.</summary>
    Unreadable,
}

/// <param name="Kind">deforme, non-soutenu, exhaustivite, qualification ou mineur.</param>
public sealed record JudgeIssue(string Excerpt, string Kind, string Explanation);

public sealed record FaithfulnessJudgment(
    FaithfulnessVerdict Verdict,
    IReadOnlyList<JudgeIssue> Issues,
    string Model,
    string PromptVersion,
    TokenUsage Usage,
    TimeSpan Duration);

/// <summary>Consignes du juge, lues par l'infrastructure dans src/prompts/judge/N.txt.</summary>
public sealed record JudgeInstructions(string Version, string System);

/// <summary>Un passage tel que l'assistant l'a cité : identifiant dans la réponse (« P1 »), titre, texte.</summary>
public sealed record JudgedPassage(string Id, string Heading, string Text);

/// <summary>
/// Juge de fidélité (S7) : un second modèle, différent de celui qui rédige, lit la réponse et les passages cités, et
/// signale ce que le contrôle automatique ne voit pas (Small en S2, gpt-oss en S6 : citation valide, sens déformé).
/// Utile mais imparfait : il ne connaît que les passages (une omission du corpus lui échappe), et il se trompe ;
/// sa justesse se mesure sur des réponses déjà relues (commande judge-calibrate).
/// </summary>
public sealed class FaithfulnessJudge(IChatClient chat, GenerationSettings settings, JudgeInstructions instructions)
{
    /// <summary>« judge/2 » : fichier src/prompts/judge/2.txt ; enregistrée dans chaque rapport d'évaluation.</summary>
    public string PromptVersion => instructions.Version;

    public string Model => settings.Model;

    /// <summary>Format du message : le modifier change ce que voit le juge, donc impose une nouvelle version des consignes.</summary>
    public static string UserMessage(string question, string answer, IEnumerable<JudgedPassage> passages)
    {
        var text = new StringBuilder($"Question : {question}\n\nPassages cités :\n");
        foreach (var p in passages)
            text.Append($"\n[{p.Id}] {p.Heading}\n{p.Text}\n");
        return text.Append($"\nRéponse de l'assistant :\n{answer}").ToString();
    }

    public async Task<FaithfulnessJudgment> JudgeAsync(
        string question, string answer, IReadOnlyList<JudgedPassage> passages, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var response = await chat.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, instructions.System),
                new ChatMessage(ChatRole.User, UserMessage(question, answer, passages)),
            ],
            new ChatOptions
            {
                ModelId = settings.Model,
                Temperature = settings.Temperature,
                // Explicite : Mistral Large 4 refuse une température 0 sans top_p = 1 (valeur par défaut des autres).
                TopP = 1,
                MaxOutputTokens = settings.MaxOutputTokens,
                ResponseFormat = ChatResponseFormat.Json,
            },
            ct);

        var (verdict, issues) = Parse(response.Text);
        long? input = response.Usage?.InputTokenCount, output = response.Usage?.OutputTokenCount;
        decimal? cost = settings.Price is { } price && input is not null && output is not null
            ? (input.Value * price.InputPerMillion + output.Value * price.OutputPerMillion) / 1_000_000m
            : null;
        return new FaithfulnessJudgment(verdict, issues, settings.Model, instructions.Version, new TokenUsage(input, output, cost), clock.Elapsed);
    }

    /// <summary>
    /// Lit le JSON du juge. Un verdict absent ou inconnu donne <see cref="FaithfulnessVerdict.Unreadable"/>, jamais
    /// « fidèle » par défaut ; un verdict contredit par les problèmes listés est corrigé vers le plus sévère.
    /// </summary>
    public static (FaithfulnessVerdict Verdict, IReadOnlyList<JudgeIssue> Issues) Parse(string text)
    {
        try
        {
            using var json = JsonDocument.Parse(ModelText.Clean(text).Trim().Trim('`').TrimStart("json".ToCharArray()));
            var issues = json.RootElement.TryGetProperty("problemes", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Select(i => new JudgeIssue(
                    Read(i, "extrait"), Read(i, "type"), Read(i, "explication"))).ToList()
                : [];
            var verdict = Read(json.RootElement, "verdict") switch
            {
                "fidele" or "fidèle" => FaithfulnessVerdict.Faithful,
                "reserve" or "réserve" => FaithfulnessVerdict.Reservation,
                "infidele" or "infidèle" => FaithfulnessVerdict.Unfaithful,
                _ => FaithfulnessVerdict.Unreadable,
            };
            if (verdict is FaithfulnessVerdict.Faithful or FaithfulnessVerdict.Reservation && issues.Any(i => i.Kind != "mineur"))
                verdict = FaithfulnessVerdict.Unfaithful;
            return (verdict, issues);
        }
        catch (JsonException)
        {
            return (FaithfulnessVerdict.Unreadable, []);
        }
    }

    private static string Read(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!.Trim()
            : "";
}
