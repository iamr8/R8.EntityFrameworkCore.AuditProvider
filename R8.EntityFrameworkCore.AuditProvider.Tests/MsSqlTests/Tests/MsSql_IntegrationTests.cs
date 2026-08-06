using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;
using R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.Entities;

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
            var audits = entity.GetAuditCollection();
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
            var audits = entity.GetAuditCollection();
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
            entity = MsSqlDbContext.Update(entity).Entity;
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
            var audits = entity.GetAuditCollection();
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
            var audits = entity.Audits;
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
            var audits = entity.GetAuditCollection();
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
        public async Task Should_Update_When_Entity_IsNotTracked()
        {
            // Act — create the entity in one scope, then update it in a fresh scope where it starts
            // detached (loaded AsNoTracking). The supported way to audit a disconnected update is to
            // Attach the entity (so EF captures the original snapshot) and THEN mutate it, giving the
            // interceptor a real old/new value to diff. A bare Update() on a detached entity has no
            // tracked baseline, so no field-level change could be detected.
            MyAuditableEntity entity;
            await using (var scope1 = ServiceProvider.CreateAsyncScope())
            {
                await using var dbContext1 = scope1.ServiceProvider.GetRequiredService<MsSqlDbContext>();
                entity = new MyAuditableEntity
                {
                    Name = "Original"
                };
                dbContext1.Add(entity);
                await dbContext1.SaveChangesAsync();
            }

            await Task.Delay(500);

            await using var scope2 = ServiceProvider.CreateAsyncScope();
            await using var dbContext2 = scope2.ServiceProvider.GetRequiredService<MsSqlDbContext>();
            var entity2 = await dbContext2.MyAuditableEntities.AsNoTracking().FirstAsync(x => x.Id == entity.Id);

            dbContext2.Attach(entity2);
            entity2.Name = "Updated";
            await dbContext2.SaveChangesAsync();

            // Arrange
            var audits = entity2.GetAuditCollection();
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
            var audits = entity.GetAuditCollection();
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

            var audits = entity.GetAuditCollection();
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

            var audits = entity.GetAuditCollection();
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

            var audits = entity.GetAuditCollection();
            Assert.NotEmpty(audits);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            var firstAudit = audits.MinBy(x => x.DateTime);

            firstAudit.Flag.Should().Be(lastAudit.Flag);
            lastAudit.Flag.Should().Be(AuditFlag.Created);
        }

        [Fact]
        public async Task should_encode_value_when_the_value_is_string_json()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "{\"key\": \"value\"}",
            };

            MsSqlDbContext.Add(entity);

            await MsSqlDbContext.SaveChangesAsync();

            entity.Name = "{\"key\": \"value2\"}";
            MsSqlDbContext.Update(entity);
            await MsSqlDbContext.SaveChangesAsync();

            var audits = entity.GetAuditCollection();
            Assert.NotEmpty(audits);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var changes = lastAudit.Changes.ToArray();
            changes.Should().NotBeEmpty();
            changes.Should().Contain(x => x.Column == "Name" && x.OldValue.Value.ToString() == "{\"key\": \"value\"}" && x.NewValue.Value.ToString() == "{\"key\": \"value2\"}");
        }

        [Fact]
        public async Task should_not_flag_a_unchanged_json_value()
        {
            // Act
            var entity = new MyAuditableEntity
            {
                Name = "{\"key\": \"value\"}",
            };

            MsSqlDbContext.Add(entity);

            await MsSqlDbContext.SaveChangesAsync();

            entity.Name = "{\"key\": \"value2\"}";
            MsSqlDbContext.Update(entity);
            await MsSqlDbContext.SaveChangesAsync();

            entity.Name = "{\"key\": \"value2\"}";
            MsSqlDbContext.Update(entity);
            await MsSqlDbContext.SaveChangesAsync();

            var audits = entity.GetAuditCollection();
            Assert.NotEmpty(audits);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);
            audits.Where(x => x.Flag == AuditFlag.Changed).Should().HaveCount(1);

            var changes = lastAudit.Changes.ToArray();
            changes.Should().NotBeEmpty();
            changes.Should().Contain(x => x.Column == "Name" && x.OldValue.Value.ToString() == "{\"key\": \"value\"}" && x.NewValue.Value.ToString() == "{\"key\": \"value2\"}");
        }

        [Fact]
        public async Task Should_Trim_Audits_To_MaxStoredAudits_Across_DbRoundTrips()
        {
            // The MsSql fixture sets MaxStoredAudits = 10. Drive well past the limit and verify the cap
            // survives serialize → persist → re-deserialize on every SaveChanges, and that the original
            // Created audit is always retained as the first entry.
            var entity = new MyAuditableEntity { Name = "v0" };
            MsSqlDbContext.Add(entity);
            await MsSqlDbContext.SaveChangesAsync();

            for (var i = 1; i <= 15; i++)
            {
                entity.Name = $"v{i}";
                MsSqlDbContext.Update(entity);
                await MsSqlDbContext.SaveChangesAsync();
            }

            // Reload from the database to prove the trimming is persisted, not just in memory.
            var reloaded = await MsSqlDbContext.MyAuditableEntities.AsNoTracking().FirstAsync(x => x.Id == entity.Id);
            var audits = reloaded.GetAuditCollection();

            audits.Should().HaveCount(10);
            audits.MinBy(x => x.DateTime).Flag.Should().Be(AuditFlag.Created);
            audits.MaxBy(x => x.DateTime).Flag.Should().Be(AuditFlag.Changed);
        }

        [Fact]
        public async Task Should_Audit_Correctly_Under_Concurrent_Load()
        {
            // Stress the singleton interceptor from many threads at once: each task uses its own scope
            // (its own DbContext) but they all share the one registered interceptor instance. If the
            // interceptor held mutable state, audit trails would leak across entities or be lost.
            const int concurrency = 50;
            const int updatesPerEntity = 3;

            var tasks = Enumerable.Range(0, concurrency).Select(async i =>
            {
                await using var scope = ServiceProvider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<MsSqlDbContext>();

                var entity = new MyAuditableEntity { Name = $"c{i}-0" };
                db.Add(entity);
                await db.SaveChangesAsync();

                for (var u = 1; u <= updatesPerEntity; u++)
                {
                    entity.Name = $"c{i}-{u}";
                    db.Update(entity);
                    await db.SaveChangesAsync();
                }

                var audits = entity.GetAuditCollection();
                return (
                    count: audits?.Count ?? 0,
                    lastFlag: audits?.MaxBy(x => x.DateTime).Flag,
                    changedName: audits?.MaxBy(x => x.DateTime).Changes?[0].NewValue?.ToString(),
                    expectedName: $"c{i}-{updatesPerEntity}");
            }).ToArray();

            var results = await Task.WhenAll(tasks);

            // Every entity independently sees exactly its own 1 Created + N Changed audits, ending Changed,
            // with the final change reflecting that entity's last value — no cross-entity contamination.
            results.Should().OnlyContain(r => r.count == updatesPerEntity + 1);
            results.Should().OnlyContain(r => r.lastFlag == AuditFlag.Changed);
            results.Should().OnlyContain(r => r.changedName == r.expectedName);
        }

        [Fact]
        public async Task Should_Record_Converted_Provider_Value_For_ValueConverter_Property()
        {
            // Status is an enum mapped to a string column via an explicit value converter. The audit must
            // record what the provider stores ("Active"/"Inactive"), not the raw CLR/enum value.
            var entity = new MyAuditableEntity { Name = "x", Status = AuditStatus.Active };
            MsSqlDbContext.Add(entity);
            await MsSqlDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.Status = AuditStatus.Inactive;
            MsSqlDbContext.Update(entity);
            await MsSqlDbContext.SaveChangesAsync();

            var audits = entity.GetAuditCollection();
            audits.Should().NotBeNull();

            var lastAudit = audits!.MaxBy(x => x.DateTime);
            lastAudit.Flag.Should().Be(AuditFlag.Changed);

            var change = lastAudit.Changes!.Single(c => c.Column == nameof(MyAuditableEntity.Status));
            change.OldValue!.Value.GetRawText().Should().Be("\"Active\"");
            change.NewValue!.Value.GetRawText().Should().Be("\"Inactive\"");
        }
    }
}