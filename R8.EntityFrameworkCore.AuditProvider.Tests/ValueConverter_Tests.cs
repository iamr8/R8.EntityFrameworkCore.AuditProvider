using System.Text.Json;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests
{
    // Covers the value-converter path in the interceptor: when a property has an EF value converter,
    // the audit must record the value the provider stores, not the raw CLR value. Uses a throwaway
    // DbContext (model metadata only, no connection) so it needs no migration and no shared-model change.
    public class ValueConverter_Tests
    {
        private enum ProbeStatus
        {
            Active,
            Inactive
        }

        private sealed class EnumConverterProbe : IAuditActivator, IAuditJsonStorage
        {
            public int Id { get; set; }
            public ProbeStatus Status { get; set; }
            public JsonElement? Audits { get; set; }
        }

        private sealed class ConverterProbeDbContext : DbContext
        {
            // SQL Server (unlike Npgsql, which maps enums natively) turns HasConversion<string> into a real
            // EF ValueConverter — which is exactly the path under test. Model metadata only; never connects.
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseSqlServer("Server=localhost;Database=probe;TrustServerCertificate=true");

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                var entity = modelBuilder.Entity<EnumConverterProbe>();
                entity.HasKey(x => x.Id);
                entity.Ignore(x => x.Audits); // set in-memory by the interceptor, not persisted here
                entity.Property(x => x.Status).HasConversion(
                    new ValueConverter<ProbeStatus, string>(
                        v => v.ToString(),
                        v => (ProbeStatus)Enum.Parse(typeof(ProbeStatus), v)));
            }
        }

        [Fact]
        public void Records_the_converted_provider_value_not_the_clr_value()
        {
            AuditProviderConfiguration.JsonOptions ??= new AuditProviderOptions().JsonOptions;

            using var dbContext = new ConverterProbeDbContext();
            var interceptor = new EntityFrameworkAuditProviderInterceptor(
                new AuditProviderOptions(), Substitute.For<IServiceProvider>(),
                NullLogger<EntityFrameworkAuditProviderInterceptor>.Instance);

            // Diagnostic: confirm the model actually carries a value converter for Status.
            var statusProp = dbContext.Model.FindEntityType(typeof(EnumConverterProbe))!
                .FindProperty(nameof(EnumConverterProbe.Status))!;
            statusProp.GetValueConverter().Should().NotBeNull("HasConversion<string> should register a converter");

            var entity = new EnumConverterProbe { Id = 1, Status = ProbeStatus.Active };
            var members = entity.GetChangeTrackerMembers(dbContext);
            members.Update(x => x.Status, ProbeStatus.Inactive);

            var entry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, members);
            interceptor.AckAudits(entry, dbContext);

            var audits = entity.GetAuditCollection();
            audits.Should().NotBeNull();

            var change = audits!.MaxBy(x => x.DateTime).Changes![0];
            change.Column.Should().Be("Status");
            // The enum is mapped to a string, so the audit records "Active"/"Inactive" — not the numeric
            // enum value it would serialize to without honoring the converter.
            change.OldValue!.Value.GetRawText().Should().Be("\"Active\"");
            change.NewValue!.Value.GetRawText().Should().Be("\"Inactive\"");
        }
    }
}
