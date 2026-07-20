using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Messaging;
using SISLAB.Infrastructure.Messaging.Behaviors;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Agenda.Domain.Bioterium;
using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.Modules.Agenda.Domain.Presentations;
using SISLAB.Modules.Agenda.Domain.Rooms;
using SISLAB.Modules.Agenda.Infrastructure.Persistence;
using SISLAB.Modules.Agenda.Infrastructure.Repositories;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Infrastructure.DependencyInjection;

public static class AgendaModuleServiceExtensions
{
    public static IServiceCollection AddAgendaModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SislabDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SislabDb' is not configured.");

        services.AddDbContext<AgendaDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "agenda");
                npgsql.MigrationsAssembly(
                    typeof(AgendaModuleServiceExtensions).Assembly.GetName().Name);
            }));

        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBioteriumRepository, BioteriumRepository>();
        services.AddScoped<IPresentationRepository, PresentationRepository>();
        services.AddScoped<IAgendaEntryRepository, AgendaEntryRepository>();

        services.AddScoped<IOutboxDbContext>(sp => sp.GetRequiredService<AgendaDbContext>());
        services.AddScoped<OutboxWriter>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<AgendaDbContext>>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        services.AddHostedService<AgendaSchemaMigrationsHostedService>();

        return services;
    }
}
