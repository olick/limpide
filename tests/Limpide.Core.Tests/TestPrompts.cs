using System.Runtime.CompilerServices;
using Limpide.Core.Answering;

namespace Limpide.Core.Tests;

/// <summary>Les vrais fichiers de consignes (src/prompts) : les tests vérifient ce que le modèle reçoit réellement.</summary>
public static class TestPrompts
{
    public const string AnswerVersion = "answer/3";

    public static readonly AnswerInstructions Answer = AnswerInstructions.FromTemplate(AnswerVersion, Read(AnswerVersion));

    public static string Read(string version) => File.ReadAllText(Path.Combine(PromptsDirectory(), $"{version}.txt"));

    /// <summary>src/prompts, repéré depuis ce fichier source (et non depuis l'appelant).</summary>
    public static string PromptsDirectory() => Locate();

    private static string Locate([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "prompts"));
}
