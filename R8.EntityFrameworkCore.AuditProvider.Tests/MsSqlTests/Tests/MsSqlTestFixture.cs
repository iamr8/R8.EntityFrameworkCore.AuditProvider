using Microsoft.EntityFrameworkCore;
#if NET8_0_OR_GREATER
using Microsoft.EntityFrameworkCore.Diagnostics;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using R8.XunitLogger;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.Tests
{
    public class MsSqlTestFixture : IAsyncLifetime, IXunitLogProvider
    {
        internal readonly ServiceProvider ServiceProvider;

        internal readonly MsSqlDbContext MsSqlDbContext;

        public event Action<string>? OnWriteLine;

        public MsSqlTestFixture()
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
                    options.MaxStoredAudits = 10;
                    options.UserProvider = sp =>
                    {
                        // var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
                        return new AuditProviderUser("1", new Dictionary<string, string>
                        {
                            { "Username", "Foo" }
                        });
                    };
                })
                .AddDbContext<MsSqlDbContext>((serviceProvider, optionsBuilder) =>
                {
                    optionsBuilder.UseSqlServer(MsSqlDbContextFactory.ConnectionString);
#if NET8_0_OR_GREATER
                    // EF Core 9+ throws PendingModelChangesWarning when the model differs from the last
                    // migration snapshot. The migrations were authored under EF 7; the diff is spurious
                    // across EF major versions, so ignore it in tests (the schema is created correctly).
                    optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
#endif
                    optionsBuilder.AddEntityFrameworkAuditProviderInterceptor(serviceProvider);
                })
                .BuildServiceProvider();
            MsSqlDbContext = ServiceProvider.GetRequiredService<MsSqlDbContext>();
        }

        public async
#if NET8_0_OR_GREATER
            ValueTask
#else
            Task
#endif
            InitializeAsync()
        {
            var pm = await MsSqlDbContext.Database.GetPendingMigrationsAsync();
            var pendingMigrations = pm.ToArray();
            if (pendingMigrations.Any())
                await MsSqlDbContext.Database.EnsureDeletedAsync();
            var canConnect = await MsSqlDbContext.Database.CanConnectAsync();
            if (!canConnect || pendingMigrations.Any())
                await MsSqlDbContext.Database.MigrateAsync();
        }

        public async
#if NET8_0_OR_GREATER
            ValueTask
#else
            Task
#endif
            DisposeAsync()
        {
            await MsSqlDbContext.Database.EnsureDeletedAsync();
            await ServiceProvider.DisposeAsync();
        }
    }
}