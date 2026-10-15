using System.Security.Cryptography;

namespace Limpide.Core.Tests.Answering;

/// <summary>
/// Une version de consignes ne change jamais de texte : sinon deux rapports d'évaluation marqués « answer/3 »
/// mesureraient deux consignes différentes. Modifier un texte = créer le fichier suivant (answer/4.txt) et l'ajouter ici.
/// </summary>
public class PromptFilesTests
{
    private static readonly Dictionary<string, string> Frozen = new()
    {
        ["answer/3"] = "aba69c5e5308d425b6d2928b2cd4268aa57f38d5ede278ac503aa4fc5da7f25f",
        ["judge/2"] = "aad4887216d54ce732a8a0da8cfa694af09f4ea68d5c00036faa2197d35abbce",
    };

    [Fact]
    public void Published_prompt_files_are_never_modified()
    {
        foreach (var (version, hash) in Frozen)
        {
            var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(TestPrompts.PromptsDirectory(), $"{version}.txt"))));
            Assert.True(actual == hash, $"{version}.txt a changé : créer une nouvelle version au lieu de modifier celle-ci.");
        }
    }

    [Fact]
    public void Every_prompt_file_is_frozen()
    {
        var files = Directory.GetFiles(TestPrompts.PromptsDirectory(), "*.txt", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(TestPrompts.PromptsDirectory(), f)[..^4].Replace('\\', '/'));
        Assert.Equal(Frozen.Keys.Order(), files.Order());
    }
}
