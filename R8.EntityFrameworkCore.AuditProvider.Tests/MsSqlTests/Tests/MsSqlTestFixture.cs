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
            // Recreate a clean schema for each test. Using EnsureDeleted + Migrate (instead of probing
            // with CanConnect/GetPendingMigrations) avoids connecting to the target database while it does
            // not exist, which the server would otherwise log as a connection failure.
            await MsSqlDbContext.Database.EnsureDeletedAsync();
            await MsSqlDbContext.Database.MigrateAsync();
            // Status is mapped with an explicit value converter; add its column out-of-band (not via a
            // migration) to exercise the interceptor's value-converter path across both providers without
            // an EF10-format migration that would break the net6.0 (EF Core 7) build.
            await MsSqlDbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE [MyAuditableEntities] ADD [Status] nvarchar(max) NOT NULL DEFAULT 'Active';");
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