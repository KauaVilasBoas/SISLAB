using FluentValidation;
using SISLAB.Infrastructure.Messaging.Behaviors;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Tests;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Throws_and_skips_handler_when_validation_fails()
    {
        bool nextCalled = false;
        var behavior = new ValidationBehavior<PingCommand, string>(new[] { new PingCommandValidator() });

        Task<string> Next()
        {
            nextCalled = true;
            return Task.FromResult("ok");
        }

        await Assert.ThrowsAsync<ValidationException>(() => behavior.HandleAsync(new PingCommand(""), Next));

        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Calls_next_when_validation_passes()
    {
        var behavior = new ValidationBehavior<PingCommand, string>(new[] { new PingCommandValidator() });

        string result = await behavior.HandleAsync(new PingCommand("valido"), () => Task.FromResult("ok"));

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Passes_through_when_no_validators_registered()
    {
        var behavior = new ValidationBehavior<PingCommand, string>(Array.Empty<IValidator<PingCommand>>());

        string result = await behavior.HandleAsync(new PingCommand(""), () => Task.FromResult("ok"));

        Assert.Equal("ok", result);
    }
}

public sealed class TransactionBehaviorTests
{
    [Fact]
    public async Task Calls_SaveChanges_for_command()
    {
        var unitOfWork = new FakeUnitOfWork();
        var behavior = new TransactionBehavior<PingCommand, string>(unitOfWork);

        string result = await behavior.HandleAsync(new PingCommand("x"), () => Task.FromResult("done"));

        Assert.Equal("done", result);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Does_not_call_SaveChanges_for_query()
    {
        var unitOfWork = new FakeUnitOfWork();
        var behavior = new TransactionBehavior<PingQuery, string>(unitOfWork);

        _ = await behavior.HandleAsync(new PingQuery("x"), () => Task.FromResult("done"));

        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }
}
