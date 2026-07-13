using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SISLAB.Jobs.Scheduling;

namespace SISLAB.Jobs.Tests.Scheduling;

/// <summary>
/// Behavioural tests for the scheduling base <see cref="TimedBackgroundService"/> (E6 #39).
///
/// These pin the three guarantees the base makes to every concrete job: it runs on the configured
/// interval, it opens a fresh DI scope per tick, and a failing tick never tears the worker down.
/// The tests drive the base through the real <see cref="Microsoft.Extensions.Hosting.IHostedService"/>
/// start/stop lifecycle and poll for the observed effect, so they stay deterministic without relying
/// on fixed sleeps.
/// </summary>
public sealed class TimedBackgroundServiceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Runs_immediately_at_startup_and_then_on_the_interval()
    {
        CountingScopeFactory scopeFactory = new();
        TestTimedJob job = new(scopeFactory, TimeSpan.FromMilliseconds(40));

        await job.StartAsync(CancellationToken.None);

        // Startup tick (1) + several interval ticks. Poll until we have seen at least 3 ticks,
        // proving the timer keeps firing rather than running once.
        await WaitUntilAsync(() => job.TickCount >= 3);

        await job.StopAsync(CancellationToken.None);

        Assert.True(job.TickCount >= 3, $"Expected the job to tick repeatedly; saw {job.TickCount}.");
    }

    [Fact]
    public async Task Creates_a_fresh_DI_scope_for_every_tick()
    {
        CountingScopeFactory scopeFactory = new();
        TestTimedJob job = new(scopeFactory, TimeSpan.FromMilliseconds(40));

        await job.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => job.TickCount >= 3);
        await job.StopAsync(CancellationToken.None);

        // One scope per observed tick, and every scope was disposed (no leak across ticks).
        Assert.Equal(job.TickCount, scopeFactory.ScopesCreated);
        Assert.Equal(scopeFactory.ScopesCreated, scopeFactory.ScopesDisposed);
    }

    [Fact]
    public async Task A_failing_tick_is_swallowed_and_the_worker_survives()
    {
        CountingScopeFactory scopeFactory = new();
        // Throw only on the very first tick; subsequent ticks succeed.
        ThrowOnceTimedJob job = new(scopeFactory, TimeSpan.FromMilliseconds(40));

        await job.StartAsync(CancellationToken.None);

        // If the exception propagated, the loop would have stopped after the first tick and
        // SuccessfulTicks would never advance. Waiting for successful ticks proves it recovered.
        await WaitUntilAsync(() => job.SuccessfulTicks >= 2);

        await job.StopAsync(CancellationToken.None);

        Assert.True(job.ThrewOnce, "The first tick was expected to throw.");
        Assert.True(job.SuccessfulTicks >= 2,
            $"Worker should keep running after a failed tick; successful ticks: {job.SuccessfulTicks}.");
    }

    [Fact]
    public async Task Stops_cleanly_on_cancellation_without_further_ticks()
    {
        CountingScopeFactory scopeFactory = new();
        TestTimedJob job = new(scopeFactory, TimeSpan.FromMilliseconds(40));

        await job.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => job.TickCount >= 2);

        await job.StopAsync(CancellationToken.None);
        int ticksAtStop = job.TickCount;

        // Give the loop time to (not) fire again; a clean stop means the count is frozen.
        await Task.Delay(150);

        Assert.Equal(ticksAtStop, job.TickCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource cts = new(TestTimeout);
        while (!condition())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }
    }

    // ------------------------------- test doubles -------------------------------

    /// <summary>A minimal concrete job that just counts ticks.</summary>
    private sealed class TestTimedJob : TimedBackgroundService
    {
        private int _tickCount;

        public TestTimedJob(IServiceScopeFactory scopeFactory, TimeSpan interval)
            : base(scopeFactory, NullLogger.Instance)
            => Interval = interval;

        public int TickCount => Volatile.Read(ref _tickCount);

        protected override TimeSpan Interval { get; }

        protected override Task ExecuteTickAsync(IServiceScope scope, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _tickCount);
            return Task.CompletedTask;
        }
    }

    /// <summary>A job that throws on its first tick and then succeeds, to prove resilience.</summary>
    private sealed class ThrowOnceTimedJob : TimedBackgroundService
    {
        private int _totalTicks;
        private int _successfulTicks;

        public ThrowOnceTimedJob(IServiceScopeFactory scopeFactory, TimeSpan interval)
            : base(scopeFactory, NullLogger.Instance)
            => Interval = interval;

        public bool ThrewOnce { get; private set; }
        public int SuccessfulTicks => Volatile.Read(ref _successfulTicks);

        protected override TimeSpan Interval { get; }

        protected override Task ExecuteTickAsync(IServiceScope scope, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _totalTicks) == 1)
            {
                ThrewOnce = true;
                throw new InvalidOperationException("boom on the first tick");
            }

            Interlocked.Increment(ref _successfulTicks);
            return Task.CompletedTask;
        }
    }

    /// <summary>An <see cref="IServiceScopeFactory"/> that counts scope creation and disposal.</summary>
    private sealed class CountingScopeFactory : IServiceScopeFactory
    {
        private int _created;
        private int _disposed;

        public int ScopesCreated => Volatile.Read(ref _created);
        public int ScopesDisposed => Volatile.Read(ref _disposed);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _created);
            return new CountingScope(this);
        }

        private void OnScopeDisposed() => Interlocked.Increment(ref _disposed);

        private sealed class CountingScope : IServiceScope
        {
            private readonly CountingScopeFactory _owner;
            private bool _disposed;

            public CountingScope(CountingScopeFactory owner) => _owner = owner;

            public IServiceProvider ServiceProvider { get; } = new EmptyServiceProvider();

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.OnScopeDisposed();
            }
        }

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}
