using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities;

using Xunit.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Tests
{
    public class PostgreSql_IntegrationTests : PostgreSqlTestFixture, IDisposable
    {
        private readonly ITestOutputHelper _outputHelper;

        public PostgreSql_IntegrationTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            this.OnWriteLine += _outputHelper.WriteLine;
        }
        
        public void Dispose()
        {
            this.OnWriteLine -= _outputHelper.WriteLine;
        }
        
        [Fact]
        public async Task Should_Add_Changes_When_Updated()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Iran"
            };
            PostgreSqlDbContext.Add(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "Turkey";
            PostgreSqlDbContext.Update(entity);

            await PostgreSqlDbContext.SaveChangesAsync();

            // Arrange
            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
            audits.Should().NotBeEmpty();
            audits.Should().HaveCount(2);

            var firstAudit = audits.MinBy(x => x.DateTime);
            firstAudit.Flag.Should().Be(AuditFlag.Created);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var changes = lastAudit.Changes.ToArray();
            changes.Should().NotBeEmpty();
            changes.Should().Contain(x => x.Column == "Name" && x.OldValue.ToString() == "Iran");
            changes.Should().Contain(x => x.Column == "Name" && x.NewValue.ToString() == "Turkey");
        }

        [Fact]
        public async Task Should_Set_IsDeleted_True_When_Updated_IsDeleted()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Iran",
            };
            PostgreSqlDbContext.Add(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.IsDeleted = true;
            PostgreSqlDbContext.Update(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            // Arrange
            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
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
            PostgreSqlDbContext.Add(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.IsDeleted = true;
            entity.Name = "UK";
            PostgreSqlDbContext.Update(entity);
            await Assert.ThrowsAsync<NotSupportedException>(async () => await PostgreSqlDbContext.SaveChangesAsync());
        }

        [Fact]
        public async Task Should_Update_Array_ChangedFields()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "test",
            };
            PostgreSqlDbContext.Add(entity);

            // We need to save to checkout and provide changes in audit
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.ListOfIntegers.Add(1);

            PostgreSqlDbContext.Update(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            // Arrange
            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
            Assert.NotEmpty(audits);

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var lastChange = lastAudit.Changes[0];
            lastChange.Column.Should().Be("ListOfIntegers");
            lastChange.OldValue.Value.GetRawText().Should().Be("[]");
            lastChange.NewValue.Value.GetRawText().Should().Be("[1]");
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
            PostgreSqlDbContext.Add(entity);

            // We need to save to checkout and provide changes in audit
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(1000);

            entity.ListOfIntegers.Add(4);

            PostgreSqlDbContext.Update(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            // Arrange
            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
            Assert.NotEmpty(audits);

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var lastChange = lastAudit.Changes[0];
            lastChange.Column.Should().Be("ListOfIntegers");
            lastChange.NewValue.Value.GetRawText().Should().Be("[1,2,3,4]");
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
            PostgreSqlDbContext.MyAuditableEntities.Add(entity);

            // We need to save to checkout and provide changes in audit
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(1000);

            entity.Date = DateTime.MinValue;

            PostgreSqlDbContext.Update(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            // Arrange
            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
            Assert.NotEmpty(audits);

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var lastChange = lastAudit.Changes[0];
            lastChange.Column.Should().Be("Date");
            lastChange.NewValue.Value.GetRawText().Should().Be($"\"{DateTime.MinValue:s}\"");
        }

        [Fact]
        public async Task Should_NotUpdate_Ignored_Property()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Arash"
            };
            PostgreSqlDbContext.Add(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "Arash 2";
            entity.LastName = "Shabbeh";

            PostgreSqlDbContext.Update(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            // Arrange
            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
            Assert.NotEmpty(audits);

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var changes = lastAudit.Changes;
            changes.Should().NotBeEmpty();
            changes.Should().ContainSingle();
            changes.Should().Contain(change => change.Column == "Name" && change.NewValue.ToString() == "Arash 2" && change.OldValue.ToString() == "Arash");
            changes.Should().NotContain(change => change.Column == "LastName");
        }

        [Fact]
        public async Task Should_Update_String_ChangedFields()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Original"
            };
            PostgreSqlDbContext.Add(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "Updated";

            PostgreSqlDbContext.Update(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            // Arrange
            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
            audits.Should().NotBeEmpty();

            audits.Should().HaveCount(2);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var lastChange = lastAudit.Changes[0];
            lastChange.Column.Should().Be("Name");
            lastChange.OldValue.Value.GetRawText().Should().Be("\"Original\"");
            lastChange.NewValue.Value.GetRawText().Should().Be("\"Updated\"");
        }

        [Fact]
        public async Task Should_Set_IsDeleted_True_When_Removed()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "Iran",
            };
            PostgreSqlDbContext.Add(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            PostgreSqlDbContext.Remove(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            // Arrange
            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
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
            PostgreSqlDbContext.Add(auditable);
            await PostgreSqlDbContext.SaveChangesAsync();

            var entity = new MyEntity
            {
                Name = "1.0.0",
                MyAuditableEntityId = auditable.Id
            };
            PostgreSqlDbContext.Add(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            PostgreSqlDbContext.Remove(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            entity = await PostgreSqlDbContext.MyEntities.FirstOrDefaultAsync(x => x.Name == "1.0.0");

            // Arrange
            entity.Should().BeNull();
        }

        [Fact]
        public async Task Should_DeletedPermanently_When_Entity_IsNotDeletable_But_Auditable()
        {
            // Act
            var entity = new MyAuditableEntityWithoutSoftDelete()
            {
                Name = "1.0.0",
            };
            PostgreSqlDbContext.Add(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            PostgreSqlDbContext.Remove(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            entity = await PostgreSqlDbContext.MyAuditableEntitiesWithoutSoftDelete.FirstOrDefaultAsync(x => x.Name == "1.0.0");

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
            PostgreSqlDbContext.Add(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            PostgreSqlDbContext.Remove(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.IsDeleted = false;
            PostgreSqlDbContext.Update(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "LaLiga";
            PostgreSqlDbContext.Update(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            entity = await PostgreSqlDbContext.MyAuditableEntities.FirstOrDefaultAsync(x => x.Name == "LaLiga");

            // Arrange
            entity.Should().NotBeNull();

            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
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
            PostgreSqlDbContext.Add(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            PostgreSqlDbContext.Remove(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            PostgreSqlDbContext.Remove(entity);
            await PostgreSqlDbContext.SaveChangesAsync();

            // Arrange
            entity.Should().NotBeNull();

            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
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

            PostgreSqlDbContext.Add(entity);

            await PostgreSqlDbContext.SaveChangesAsync();

            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
            Assert.NotEmpty(audits);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            var firstAudit = audits.MinBy(x => x.DateTime);

            firstAudit.Flag.Should().Be(lastAudit.Flag);
            lastAudit.Flag.Should().Be(AuditFlag.Created);
        }
    }
}