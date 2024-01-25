using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Tests
{
    public class PostgreSqlTestFixture : IAsyncLifetime
    {
        private readonly ServiceProvider _serviceProvider;

        internal readonly PostgreSqlDbContext PostgreSqlDbContext;

        public PostgreSqlTestFixture()
        {
            _serviceProvider = new ServiceCollection()
                .AddLogging()
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