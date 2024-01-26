using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.Entities;

using Xunit.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.Tests
{
    public class MsSql_IntegrationTests : MsSqlTestFixture, IDisposable
    {
        private readonly ITestOutputHelper _outputHelper;

        public MsSql_IntegrationTests(ITestOutputHelper outputHelper)
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
            MsSqlDbContext.Add(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "Turkey";
            MsSqlDbContext.Update(entity);

            await MsSqlDbContext.SaveChangesAsync();

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
            MsSqlDbContext.Add(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.IsDeleted = true;
            MsSqlDbContext.Update(entity);
            await MsSqlDbContext.SaveChangesAsync();

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
            MsSqlDbContext.Add(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.IsDeleted = true;
            entity.Name = "UK";
            MsSqlDbContext.Update(entity);
            await Assert.ThrowsAsync<NotSupportedException>(async () => await MsSqlDbContext.SaveChangesAsync());
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
            MsSqlDbContext.MyAuditableEntities.Add(entity);

            // We need to save to checkout and provide changes in audit
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(1000);

            entity.Date = DateTime.MinValue;

            MsSqlDbContext.Update(entity);
            await MsSqlDbContext.SaveChangesAsync();

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
            MsSqlDbContext.Add(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "Arash 2";
            entity.LastName = "Shabbeh";

            MsSqlDbContext.Update(entity);
            await MsSqlDbContext.SaveChangesAsync();

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
            MsSqlDbContext.Add(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "Updated";

            MsSqlDbContext.Update(entity);
            await MsSqlDbContext.SaveChangesAsync();

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
            MsSqlDbContext.Add(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            MsSqlDbContext.Remove(entity);
            await MsSqlDbContext.SaveChangesAsync();

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
            MsSqlDbContext.Add(auditable);
            await MsSqlDbContext.SaveChangesAsync();

            var entity = new MyEntity
            {
                Name = "1.0.0",
                MyAuditableEntityId = auditable.Id
            };
            MsSqlDbContext.Add(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            MsSqlDbContext.Remove(entity);
            await MsSqlDbContext.SaveChangesAsync();

            entity = await MsSqlDbContext.MyEntities.FirstOrDefaultAsync(x => x.Name == "1.0.0");

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
            MsSqlDbContext.Add(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            MsSqlDbContext.Remove(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.IsDeleted = false;
            MsSqlDbContext.Update(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Name = "LaLiga";
            MsSqlDbContext.Update(entity);
            await MsSqlDbContext.SaveChangesAsync();

            entity = await MsSqlDbContext.MyAuditableEntities.FirstOrDefaultAsync(x => x.Name == "LaLiga");

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
            MsSqlDbContext.Add(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            MsSqlDbContext.Remove(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            MsSqlDbContext.Remove(entity);
            await MsSqlDbContext.SaveChangesAsync();

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

            MsSqlDbContext.Add(entity);

            await MsSqlDbContext.SaveChangesAsync();

            var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
            Assert.NotEmpty(audits);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            var firstAudit = audits.MinBy(x => x.DateTime);

            firstAudit.Flag.Should().Be(lastAudit.Flag);
            lastAudit.Flag.Should().Be(AuditFlag.Created);
        }
    }
}