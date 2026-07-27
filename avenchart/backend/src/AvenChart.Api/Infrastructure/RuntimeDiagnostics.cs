using System.Threading;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Process-local aggregate request counters for the protected operations view.
/// This intentionally stores no request path, query, header, body, identity, or correlation value.
/// </summary>
public sealed class RuntimeDiagnostics
{
    private readonly DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
    private long completedResponses;
    private long informationalResponses;
    private long successfulResponses;
    private long redirectResponses;
    private long clientErrorResponses;
    private long serverErrorResponses;
    private long rateLimitedResponses;

    public void RecordCompletedResponse(int statusCode)
    {
        Interlocked.Increment(ref completedResponses);

        switch (statusCode / 100)
        {
            case 1:
                Interlocked.Increment(ref informationalResponses);
                break;
            case 2:
                Interlocked.Increment(ref successfulResponses);
                break;
            case 3:
                Interlocked.Increment(ref redirectResponses);
                break;
            case 4:
                Interlocked.Increment(ref clientErrorResponses);
                break;
            default:
                Interlocked.Increment(ref serverErrorResponses);
                break;
        }

        if (statusCode == StatusCodes.Status429TooManyRequests)
        {
            Interlocked.Increment(ref rateLimitedResponses);
        }
    }

    public RuntimeDiagnosticsSnapshot GetSnapshot()
    {
        return new RuntimeDiagnosticsSnapshot(
            Application: "avenchart-api",
            StartedAtUtc: startedAtUtc,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            CompletedResponses: Interlocked.Read(ref completedResponses),
            InformationalResponses: Interlocked.Read(ref informationalResponses),
            SuccessfulResponses: Interlocked.Read(ref successfulResponses),
            RedirectResponses: Interlocked.Read(ref redirectResponses),
            ClientErrorResponses: Interlocked.Read(ref clientErrorResponses),
            ServerErrorResponses: Interlocked.Read(ref serverErrorResponses),
            RateLimitedResponses: Interlocked.Read(ref rateLimitedResponses));
    }
}

public sealed record RuntimeDiagnosticsSnapshot(
    string Application,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long CompletedResponses,
    long InformationalResponses,
    long SuccessfulResponses,
    long RedirectResponses,
    long ClientErrorResponses,
    long ServerErrorResponses,
    long RateLimitedResponses);
