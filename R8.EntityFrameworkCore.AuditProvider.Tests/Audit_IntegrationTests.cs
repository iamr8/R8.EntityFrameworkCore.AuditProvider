using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;

[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]

namespace R8.EntityFrameworkCore.AuditProvider.Tests
{
    public class Audit_IntegrationTests : TestFixture
    {
        [Fact]
        public async Task Should_Add_Changes_When_Updated()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Iran"
            };
            DummyDbContext.Add(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "Turkey";
            DummyDbContext.Update(entity);

            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            audits.Should().NotBeEmpty();
            audits.Should().HaveCount(2);

            var firstAudit = audits.MinBy(x => x.DateTime);
            firstAudit.Flag.Should().Be(AuditFlag.Created);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var changes = lastAudit.Changes.ToArray();
            changes.Should().NotBeEmpty();
            changes.Should().Contain(x => x.Key == "Name" && x.OldValue.ToString() == "Iran");
            changes.Should().Contain(x => x.Key == "Name" && x.NewValue.ToString() == "Turkey");
        }

        [Fact]
        public async Task Should_Set_IsDeleted_True_When_Updated_IsDeleted()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Iran",
            };
            DummyDbContext.Add(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.IsDeleted = true;
            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            audits.Should().NotBeEmpty();

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Deleted);
        }

        [Fact]
        public async Task Should_Not_Update_And_Delete_AtTheSameTime()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "US",
            };
            DummyDbContext.Add(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.IsDeleted = true;
            entity.Name = "UK";
            DummyDbContext.Update(entity);
            await Assert.ThrowsAsync<NotSupportedException>(async () => await DummyDbContext.SaveChangesAsync());
        }

        [Fact]
        public async Task Should_Update_Array_ChangedFields()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "test",
            };
            DummyDbContext.Add(entity);

            // We need to save to checkout and provide changes in audit
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.ListOfIntegers.Add(1);

            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var lastChange = lastAudit.Changes[0];
            lastChange.Key.Should().Be("ListOfIntegers");
            lastChange.OldValue.Should().Be("[]");
            lastChange.NewValue.Should().Be("[1]");
        }

        [Fact]
        public async Task Should_Update_Array_ChangedFields2()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "test",
                ListOfIntegers = new List<int> { 1, 2, 3 }
            };
            DummyDbContext.Add(entity);

            // We need to save to checkout and provide changes in audit
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(1000);

            entity.ListOfIntegers.Add(4);

            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var lastChange = lastAudit.Changes[0];
            lastChange.Key.Should().Be("ListOfIntegers");
            lastChange.NewValue.Should().Be("[1,2,3,4]");
        }

        [Fact]
        public async Task Should_Update_DateTime_ChangedFields2()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "test",
                Date = DateTime.UtcNow
            };
            DummyDbContext.MyAuditableEntities.Add(entity);

            // We need to save to checkout and provide changes in audit
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(1000);

            entity.Date = DateTime.MinValue;

            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var lastChange = lastAudit.Changes[0];
            lastChange.Key.Should().Be("Date");
            lastChange.NewValue.Should().Be(DateTime.MinValue.ToString("s"));
        }

        [Fact(Skip = "Incorrect strategy")]
        public async Task Should_NOT_Update_DateTime_When_Auditing_Ignored()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "test",
                Date = DateTime.UtcNow
            };
            DummyDbContext.MyAuditableEntities.Add(entity);

            // We need to save to checkout and provide changes in audit
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(1000);

            entity.Date = DateTime.MinValue;

            DummyDbContext.MyAuditableEntities.IgnoreAuditing().Update(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);

            audits.Should().ContainSingle();

            var lastAudit = audits[0];
            lastAudit.Flag.Should().Be(AuditFlag.Created);
        }

        [Fact]
        public async Task Should_NotUpdate_Ignored_Property()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Arash"
            };
            DummyDbContext.Add(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "Arash 2";
            entity.LastName = "Shabbeh";

            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var changes = lastAudit.Changes;
            changes.Should().NotBeEmpty();
            changes.Should().ContainSingle();
            changes.Should().Contain(change => change.Key == "Name" && change.NewValue.ToString() == "Arash 2" && change.OldValue.ToString() == "Arash");
            changes.Should().NotContain(change => change.Key == "LastName");
        }

        [Fact]
        public async Task Should_Update_String_ChangedFields()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Original"
            };
            DummyDbContext.Add(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "Updated";

            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            audits.Should().NotBeEmpty();

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var lastChange = lastAudit.Changes[0];
            lastChange.Key.Should().Be("Name");
            lastChange.OldValue.Should().Be("Original");
            lastChange.NewValue.Should().Be("Updated");
        }

        [Fact]
        public async Task Should_Set_IsDeleted_True_When_Removed()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Iran",
            };
            DummyDbContext.Add(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            DummyDbContext.Remove(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            audits.Should().NotBeEmpty();

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Deleted);
        }

        [Fact]
        public async Task Should_DeletedPermanently_When_Entity_IsNotDeletable()
        {
            // Act
            var auditable = new MyAuditableEntity
            {
                Name = "A"
            };
            DummyDbContext.Add(auditable);
            await DummyDbContext.SaveChangesAsync();

            var entity = new MyEntity
            {
                Name = "1.0.0",
                MyAuditableEntityId = auditable.Id
            };
            DummyDbContext.Add(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            DummyDbContext.Remove(entity);
            await DummyDbContext.SaveChangesAsync();

            entity = await DummyDbContext.MyEntities.FirstOrDefaultAsync(x => x.Name == "1.0.0");

            // Arrange
            entity.Should().BeNull();
        }

        [Fact]
        public async Task Should_Add_Changes_When_Operations_Done()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Premier League",
            };
            DummyDbContext.Add(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            DummyDbContext.Remove(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.IsDeleted = false;
            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "LaLiga";
            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            entity = await DummyDbContext.MyAuditableEntities.FirstOrDefaultAsync(x => x.Name == "LaLiga");

            // Arrange
            entity.Should().NotBeNull();

            var audits = entity.GetAudits();
            audits.Should().NotBeEmpty();
            audits.Should().HaveCount(4);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);
        }

        [Fact]
        public async Task Should_Set_IsDeleted_True_When_Removed_Twice()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Iraq",
            };
            DummyDbContext.Add(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            DummyDbContext.Remove(entity);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            DummyDbContext.Remove(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            entity.Should().NotBeNull();

            var audits = entity.GetAudits();
            audits.Should().NotBeEmpty();

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Deleted);
        }

        [Fact]
        public async Task Should_Add_Created_When_Added()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Iran",
            };

            DummyDbContext.Add(entity);

            await DummyDbContext.SaveChangesAsync();

            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            var firstAudit = audits.MinBy(x => x.DateTime);

            firstAudit.Flag.Should().Be(lastAudit.Flag);
            lastAudit.Flag.Should().Be(AuditFlag.Created);
        }
    }
}