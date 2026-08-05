using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests
{
    public class MsSqlDbContextFactory : IDesignTimeDbContextFactory<MsSqlDbContext>
    {
        // Password comes from the TEST_DB_PASSWORD env var in CI (set from a GitHub secret); falls back to
        // a local default so `dotnet test` works out of the box against a local container.
        private static string Password => Environment.GetEnvironmentVariable("TEST_DB_PASSWORD") ?? "MyPassWoRD@#$";

        public static string ConnectionString =>
            $"Server=localhost,14331;Database=r8-audit-test;User ID=sa;Password={Password};Trusted_Connection=false;Integrated Security=false;MultipleActiveResultSets=true;Persist Security Info=False;Encrypt=False";

        public DbContextOptions<MsSqlDbContext> GetOptions()
        {
            var optionsBuilder = new DbContextOptionsBuilder<MsSqlDbContext>();
            optionsBuilder.UseSqlServer(ConnectionString);
            return optionsBuilder.Options;
        }
        
        public MsSqlDbContext CreateDbContext(string[] args)
        {
            var options = GetOptions();
            return new MsSqlDbContext(options);
        }
    }
}