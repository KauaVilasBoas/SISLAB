using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace SISLAB.Api.Observability;

/// <summary>
/// Wires Serilog for the Host (card [E9] #56): structured JSON logs enriched with the request
/// <c>CorrelationId</c>, a Console sink always, and — when a Coralogix API key is configured — a durable
/// HTTP sink that ships the same JSON to Coralogix's ingestion endpoint.
///
/// <para>Configuration keys (from <see cref="IConfiguration"/> — User Secrets / environment, never the repo):</para>
/// <list type="bullet">
///   <item><c>Coralogix:ApiKey</c> — send key. When absent (local dev), only the Console sink is used.</item>
///   <item><c>Coralogix:IngestUrl</c> — logs ingestion endpoint. Defaults to the EU2 Serilog endpoint.</item>
/// </list>
///
/// <para>Secrets hygiene: SISLAB never logs passwords, JWT tokens or cookies. Serilog only emits the
/// structured properties the application explicitly logs (request name, duration, correlation id,
/// exception type/message) plus the framework request log — none of which carry credentials. The
/// <c>Authorization</c> header, cookies and request bodies are never enriched into the log context.</para>
/// </summary>
internal static class SerilogConfiguration
{
    private const string CoralogixApiKeyPath = "Coralogix:ApiKey";
    private const string CoralogixIngestUrlPath = "Coralogix:IngestUrl";
    private const string DefaultCoralogixIngestUrl = "https://ingress.eu2.coralogix.com/logs/v1/singles";

    /// <summary>
    /// Builds the Serilog logger configuration for the given host context. Reads any <c>Serilog</c> section
    /// from configuration first, then enriches with the correlation id and adds the sinks.
    /// </summary>
    public static void Configure(HostBuilderContext context, LoggerConfiguration configuration)
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "SISLAB.Api")
            .WriteTo.Console(new CompactJsonFormatter());

        string? coralogixApiKey = context.Configuration[CoralogixApiKeyPath];

        // In development (no Coralogix key configured) → Console JSON only. When the key is present, ship the
        // same structured events to Coralogix over HTTP. Serilog.Sinks.Http buffers to disk and retries, so a
        // transient Coralogix outage never blocks the request path or drops logs silently.
        if (!string.IsNullOrWhiteSpace(coralogixApiKey))
        {
            string ingestUrl = context.Configuration[CoralogixIngestUrlPath] ?? DefaultCoralogixIngestUrl;

            configuration.WriteTo.Http(
                requestUri: ingestUrl,
                queueLimitBytes: null);
        }
    }
}
