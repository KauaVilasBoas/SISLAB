using MediatR;

namespace SISLAB.Modules.Identity.Tests.Authorization;

/// <summary>
/// A minimal in-memory stand-in for MediatR's <see cref="IMediator"/>, used to drive
/// <c>LumenAuthorizationGateway</c> without the real Lumen pipeline or a database. Requests are matched by
/// their runtime type to a registered responder; unhandled requests fail loudly so a test cannot silently
/// pass against an unexercised code path. Also records every dispatched request for assertion.
/// </summary>
internal sealed class FakeLumenMediator : IMediator
{
    private readonly Dictionary<Type, Func<object, object?>> _responders = new();

    public List<object> SentRequests { get; } = [];

    /// <summary>Registers the response returned when a request of type <typeparamref name="TRequest"/> is sent.</summary>
    public FakeLumenMediator On<TRequest>(Func<TRequest, object?> responder)
    {
        _responders[typeof(TRequest)] = request => responder((TRequest)request);
        return this;
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        SentRequests.Add(request);

        if (!_responders.TryGetValue(request.GetType(), out Func<object, object?>? responder))
            throw new InvalidOperationException($"No fake responder registered for {request.GetType().Name}.");

        return Task.FromResult((TResponse)responder(request)!);
    }

    public Task Send(IRequest request, CancellationToken cancellationToken = default)
    {
        SentRequests.Add(request);

        // Void commands (assign/remove/update/set-permissions) need no response; record and complete.
        if (_responders.TryGetValue(request.GetType(), out Func<object, object?>? responder))
            responder(request);

        return Task.CompletedTask;
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
        => Send((IRequest)request!, cancellationToken);

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
        => Task.CompletedTask;
}
