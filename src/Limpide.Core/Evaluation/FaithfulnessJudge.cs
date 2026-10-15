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

/// <summary>Un passage tel que l'assistant l'a cité : identifiant dans la réponse (« P1 »), titre, texte.</summary>
public sealed record JudgedPassage(string Id, string Heading, string Text);

/// <summary>
/// Juge de fidélité (S7) : un second modèle, différent de celui qui rédige, lit la réponse et les passages cités, et
/// signale ce que le contrôle automatique ne voit pas (Small en S2, gpt-oss en S6 : citation valide, sens déformé).
/// Utile mais imparfait : il ne connaît que les passages (une omission du corpus lui échappe), et il se trompe ;
/// sa justesse se mesure sur des réponses déjà relues (commande judge-calibrate).
/// </summary>
public sealed class FaithfulnessJudge(IChatClient chat, GenerationSettings settings)
{
    /// <summary>Toute modification des consignes incrémente la version (comme pour la rédaction).</summary>
    public const string PromptVersion = "judge/2";

    public string Model => settings.Model;

    // judge/2 (2026-10-15), après calibration de judge/1 (accord 17/37) : test de gravité explicite (le lecteur se
    // tromperait-il ?), simplification = mineur, qualification = classer le cas concret de l'utilisateur.
    public const string System = """
        Tu relis les réponses d'un assistant qui aide à naviguer dans l'AI Act et les fiches pratiques IA de la CNIL.
        On te donne la question, la réponse de l'assistant et les passages qu'il cite ([P1], [P2]…).
        Tu juges seulement la fidélité de la réponse aux passages cités : ni le style, ni la longueur, ni ce qui aurait
        pu être ajouté.

        Pour chaque phrase qui s'écarte des passages, pose-toi une seule question : un lecteur qui se fie à cette
        phrase se tromperait-il sur ce que dit le texte (qui est visé, ce qui est obligatoire, interdit ou permis, à
        partir de quand, à quelles conditions) ?
        - Si oui, c'est un problème grave, d'un de ces types :
          - « deforme » : sens contraire ou différent du passage (exception appliquée hors de son champ, recommandation
            ou « attention particulière » devenue interdiction, réserve devenue obligation, notions confondues comme
            l'entrée en vigueur et l'application, article présenté comme portant sur un autre objet) ;
          - « non-soutenu » : règle ou fait juridique qu'aucun passage cité ne contient ;
          - « exhaustivite » : liste ou règle présentée comme complète (« les seules », « uniquement », « tout
            système ») alors que les passages ne permettent pas de l'affirmer ;
          - « qualification » : la réponse classe le cas concret de l'utilisateur (son logiciel, son entreprise, son
            projet) dans une catégorie juridique ou se prononce sur sa conformité (« Oui, votre logiciel est à haut
            risque », « Oui, un logiciel qui trie les CV comme le vôtre est classé à haut risque », « vous n'êtes pas en
            infraction »).
        - Si non, c'est « mineur » : simplification, nuance ou condition secondaire omise, détail ajouté sans
          conséquence, reformulation un peu plus large ou plus étroite que le texte, passage cité un peu à côté.

        Ne sont pas des problèmes : une reformulation fidèle ; rapporter une obligation générale telle que le texte la
        pose, même adressée au lecteur (« vous devez informer les utilisateurs qu'ils échangent avec une IA, sauf
        si… ») ; rapporter une classification du texte (« les systèmes utilisés pour le recrutement sont classés à haut
        risque ») sans dire que le système de l'utilisateur en relève ; un renvoi vers un professionnel ; dire que les
        passages ne suffisent pas.

        Vérifie chaque problème grave contre le texte exact du passage avant de le retenir : si le passage dit bien
        ce que dit la réponse, ce n'est pas un problème.

        Verdict :
        - « fidele » : rien à signaler ;
        - « reserve » : seulement des problèmes « mineur » ;
        - « infidele » : au moins un problème grave.

        Réponds uniquement par un objet JSON, problèmes d'abord, verdict ensuite :
        {"problemes":[{"extrait":"phrase de la réponse","type":"deforme","explication":"ce que dit vraiment le passage"}],"verdict":"infidele"}
        """;

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
                new ChatMessage(ChatRole.System, System),
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
        return new FaithfulnessJudgment(verdict, issues, settings.Model, PromptVersion, new TokenUsage(input, output, cost), clock.Elapsed);
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
