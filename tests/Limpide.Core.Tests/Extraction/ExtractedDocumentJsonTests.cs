using Limpide.Core.Extraction;

namespace Limpide.Core.Tests.Extraction;

public class ExtractedDocumentJsonTests
{
    [Fact]
    public void Round_trips_and_stays_readable()
    {
        var document = new ExtractedDocument("cnil-html/1", new string('a', 64),
        [
            new Block(BlockKind.Heading, "IA : Annoter les données", 1),
            new Block(BlockKind.ListItem, "a) la mise sur le marché « d’un système »", 1, "005.001"),
        ]);

        var json = ExtractedDocumentJson.Serialize(document);
        var read = ExtractedDocumentJson.Deserialize(json);

        Assert.Equal(document.Blocks, read.Blocks);
        Assert.Equal((document.Extractor, document.ContentHash), (read.Extractor, read.ContentHash));
        Assert.Contains("« d’un système »", json); // accents et guillemets non échappés
        Assert.Contains("\"kind\": \"listItem\"", json);
    }
}
