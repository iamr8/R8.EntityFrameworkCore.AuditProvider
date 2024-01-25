using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests
{
    public class PostgreSqlDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlDbContext>
    {
        public static string ConnectionString
        {
            get
            {
                var csb = new NpgsqlConnectionStringBuilder
                {
                    Host = "localhost",
                    Port = 54322,
                    Database = "r8-audit-test",
                    Username = "postgres",
                    Password = "MyPassWoRD@#$"
                };
                return csb.ConnectionString;
            }
        }

        public DbContextOptions<PostgreSqlDbContext> GetOptions()
        {
            var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlDbContext>();
            optionsBuilder.UseNpgsql(ConnectionString);
            return optionsBuilder.Options;
        }
        
        public PostgreSqlDbContext CreateDbContext(string[] args)
        {
            var options = GetOptions();
            return new PostgreSqlDbContext(options);
        }
    }
}