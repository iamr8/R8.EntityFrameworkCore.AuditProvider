using Microsoft.EntityFrameworkCore.ChangeTracking;

using FluentAssertions;

using R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests;
using R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests
{
    // Documents why a fixed-size rented buffer for the change set is unsafe in a general library:
    // EntityEntry.Members is a lazy LINQ concat (properties + navigations), so it is NOT countable.
    // A rent-a-fixed-fallback-then-throw strategy would therefore always use its fallback size and throw
    // on any entity whose audited change count exceeds it.
    public class ArrayPoolProbe_Tests
    {
        [Fact]
        public void EntityEntry_Members_is_not_countable_so_a_fixed_buffer_cannot_be_sized_from_it()
        {
            using var db = new PostgreSqlDbContextFactory().CreateDbContext(System.Array.Empty<string>());
            var entry = db.Entry(new MyAuditableEntity { Name = "x" });

            IEnumerable<MemberEntry> members = entry.Members;
            var countable = members.TryGetNonEnumeratedCount(out _);

            countable.Should().BeFalse(
                "EntityEntry.Members is a lazy concat of properties + navigations, so a rented buffer " +
                "cannot be sized from it without materializing — the fork's fixed fallback is always used");
        }
    }
}
