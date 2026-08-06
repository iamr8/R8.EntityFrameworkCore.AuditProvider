using System.Text.Json;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests
{
    // Stress the ArrayPool-backed change buffer across its growth boundaries (initial capacity 16, then
    // 32, 64, ...). Proves the buffer grows without a cap, never throws, and never drops or duplicates a
    // change — regardless of how many audited properties change in a single save.
    public class ChangeBuffer_Tests
    {
        // 64 audited string properties, enough to force several buffer growths.
        private sealed class WideAuditEntity : IAuditActivator, IAuditJsonStorage
        {
            public int Id { get; set; }
            public JsonElement? Audits { get; set; }
        public string P0 { get; set; } = "";
        public string P1 { get; set; } = "";
        public string P2 { get; set; } = "";
        public string P3 { get; set; } = "";
        public string P4 { get; set; } = "";
        public string P5 { get; set; } = "";
        public string P6 { get; set; } = "";
        public string P7 { get; set; } = "";
        public string P8 { get; set; } = "";
        public string P9 { get; set; } = "";
        public string P10 { get; set; } = "";
        public string P11 { get; set; } = "";
        public string P12 { get; set; } = "";
        public string P13 { get; set; } = "";
        public string P14 { get; set; } = "";
        public string P15 { get; set; } = "";
        public string P16 { get; set; } = "";
        public string P17 { get; set; } = "";
        public string P18 { get; set; } = "";
        public string P19 { get; set; } = "";
        public string P20 { get; set; } = "";
        public string P21 { get; set; } = "";
        public string P22 { get; set; } = "";
        public string P23 { get; set; } = "";
        public string P24 { get; set; } = "";
        public string P25 { get; set; } = "";
        public string P26 { get; set; } = "";
        public string P27 { get; set; } = "";
        public string P28 { get; set; } = "";
        public string P29 { get; set; } = "";
        public string P30 { get; set; } = "";
        public string P31 { get; set; } = "";
        public string P32 { get; set; } = "";
        public string P33 { get; set; } = "";
        public string P34 { get; set; } = "";
        public string P35 { get; set; } = "";
        public string P36 { get; set; } = "";
        public string P37 { get; set; } = "";
        public string P38 { get; set; } = "";
        public string P39 { get; set; } = "";
        public string P40 { get; set; } = "";
        public string P41 { get; set; } = "";
        public string P42 { get; set; } = "";
        public string P43 { get; set; } = "";
        public string P44 { get; set; } = "";
        public string P45 { get; set; } = "";
        public string P46 { get; set; } = "";
        public string P47 { get; set; } = "";
        public string P48 { get; set; } = "";
        public string P49 { get; set; } = "";
        public string P50 { get; set; } = "";
        public string P51 { get; set; } = "";
        public string P52 { get; set; } = "";
        public string P53 { get; set; } = "";
        public string P54 { get; set; } = "";
        public string P55 { get; set; } = "";
        public string P56 { get; set; } = "";
        public string P57 { get; set; } = "";
        public string P58 { get; set; } = "";
        public string P59 { get; set; } = "";
        public string P60 { get; set; } = "";
        public string P61 { get; set; } = "";
        public string P62 { get; set; } = "";
        public string P63 { get; set; } = "";
        }

        private sealed class WideDbContext : DbContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseSqlServer("Server=localhost;Database=wide;TrustServerCertificate=true");

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                var e = modelBuilder.Entity<WideAuditEntity>();
                e.HasKey(x => x.Id);
                e.Ignore(x => x.Audits);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(16)]  // == initial capacity
        [InlineData(17)]  // first growth (16 -> 32)
        [InlineData(31)]
        [InlineData(32)]  // == second capacity
        [InlineData(33)]  // second growth (32 -> 64)
        [InlineData(63)]
        [InlineData(64)]  // fills third capacity exactly
        public void Records_every_change_across_buffer_growth_boundaries(int changedCount)
        {
            AuditProviderConfiguration.JsonOptions ??= new AuditProviderOptions().JsonOptions;

            using var db = new WideDbContext();
            var interceptor = new EntityFrameworkAuditProviderInterceptor(
                new AuditProviderOptions(), Substitute.For<IServiceProvider>(),
                NullLogger<EntityFrameworkAuditProviderInterceptor>.Instance);

            var entity = new WideAuditEntity { Id = 1 };
            db.Attach(entity); // tracked & Unchanged: original snapshot captured as ""
            var entry = db.Entry(entity);

            var marked = 0;
            foreach (var p in entry.Properties)
            {
                if (marked >= changedCount || !p.Metadata.Name.StartsWith("P"))
                    continue;
                p.CurrentValue = "new" + marked; // differs from the "" original → EF marks it modified
                marked++;
            }
            marked.Should().Be(changedCount, "test setup should mark the requested number of properties");

            var mock = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, entry.Members);

            var act = () => interceptor.AckAudits(mock, db);
            act.Should().NotThrow();

            var audits = entity.GetAuditCollection();
            if (changedCount == 0)
            {
                audits.Should().BeNull("no changes means no audit is stored");
                return;
            }

            audits.Should().NotBeNull();
            var last = audits!.MaxBy(x => x.DateTime);
            last.Flag.Should().Be(AuditFlag.Changed);
            last.Changes!.Length.Should().Be(changedCount, "every changed property must be recorded exactly once");
            last.Changes!.Select(c => c.Column).Distinct().Count().Should().Be(changedCount, "no duplicates");
        }
    }
}
