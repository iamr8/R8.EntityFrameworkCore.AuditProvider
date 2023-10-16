using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R8.EntityFrameworkAuditProvider.Tests.Entities;

namespace R8.EntityFrameworkAuditProvider.Tests
{
    public class TestFixture : IAsyncLifetime
    {
        private readonly ServiceProvider _serviceProvider;

        internal readonly DummyDbContext DummyDbContext;

        public TestFixture()
        {
            _serviceProvider = new ServiceCollection()
                .AddLogging()
                .AddEntityFrameworkAuditProvider(options =>
                {
                    options.ExcludedColumns.Add(nameof(IAggregateEntity.Id));
                    options.UserProvider = sp =>
                    {
                        // var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
                        return new EntityFrameworkAuditUser("1", new Dictionary<string, string>
                        {
                            { "Username", "Foo" }
                        });
                    };
                    options.IncludeStackTrace = true;
                })
                .AddDbContext<DummyDbContext>((serviceProvider, optionsBuilder) =>
                {
                    optionsBuilder.UseNpgsql(DummyDbContextFactory.ConnectionString);
                    optionsBuilder.AddEntityFrameworkAuditProviderInterceptor(serviceProvider);
                })
                .BuildServiceProvider();
            DummyDbContext = _serviceProvider.GetRequiredService<DummyDbContext>();
        }

        public async Task InitializeAsync()
        {
            var pm = await DummyDbContext.Database.GetPendingMigrationsAsync();
            var pendingMigrations = pm.ToArray();
            if (pendingMigrations.Any())
                await DummyDbContext.Database.EnsureDeletedAsync();
            var canConnect = await DummyDbContext.Database.CanConnectAsync();
            if (!canConnect || pendingMigrations.Any())
                await DummyDbContext.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            await DummyDbContext.Database.EnsureDeletedAsync();
            await _serviceProvider.DisposeAsync();
        }
    }
}