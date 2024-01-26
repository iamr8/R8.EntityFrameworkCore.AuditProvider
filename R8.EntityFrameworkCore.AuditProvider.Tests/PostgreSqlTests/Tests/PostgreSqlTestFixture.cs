using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using R8.XunitLogger;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Tests
{
    public class PostgreSqlTestFixture : IAsyncLifetime, IXunitLogProvider
    {
        private readonly ServiceProvider _serviceProvider;

        internal readonly PostgreSqlDbContext PostgreSqlDbContext;

        public event Action<string>? OnWriteLine;

        public PostgreSqlTestFixture()
        {
            _serviceProvider = new ServiceCollection()
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
                    optionsBuilder.AddEntityFrameworkAuditProviderInterceptor(serviceProvider);
                })
                .BuildServiceProvider();
            PostgreSqlDbContext = _serviceProvider.GetRequiredService<PostgreSqlDbContext>();
        }

        public async Task InitializeAsync()
        {
            var pm = await PostgreSqlDbContext.Database.GetPendingMigrationsAsync();
            var pendingMigrations = pm.ToArray();
            if (pendingMigrations.Any())
                await PostgreSqlDbContext.Database.EnsureDeletedAsync();
            var canConnect = await PostgreSqlDbContext.Database.CanConnectAsync();
            if (!canConnect || pendingMigrations.Any())
                await PostgreSqlDbContext.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            await PostgreSqlDbContext.Database.EnsureDeletedAsync();
            await _serviceProvider.DisposeAsync();
        }
    }
}