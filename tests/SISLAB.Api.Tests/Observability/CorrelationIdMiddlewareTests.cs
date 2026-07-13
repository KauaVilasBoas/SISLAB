using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using SISLAB.Api.Middleware;
using SISLAB.Api.Observability;
using SISLAB.SharedKernel.Observability;

namespace SISLAB.Api.Tests.Observability;

/// <summary>
/// Tests for <see cref="CorrelationIdMiddleware"/> (card [E9] #56): the request-correlation gate that
/// resolves the <c>X-Correlation-Id</c> for every request, publishes it on the scoped accessor and echoes
/// it on the response.
/// </summary>
public sealed class CorrelationIdMiddlewareTests
{
    private const string HeaderName = "X-Correlation-Id";

    [Fact]
    public async Task WhenHeaderAbsent_GeneratesId_PublishesAndEchoesIt()
    {
        var accessor = new CorrelationIdAccessor();
        var (context, next) = BuildContext(inboundCorrelationId: null);

        await InvokeAsync(context, accessor, next);

        Assert.True(next.Called);
        Assert.False(string.IsNullOrWhiteSpace(accessor.CorrelationId));

        string echoed = ResponseHeader(context);
        Assert.Equal(accessor.CorrelationId, echoed);
    }

    [Fact]
    public async Task WhenHeaderPresent_PropagatesInboundId()
    {
        const string inbound = "abc123-inbound-correlation";
        var accessor = new CorrelationIdAccessor();
        var (context, next) = BuildContext(inboundCorrelationId: inbound);

        await InvokeAsync(context, accessor, next);

        Assert.Equal(inbound, accessor.CorrelationId);
        Assert.Equal(inbound, ResponseHeader(context));
    }

    [Fact]
    public async Task WhenHeaderBlank_GeneratesIdInsteadOfPropagatingEmpty()
    {
        var accessor = new CorrelationIdAccessor();
        var (context, next) = BuildContext(inboundCorrelationId: "   ");

        await InvokeAsync(context, accessor, next);

        Assert.False(string.IsNullOrWhiteSpace(accessor.CorrelationId));
        Assert.NotEqual("   ", accessor.CorrelationId);
        Assert.Equal(accessor.CorrelationId, ResponseHeader(context));
    }

    [Fact]
    public async Task AlwaysAddsResponseHeader_EvenWhenDownstreamThrows()
    {
        var accessor = new CorrelationIdAccessor();
        var (context, _) = BuildContext(inboundCorrelationId: "will-still-echo");
        var throwingNext = new NextSpy(shouldThrow: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => InvokeAsync(context, accessor, throwingNext));

        // The response header is armed via OnStarting before next() runs, so the id is echoed even when the
        // downstream pipeline faults (the exception handler upstream writes the body).
        Assert.Equal("will-still-echo", ResponseHeader(context));
    }

    private static async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor accessor, NextSpy next)
    {
        var responseFeature = (OnStartingResponseFeature)context.Features.Get<IHttpResponseFeature>()!;
        var middleware = new CorrelationIdMiddleware(next.Invoke);

        try
        {
            await middleware.InvokeAsync(context, accessor);
        }
        finally
        {
            // DefaultHttpContext never flushes a real response, so OnStarting callbacks are stored but not
            // fired. Trigger them (in finally, since the header is armed before next() so it must echo even
            // when the downstream pipeline faults) to observe the echoed header.
            await responseFeature.FireOnStartingAsync();
        }
    }

    private static (HttpContext Context, NextSpy Next) BuildContext(string? inboundCorrelationId)
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new OnStartingResponseFeature());

        if (inboundCorrelationId is not null)
            context.Request.Headers[HeaderName] = inboundCorrelationId;

        return (context, new NextSpy(shouldThrow: false));
    }

    private static string ResponseHeader(HttpContext context) => context.Response.Headers[HeaderName].ToString();

    private sealed class NextSpy
    {
        private readonly bool _shouldThrow;

        public NextSpy(bool shouldThrow) => _shouldThrow = shouldThrow;

        public bool Called { get; private set; }

        public Task Invoke(HttpContext _)
        {
            Called = true;
            if (_shouldThrow)
                throw new InvalidOperationException("Simulated downstream failure.");

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Minimal <see cref="IHttpResponseFeature"/> that actually invokes registered OnStarting callbacks when
    /// <see cref="HttpResponse.StartAsync"/> runs — <see cref="DefaultHttpContext"/>'s default feature stores
    /// them but never fires them, so the echoed header would otherwise be unobservable in a unit test.
    /// </summary>
    private sealed class OnStartingResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = new();

        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) => _onStarting.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state) { }

        public async Task FireOnStartingAsync()
        {
            HasStarted = true;
            foreach ((Func<object, Task> callback, object state) in _onStarting)
                await callback(state);
        }
    }
}
