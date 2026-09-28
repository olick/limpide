using Limpide.Core.Corpus;

namespace Limpide.Core.Tests.Corpus;

public class CorpusLoaderTests
{
    private const string ValidCorpus = """
        {
          // Les commentaires sont autorisés.
          "sources": [
            {
              "name": "EUR-Lex",
              "baseUrl": "https://eur-lex.europa.eu",
              "reuseTerms": "Décision 2011/833/UE",
              "documents": [
                {
                  "title": "AI Act",
                  "url": "https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=OJ:L_202401689",
                  "format": "html",
                  "fetchUrl": "http://publications.europa.eu/resource/celex/32024R1689",
                  "accept": "application/xhtml+xml"
                }
              ]
            },
            {
              "name": "CNIL",
              "baseUrl": "https://www.cnil.fr",
              "reuseTerms": "CC-BY-ND 4.0 FR",
              "documents": [
                { "title": "Fiche", "url": "https://www.cnil.fr/fr/fiche.pdf", "format": "pdf" }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Parse_reads_sources_and_documents()
    {
        var corpus = CorpusLoader.Parse(ValidCorpus);

        Assert.Equal(["eur-lex", "cnil"], corpus.Sources.Select(s => s.Slug));
        var aiAct = corpus.Sources[0].Documents.Single();
        Assert.Equal(DocumentFormat.Html, aiAct.Format);
        Assert.Equal("http://publications.europa.eu/resource/celex/32024R1689", aiAct.EffectiveFetchUrl);
        Assert.Equal(DocumentFormat.Pdf, corpus.Sources[1].Documents.Single().Format);
    }

    [Fact]
    public void EffectiveFetchUrl_defaults_to_url()
    {
        var document = CorpusLoader.Parse(ValidCorpus).Sources[1].Documents.Single();

        Assert.Equal(document.Url, document.EffectiveFetchUrl);
    }

    [Fact]
    public void Parse_rejects_unknown_format()
    {
        var json = ValidCorpus.Replace("\"format\": \"pdf\"", "\"format\": \"docx\"");

        Assert.Throws<InvalidDataException>(() => CorpusLoader.Parse(json));
    }

    [Fact]
    public void Parse_rejects_missing_reuse_terms()
    {
        var json = ValidCorpus.Replace("\"reuseTerms\": \"CC-BY-ND 4.0 FR\"", "\"reuseTerms\": \" \"");

        var ex = Assert.Throws<InvalidDataException>(() => CorpusLoader.Parse(json));
        Assert.Contains("conditions de réutilisation", ex.Message);
    }

    [Fact]
    public void Parse_rejects_missing_required_field()
    {
        var json = ValidCorpus.Replace("\"reuseTerms\": \"CC-BY-ND 4.0 FR\",", "");

        Assert.Throws<InvalidDataException>(() => CorpusLoader.Parse(json));
    }

    [Fact]
    public void Parse_rejects_duplicate_document_url()
    {
        var json = ValidCorpus.Replace("https://www.cnil.fr/fr/fiche.pdf",
            "https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=OJ:L_202401689");

        var ex = Assert.Throws<InvalidDataException>(() => CorpusLoader.Parse(json));
        Assert.Contains("déclaré deux fois", ex.Message);
    }

    [Fact]
    public void Parse_rejects_relative_url()
    {
        var json = ValidCorpus.Replace("https://www.cnil.fr/fr/fiche.pdf", "/fr/fiche.pdf");

        Assert.Throws<InvalidDataException>(() => CorpusLoader.Parse(json));
    }

    [Fact]
    public void Repository_corpus_file_is_valid()
    {
        var path = Path.Combine(RepositoryRoot(), "corpus.json");

        var corpus = CorpusLoader.Load(path);

        Assert.NotEmpty(corpus.Sources.SelectMany(s => s.Documents));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Limpide.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Racine du dépôt introuvable.");
    }
}
