using Microsoft.EntityFrameworkCore;
#if NET8_0_OR_GREATER
using Microsoft.EntityFrameworkCore.Diagnostics;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using R8.XunitLogger;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Tests
{
    public class PostgreSqlTestFixture : IAsyncLifetime, IXunitLogProvider
    {
        internal readonly ServiceProvider ServiceProvider;

        internal readonly PostgreSqlDbContext PostgreSqlDbContext;

        public event Action<string>? OnWriteLine;

        public PostgreSqlTestFixture()
        {
            ServiceProvider = new ServiceCollection()
                .AddLogging()
                .AddXunitLogger(s => OnWriteLine?.Invoke(s), o =>
                {
                    o.MinimumLevel = LogLevel.Debug;
                    o.Categories.Add("R8.EntityFrameworkCore.AuditProvider");
                })
                .AddEntityFrameworkAuditProvider(options =>
                {
                    options.UserProvider = sp =>
                    {
                        // var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
                        return new AuditProviderUser("1", new Dictionary<string, string>
                        {
                            { "Username", "Foo" }
                        });
                    };
                })
                .AddDbContext<PostgreSqlDbContext>((serviceProvider, optionsBuilder) =>
                {
                    optionsBuilder.UseNpgsql(PostgreSqlDbContextFactory.ConnectionString);
#if NET8_0_OR_GREATER
                    // EF Core 9+ throws PendingModelChangesWarning when the model differs from the last
                    // migration snapshot. The migrations were authored under EF 7; the diff is spurious
                    // across EF major versions, so ignore it in tests (the schema is created correctly).
                    optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
#endif
                    optionsBuilder.AddEntityFrameworkAuditProviderInterceptor(serviceProvider);
                })
                .BuildServiceProvider();
            PostgreSqlDbContext = ServiceProvider.GetRequiredService<PostgreSqlDbContext>();
        }

        public async
#if NET8_0_OR_GREATER
            ValueTask
#else
            Task
#endif
            InitializeAsync()
        {
            var pm = await PostgreSqlDbContext.Database.GetPendingMigrationsAsync();
            var pendingMigrations = pm.ToArray();
            if (pendingMigrations.Any())
                await PostgreSqlDbContext.Database.EnsureDeletedAsync();
            var canConnect = await PostgreSqlDbContext.Database.CanConnectAsync();
            if (!canConnect || pendingMigrations.Any())
                await PostgreSqlDbContext.Database.MigrateAsync();
        }

        public async
#if NET8_0_OR_GREATER
            ValueTask
#else
            Task
#endif
            DisposeAsync()
        {
            await PostgreSqlDbContext.Database.EnsureDeletedAsync();
            await ServiceProvider.DisposeAsync();
        }
    }
}