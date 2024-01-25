using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests
{
    public class MsSqlDbContextFactory : IDesignTimeDbContextFactory<MsSqlDbContext>
    {
        public static string ConnectionString => 
            "Server=localhost,14331;Database=r8-audit-test;User ID=sa;Password=MyPassWoRD@#$;Trusted_Connection=false;Integrated Security=false;MultipleActiveResultSets=true;Persist Security Info=False;Encrypt=False";

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