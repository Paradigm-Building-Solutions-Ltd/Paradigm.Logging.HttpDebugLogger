using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Paradigm.Logging.HttpDebugLogger;

/// <summary>
/// Configuration file for <see cref="HttpDebugLogger"/>
/// </summary>
public class HttpDebugLoggerConfiguration
{
    public bool LogRequestHeaders { get; set; } = true;
    public bool LogRequestContentHeaders { get; set; } = true;
    public bool LogResponseHeaders { get; set; } = true;
    public bool LogResponseContentHeaders { get; set; } = true;
    public bool LogRequestContent { get; set; } = true;
    public bool LogResponseContent { get; set; } = true;
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
}

/// <summary>
/// Logs HTTP request details to the ILogger instance provided.
/// </summary>
public class HttpDebugLogger(ILogger<HttpDebugLogger> logger, IOptions<HttpDebugLoggerConfiguration> options) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;

        var scopeArgs = new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>("RequestId", Guid.NewGuid()),
            new KeyValuePair<string, object>("Host", $"{request.RequestUri?.Scheme}://{request.RequestUri?.Host}"),
            new KeyValuePair<string, object>("Request", $"{request.Method} {request.RequestUri?.PathAndQuery} {request.RequestUri?.Scheme}/{request.Version}")
        };

        using (var scope = logger.BeginScope(scopeArgs))
        {
            if (opts.LogRequestHeaders)
            {
                var builder = new StringBuilder();

                foreach (var header in request.Headers)
                {
                    builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }

                logger?.Log(opts.LogLevel, "Headers:\n{headers}", builder.ToString());
            }

            if (request.Content != null)
            {
                if (opts.LogRequestContentHeaders)
                {
                    var builder = new StringBuilder();

                    foreach (var header in request.Content.Headers)
                    {
                        builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                    }

                    logger?.Log(opts.LogLevel, "Content headers:\n{result}", builder.ToString());
                }

                if (opts.LogRequestContent)
                {
                    if (request.Content is StringContent || IsTextBasedContentType(request.Headers) || IsTextBasedContentType(request.Content.Headers))
                    {
                        var result = await request.Content.ReadAsStringAsync();

                        logger?.Log(opts.LogLevel, "Content:\n{result}", result);
                    }
                }
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            logger?.Log(opts.LogLevel, "Request took {ms} ms", stopwatch.ElapsedMilliseconds);

            logger?.Log(opts.LogLevel, "Response: {scheme}/{version} {code} {reason}", request.RequestUri?.Scheme.ToUpper(), response.Version, response.StatusCode, response.ReasonPhrase);

            if (opts.LogResponseHeaders)
            {
                var builder = new StringBuilder();
                foreach (var header in response.Headers)
                {
                    builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }

                logger?.Log(opts.LogLevel, "Response headers:\n{result}", builder.ToString());
            }

            if (opts.LogResponseContent)
            {
                if (response.Content != null)
                {
                    if (opts.LogResponseContentHeaders)
                    {
                        var builder = new StringBuilder();

                        foreach (var header in response.Content.Headers)
                        {
                            builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                        }

                        logger?.Log(opts.LogLevel, "Response content headers:\n{response}", builder.ToString());
                    }

                    if (response.Content is StringContent || IsTextBasedContentType(response.Headers) || IsTextBasedContentType(response.Content.Headers))
                    {
                        var readContentStopwatch = new Stopwatch();
                        readContentStopwatch.Start();
                        var result = await response.Content.ReadAsStringAsync(cancellationToken);
                        readContentStopwatch.Stop();

                        logger?.Log(opts.LogLevel, "Content:\n{content}", result);
                        logger?.Log(opts.LogLevel, "Content took {ms} ms to read.", readContentStopwatch.ElapsedMilliseconds);
                    }
                }
            }

            logger?.Log(opts.LogLevel, "Request ended.");

            return response;
        }
    }

    private static readonly string[] _textContentTypes = ["html", "text", "xml", "json", "txt", "x-www-form-urlencoded"];

    private static bool IsTextBasedContentType(HttpHeaders headers)
    {
        if (!headers.TryGetValues("Content-Type", out var values))
        {
            return false;
        }

        var header = string.Join(" ", values).ToLowerInvariant();

        return _textContentTypes.Any(t => header.Contains(t));
    }
}
