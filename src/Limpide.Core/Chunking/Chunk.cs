namespace Limpide.Core.Chunking;

/// <summary>Un passage indexé : un extrait continu de la source, avec son titre de rattachement.</summary>
/// <param name="Heading">Titre de rattachement affiché avec le passage (ex. « Article 5 — Pratiques interdites… »).</param>
/// <param name="Content">Texte de la source, sans modification : blocs séparés par un retour à la ligne.</param>
/// <param name="Anchor">Subdivision d'origine quand la source en a (ex. « art_5 », « rct_12 », « anx_III »).</param>
public sealed record Chunk(string Heading, string Content, string? Anchor);
