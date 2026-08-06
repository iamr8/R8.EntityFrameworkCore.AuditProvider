using System.Text.Json;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests;
using R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests
{
    // Targeted coverage for interceptor branches not reached by the behavioural tests.
    public class Interceptor_EdgeCase_Tests
    {
        // Implements the marker base but NEITHER IAuditJsonStorage NOR IAuditStorage.
        private sealed class OnlyBaseStorageEntity : IAuditActivator, IAuditStorageBase
        {
        }

        private static EntityFrameworkAuditProviderInterceptor CreateInterceptor(AuditProviderOptions? options = null)
            => new(options ?? new AuditProviderOptions(), Substitute.For<IServiceProvider>(),
                NullLogger<EntityFrameworkAuditProviderInterceptor>.Instance);

        [Fact]
        public void AckAudits_throws_when_storage_is_only_the_base_interface()
        {
            using var dbContext = new PostgreSqlDbContextFactory().CreateDbContext(Array.Empty<string>());
            var interceptor = CreateInterceptor();

            var entity = new OnlyBaseStorageEntity();
            var entry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Added, entity, Array.Empty<PropertyEntry>());

            // AppendAudit cannot resolve a concrete storage shape and must reject it.
            Assert.Throws<NotSupportedException>(() => interceptor.AckAudits(entry, dbContext));
        }

        [Fact]
        public void AckAudits_skips_property_that_is_null_before_and_after()
        {
            using var dbContext = new PostgreSqlDbContextFactory().CreateDbContext(Array.Empty<string>());
            var interceptor = CreateInterceptor();

            var entity = new MyAuditableEntity { Name = null };
            var members = entity.GetChangeTrackerMembers(dbContext);
            members.Update(x => x.Name, (string?)null); // modified, but null -> null

            var entry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, members);
            interceptor.AckAudits(entry, dbContext);

            // A null-to-null value is not a change, so nothing is stored.
            entity.Audits.Should().BeNull();
        }

        [Fact]
        public void AckAudits_flags_undelete_when_IsDeleted_goes_true_to_false()
        {
            using var dbContext = new PostgreSqlDbContextFactory().CreateDbContext(Array.Empty<string>());
            var interceptor = CreateInterceptor();

            var entity = new MyAuditableEntity { Name = "x", IsDeleted = true };
            var members = entity.GetChangeTrackerMembers(dbContext);
            members.Update(x => x.IsDeleted, false); // restore

            var entry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, members);
            interceptor.AckAudits(entry, dbContext);

            var audits = entity.GetAuditCollection();
            audits.Should().NotBeNull();
            audits!.Last(true)!.Value.Flag.Should().Be(AuditFlag.UnDeleted);
        }

        [Fact]
        public void AckAudits_ignores_a_direct_change_to_a_framework_owned_date()
        {
            using var dbContext = new PostgreSqlDbContextFactory().CreateDbContext(Array.Empty<string>());
            var interceptor = CreateInterceptor();

            var entity = new MyAuditableEntityWithUpdateDate { Name = "x" };
            var members = entity.GetChangeTrackerMembers(dbContext);
            members.Update(x => x.UpdateDate, DateTime.UtcNow); // UpdateDate is managed by the provider

            var entry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, members);
            interceptor.AckAudits(entry, dbContext);

            // The framework-owned date column is skipped as a change, so no "changed" audit is stored.
            entity.Audits.Should().BeNull();
        }

        [Fact]
        public void AckAudits_ignores_a_value_that_serializes_to_the_same_json()
        {
            using var dbContext = new PostgreSqlDbContextFactory().CreateDbContext(Array.Empty<string>());
            var interceptor = CreateInterceptor();

            // Different JsonDocument instances (so CLR reference equality is false) with identical content.
            var entity = new MyAuditableEntity { Name = "x", Payload = JsonDocument.Parse("{\"a\":1}") };
            var members = entity.GetChangeTrackerMembers(dbContext);
            members.Update(x => x.Payload, JsonDocument.Parse("{\"a\":1}"));

            var entry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, members);
            interceptor.AckAudits(entry, dbContext);

            // The serialized JSON is identical, so it is not recorded as a change.
            entity.Audits.Should().BeNull();
        }
    }
}
