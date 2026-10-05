using System.Net;
using Limpide.Core.Corpus;
using Limpide.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace Limpide.Ingestion.Fetch;

/// <summary>
/// Télécharge chaque document de corpus.json dans data/raw/&lt;source&gt;/&lt;sha256&gt;.&lt;ext&gt;
/// et enregistre une nouvelle version uniquement si l'empreinte a changé. Relançable sans effet de bord.
/// </summary>
public sealed class FetchCommand(
    HttpClient http,
    DocumentVersionStore store,
    IngestionOptions options,
    ILogger<FetchCommand> logger)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var corpus = CorpusLoader.Load(options.CorpusPath);
        var failures = 0;
        var first = true;

        foreach (var source in corpus.Sources)
        foreach (var document in source.Documents)
        {
            if (!first)
                await Task.Delay(options.DelayBetweenRequests, ct);
            first = false;

            try
            {
                await FetchAsync(source, document, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or TaskCanceledException
                                       && !ct.IsCancellationRequested)
            {
                failures++;
                logger.LogError("{Source} / {Title} : échec — {Message}", source.Name, document.Title, ex.Message);
            }
        }

        logger.LogInformation("Collecte terminée : {Count} document(s), {Failures} échec(s)",
            corpus.Sources.Sum(s => s.Documents.Count), failures);

        // Code de sortie non nul : un orchestrateur (Airflow en phase 2) doit voir l'échec.
        return failures == 0 ? 0 : 1;
    }

    private async Task FetchAsync(SourceDefinition source, DocumentDefinition document, CancellationToken ct)
    {
        var content = await DownloadAsync(document, ct);
        var hash = ContentHash.Compute(content);

        var rawPath = $"{source.Slug}/{hash}{Extension(document.Format)}";
        await WriteRawFileAsync(rawPath, content, ct);

        var outcome = await store.SaveAsync(source, document, hash, rawPath, ct);

        logger.LogInformation("{Source} / {Title} : {Outcome} ({Hash}…, {Size:N0} octets)",
            source.Name, document.Title, Describe(outcome), hash[..12], content.Length);
    }

    private async Task<byte[]> DownloadAsync(DocumentDefinition document, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, document.EffectiveFetchUrl);
        if (document.Accept is not null)
            request.Headers.Accept.ParseAdd(document.Accept);
        if (document.AcceptLanguage is not null)
            request.Headers.AcceptLanguage.ParseAdd(document.AcceptLanguage);

        using var response = await http.SendAsync(request, ct);

        // 200 exigé : un pare-feu applicatif répond « 202 » avec un corps vide,
        // qu'il ne faut surtout pas enregistrer comme une version du document.
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} sur {document.EffectiveFetchUrl}", null, response.StatusCode);

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!IsExpectedMediaType(document.Format, mediaType))
            throw new InvalidDataException($"type de contenu inattendu « {mediaType} » pour un document {document.Format}");

        var content = await response.Content.ReadAsByteArrayAsync(ct);
        if (content.Length == 0)
            throw new InvalidDataException("contenu vide");

        return content;
    }

    /// <summary>Écriture atomique ; le nom étant l'empreinte, un fichier existant est forcément identique.</summary>
    private async Task WriteRawFileAsync(string rawPath, byte[] content, CancellationToken ct)
    {
        var fullPath = Path.Combine(options.RawDataPath, rawPath);
        if (File.Exists(fullPath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var tempPath = fullPath + ".tmp";
        await File.WriteAllBytesAsync(tempPath, content, ct);
        File.Move(tempPath, fullPath, overwrite: true);
    }

    private static bool IsExpectedMediaType(DocumentFormat format, string? mediaType) => format switch
    {
        DocumentFormat.Html => mediaType is "text/html" or "application/xhtml+xml",
        DocumentFormat.Pdf => mediaType is "application/pdf",
        _ => false,
    };

    private static string Extension(DocumentFormat format) => format switch
    {
        DocumentFormat.Html => ".html",
        DocumentFormat.Pdf => ".pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    private static string Describe(VersionOutcome outcome) => outcome switch
    {
        VersionOutcome.Unchanged => "inchangé",
        VersionOutcome.Created => "nouvelle version",
        VersionOutcome.Restored => "version antérieure redevenue courante",
        _ => outcome.ToString(),
    };

    public static HttpClient CreateHttpClient(IngestionOptions options)
    {
        var handler = new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All };
        var client = new HttpClient(handler) { Timeout = options.RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
        return client;
    }
}
