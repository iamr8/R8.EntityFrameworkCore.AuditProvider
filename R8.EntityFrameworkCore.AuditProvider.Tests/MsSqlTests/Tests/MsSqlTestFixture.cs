using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.XunitLogger;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.Tests
{
    public class MsSqlTestFixture : IAsyncLifetime, IXunitLogProvider
    {
        private readonly ServiceProvider _serviceProvider;

        internal readonly MsSqlDbContext MsSqlDbContext;

        public event Action<string>? OnWriteLine;

        public MsSqlTestFixture()
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
                    optionsBuilder.AddEntityFrameworkAuditProviderInterceptor(serviceProvider);
                })
                .BuildServiceProvider();
            MsSqlDbContext = _serviceProvider.GetRequiredService<MsSqlDbContext>();
        }

        public async Task InitializeAsync()
        {
            var pm = await MsSqlDbContext.Database.GetPendingMigrationsAsync();
            var pendingMigrations = pm.ToArray();
            if (pendingMigrations.Any())
                await MsSqlDbContext.Database.EnsureDeletedAsync();
            var canConnect = await MsSqlDbContext.Database.CanConnectAsync();
            if (!canConnect || pendingMigrations.Any())
                await MsSqlDbContext.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            await MsSqlDbContext.Database.EnsureDeletedAsync();
            await _serviceProvider.DisposeAsync();
        }
    }
}