using Microsoft.EntityFrameworkCore;
using R8.EventSourcing.PostgreSQL.Tests.Entities;

namespace R8.EventSourcing.PostgreSQL.Tests
{
    public class AuditTests : TestFixture
    {
        [Fact]
        public async Task Should_Add_Changes_When_Updated()
        {
            // Act
            var entity = new FirstEntity
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
            Assert.NotEmpty(audits);
            Assert.Equal(2, audits.Length);
            Assert.Equal(AuditFlags.Created, audits.MinBy(x => x.DateTime).Flag);
            Assert.Equal(AuditFlags.Changed, audits.MaxBy(x => x.DateTime).Flag);
            Assert.NotEmpty(audits.OrderByDescending(x => x.DateTime).First().Changes);
            Assert.Contains(audits.OrderByDescending(x => x.DateTime).First().Changes, x => x.Key == "Name" && x.OldValue.ToString() == "Iran");
            Assert.Contains(audits.OrderByDescending(x => x.DateTime).First().Changes, x => x.Key == "Name" && x.NewValue.ToString() == "Turkey");
        }

        [Fact]
        public async Task Should_Set_IsDeleted_True_When_Updated_IsDeleted()
        {
            // Act
            var entity = new FirstEntity
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
            Assert.NotEmpty(audits);

            Assert.Equal(2, audits.Length);
            Assert.Equal(AuditFlags.Deleted, audits.MaxBy(x => x.DateTime).Flag);
        }

        [Fact]
        public async Task Should_Not_Update_And_Delete_AtTheSameTime()
        {
            // Act
            var entity = new FirstEntity
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
            var entity = new FirstEntity
            {
                Name = "test",
            };
            DummyDbContext.Add(entity);

            // We need to save to checkout and provide changes in audit
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);

            entity.ArrayOfIntegers.Add(1);

            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);

            Assert.Equal(2, audits.Length);
            Assert.Equal(AuditFlags.Changed, audits.MaxBy(x => x.DateTime).Flag);
            Assert.Equal("ArrayOfIntegers", audits.MaxBy(x => x.DateTime).Changes.First().Key);
            Assert.Null(audits.MaxBy(x => x.DateTime).Changes.First().OldValue);
            Assert.Equal("[1]", audits.MaxBy(x => x.DateTime).Changes.First().NewValue);
        }

        [Fact]
        public async Task Should_Update_Array_ChangedFields2()
        {
            // Act
            var entity = new FirstEntity
            {
                Name = "test",
                ArrayOfIntegers = new List<int> { 1, 2, 3 }
            };
            DummyDbContext.Add(entity);

            // We need to save to checkout and provide changes in audit
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(1000);

            entity.ArrayOfIntegers.Add(4);

            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);

            Assert.Equal(2, audits.Length);
            Assert.Equal(AuditFlags.Changed, audits.MaxBy(x => x.DateTime).Flag);
            Assert.Equal("ArrayOfIntegers", audits.MaxBy(x => x.DateTime).Changes.First().Key);
            Assert.Equal("[1,2,3,4]", audits.MaxBy(x => x.DateTime).Changes.First().NewValue);
        }
        
        [Fact]
        public async Task Should_Update_DateTime_ChangedFields2()
        {
            // Act
            var entity = new SecondEntity
            {
                Name = "test",
                Date = DateTime.UtcNow
            };
            DummyDbContext.Add(entity);

            // We need to save to checkout and provide changes in audit
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(1000);

            entity.Date = DateTime.MinValue;

            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);

            Assert.Equal(2, audits.Length);
            Assert.Equal(AuditFlags.Changed, audits.MaxBy(x => x.DateTime).Flag);
            Assert.Equal("Date", audits.MaxBy(x => x.DateTime).Changes.First().Key);
            Assert.Null(audits.MaxBy(x => x.DateTime).Changes.First().NewValue);
        }
        
        [Fact]
        public async Task Should_Update_DateTime_ChangedFields()
        {
            // Act
            var entity = new SecondEntity
            {
                Name = "test",
            };
            DummyDbContext.Add(entity);

            // We need to save to checkout and provide changes in audit
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(1000);

            entity.Date = DateTime.UtcNow;

            DummyDbContext.Update(entity);
            await DummyDbContext.SaveChangesAsync();

            // Arrange
            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);

            Assert.Equal(2, audits.Length);
            Assert.Equal(AuditFlags.Changed, audits.MaxBy(x => x.DateTime).Flag);
            Assert.Equal("Date", audits.MaxBy(x => x.DateTime).Changes.First().Key);
            Assert.Null(audits.MaxBy(x => x.DateTime).Changes.First().OldValue);
            Assert.Equal(entity.Date.ToString(), audits.MaxBy(x => x.DateTime).Changes.First().NewValue);
        }
        
        [Fact]
        public async Task Should_NotUpdate_Ignored_Property()
        {
            // Act
            var entity = new FirstEntity
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
        
            Assert.Equal(2, audits.Length);
            Assert.Equal(AuditFlags.Changed, audits.MaxBy(x => x.DateTime).Flag);
            var changes = audits.MaxBy(x => x.DateTime).Changes;
            Assert.Single(changes);
            Assert.Contains(changes, change => change.Key == "Name" && change.NewValue.ToString() == "Arash 2" && change.OldValue.ToString() == "Arash");
            Assert.DoesNotContain(changes, change => change.Key == "LastName");
        }
        
        [Fact]
        public async Task Should_Update_String_ChangedFields()
        {
            // Act
            var entity = new FirstEntity
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
            Assert.NotEmpty(audits);
        
            Assert.Equal(2, audits.Length);
            Assert.Equal(AuditFlags.Changed, audits.MaxBy(x => x.DateTime).Flag);
            Assert.Equal("Name", audits.MaxBy(x => x.DateTime).Changes.First().Key);
            Assert.Equal("Original", audits.MaxBy(x => x.DateTime).Changes.First().OldValue);
            Assert.Equal("Updated", audits.MaxBy(x => x.DateTime).Changes.First().NewValue);
        }
        
        [Fact]
        public async Task Should_Set_IsDeleted_True_When_Removed()
        {
            // Act
            var entity = new FirstEntity
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
            Assert.NotEmpty(audits);
        
            Assert.Equal(2, audits.Length);
            Assert.Null(audits.MaxBy(x => x.DateTime).Changes);
            Assert.Equal(AuditFlags.Deleted, audits.MaxBy(x => x.DateTime).Flag);
        }
        
        [Fact]
        public async Task Should_DeletedPermanently_When_Entity_IsNotDeletable()
        {
            // Act
            var entity = new ThirdEntity
            {
                Name = "1.0.0",
            };
            DummyDbContext.Add(entity);
            await DummyDbContext.SaveChangesAsync();
        
            await Task.Delay(500);
        
            DummyDbContext.Remove(entity);
            await DummyDbContext.SaveChangesAsync();
        
            entity = await DummyDbContext.ThirdEntities.FirstOrDefaultAsync(x => x.Name == "1.0.0");
        
            // Arrange
            Assert.Null(entity);
        }
        
        [Fact]
        public async Task Should_Add_Changes_When_Operations_Done()
        {
            // Act
            var entity = new FirstEntity
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
        
            entity = await DummyDbContext.FirstEntities.FirstOrDefaultAsync(x => x.Name == "LaLiga");
        
            // Arrange
            Assert.NotNull(entity);
        
            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);
        
            Assert.Equal(4, audits.Length);
            Assert.Equal(AuditFlags.Changed, audits.MaxBy(x => x.DateTime).Flag);
        }
        
        [Fact]
        public async Task Should_Set_IsDeleted_True_When_Removed_Twice()
        {
            // Act
            var entity = new FirstEntity
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
            Assert.NotNull(entity);

            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);
        
            Assert.Equal(2, audits.Length);
            Assert.Equal(AuditFlags.Deleted, audits.MaxBy(x => x.DateTime).Flag);
        }
        
        [Fact]
        public async Task Should_Have_EntityNavigation_When_Updated_ForeignKeyId()
        {
            // Act
            var entity = new FirstEntity
            {
                Name = "Tag",
            };
            DummyDbContext.Add(entity);
            await DummyDbContext.SaveChangesAsync();
        
            var entity2 = new SecondEntity
            {
                Name = "Tag2",
            };
            DummyDbContext.Add(entity2);
            await DummyDbContext.SaveChangesAsync();

            await Task.Delay(500);
        
            entity2.FirstEntityId = entity.Id;
            DummyDbContext.Update(entity2);
            await DummyDbContext.SaveChangesAsync();

            var audits = entity2.GetAudits();
            var changes = audits.MaxBy(x => x.DateTime).Changes;
        
            // Arrange
            Assert.Single(changes);
        
            Assert.Contains(changes, change => change.Key == nameof(SecondEntity.FirstEntityId));
        }

        [Fact]
        public async Task Should_Add_Created_When_Added()
        {
            // Act
            var entity = new FirstEntity
            {
                Name = "Iran",
            };

            DummyDbContext.Add(entity);

            await DummyDbContext.SaveChangesAsync();

            var audits = entity.GetAudits();
            Assert.NotEmpty(audits);

            var lastAudit = audits.MaxBy(x => x.DateTime);
            var createdAudit = audits.MinBy(x => x.DateTime);

            Assert.Equal(lastAudit, createdAudit);
            Assert.Equal(AuditFlags.Created, lastAudit.Flag);
            Assert.Equal(audits.MinBy(x => x.DateTime), audits.MaxBy(x => x.DateTime));
        }
    }
}