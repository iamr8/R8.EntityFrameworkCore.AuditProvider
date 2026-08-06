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
            // Apply the schema once (the database persists for the whole test run) and clear all data
            // before each test. Never dropping the database means EF never probes a non-existent database,
            // which is what makes PostgreSQL log "FATAL: database ... does not exist" during the run.
            // TRUNCATE ... CASCADE resets every data table (keeping the migrations history) and gives each
            // test a clean slate while still committing rows, so the multi-scope/concurrency tests work.
            await PostgreSqlDbContext.Database.MigrateAsync();
            await PostgreSqlDbContext.Database.ExecuteSqlRawAsync(
                "DO $$ DECLARE r RECORD; BEGIN " +
                "FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory') LOOP " +
                "EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' RESTART IDENTITY CASCADE'; " +
                "END LOOP; END $$;");
        }

        public async
#if NET8_0_OR_GREATER
            ValueTask
#else
            Task
#endif
            DisposeAsync()
        {
            // Data is cleared in InitializeAsync (before each test); the database itself is left in place
            // so it is never re-probed while missing.
            await ServiceProvider.DisposeAsync();
        }
    }
}