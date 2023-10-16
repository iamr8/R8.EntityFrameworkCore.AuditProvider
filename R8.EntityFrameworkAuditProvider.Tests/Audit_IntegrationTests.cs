// using FluentAssertions;
//
// using Microsoft.EntityFrameworkCore;
//
// using R8.EntityFrameworkAuditProvider.Tests.Entities;
//
// namespace R8.EntityFrameworkAuditProvider.Tests
// {
//     public class Audit_IntegrationTests : TestFixture
//     {
//         [Fact]
//         public async Task Should_Add_Changes_When_Updated()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "Iran"
//             };
//             DummyDbContext.Add(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             entity.Name = "Turkey";
//             DummyDbContext.Update(entity);
//
//             await DummyDbContext.SaveChangesAsync();
//
//             // Arrange
//             var audits = entity.GetAudits();
//             audits.Should().NotBeEmpty();
//             audits.Should().HaveCount(2);
//             
//             var firstAudit = audits.MinBy(x => x.DateTime);
//             firstAudit.Flag.Should().Be(AuditFlag.Created);
//             
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             lastAudit.Flag.Should().Be(AuditFlag.Changed);
//
//             var changes = lastAudit.Changes.ToArray();
//             changes.Should().NotBeEmpty();
//             changes.Should().Contain(x => x.Key == "Name" && x.OldValue.ToString() == "Iran");
//             changes.Should().Contain(x => x.Key == "Name" && x.NewValue.ToString() == "Turkey");
//         }
//
//         [Fact]
//         public async Task Should_Set_IsDeleted_True_When_Updated_IsDeleted()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "Iran",
//             };
//             DummyDbContext.Add(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             entity.IsDeleted = true;
//             DummyDbContext.Update(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             // Arrange
//             var audits = entity.GetAudits();
//             audits.Should().NotBeEmpty();
//
//             audits.Should().HaveCount(2);
//
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             lastAudit.Flag.Should().Be(AuditFlag.Deleted);
//         }
//
//         [Fact]
//         public async Task Should_Not_Update_And_Delete_AtTheSameTime()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "US",
//             };
//             DummyDbContext.Add(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             entity.IsDeleted = true;
//             entity.Name = "UK";
//             DummyDbContext.Update(entity);
//             await Assert.ThrowsAsync<NotSupportedException>(async () => await DummyDbContext.SaveChangesAsync());
//         }
//
//         [Fact]
//         public async Task Should_Update_Array_ChangedFields()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "test",
//             };
//             DummyDbContext.Add(entity);
//
//             // We need to save to checkout and provide changes in audit
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             entity.ArrayOfIntegers.Add(1);
//
//             DummyDbContext.Update(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             // Arrange
//             var audits = entity.GetAudits();
//             Assert.NotEmpty(audits);
//
//             audits.Should().HaveCount(2);
//
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             lastAudit.Flag.Should().Be(AuditFlag.Changed);
//
//             var lastChange = lastAudit.Changes[0];
//             lastChange.Key.Should().Be("ArrayOfIntegers");
//             lastChange.OldValue.Should().BeNull();
//             lastChange.NewValue.Should().Be("[1]");
//         }
//
//         [Fact]
//         public async Task Should_Update_Array_ChangedFields2()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "test",
//                 ArrayOfIntegers = new List<int> { 1, 2, 3 }
//             };
//             DummyDbContext.Add(entity);
//
//             // We need to save to checkout and provide changes in audit
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(1000);
//
//             entity.ArrayOfIntegers.Add(4);
//
//             DummyDbContext.Update(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             // Arrange
//             var audits = entity.GetAudits();
//             Assert.NotEmpty(audits);
//
//             audits.Should().HaveCount(2);
//
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             lastAudit.Flag.Should().Be(AuditFlag.Changed);
//
//             var lastChange = lastAudit.Changes[0];
//             lastChange.Key.Should().Be("ArrayOfIntegers");
//             lastChange.NewValue.Should().Be("[1,2,3,4]");
//         }
//
//         [Fact]
//         public async Task Should_Update_DateTime_ChangedFields2()
//         {
//             // Act
//             var entity = new SecondAuditableEntity
//             {
//                 Name = "test",
//                 Date = DateTime.UtcNow
//             };
//             DummyDbContext.Add(entity);
//
//             // We need to save to checkout and provide changes in audit
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(1000);
//
//             entity.Date = DateTime.MinValue;
//
//             DummyDbContext.Update(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             // Arrange
//             var audits = entity.GetAudits();
//             Assert.NotEmpty(audits);
//
//             audits.Should().HaveCount(2);
//
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             lastAudit.Flag.Should().Be(AuditFlag.Changed);
//
//             var lastChange = lastAudit.Changes[0];
//             lastChange.Key.Should().Be("Date");
//             lastChange.NewValue.Should().BeNull();
//         }
//
//         [Fact]
//         public async Task Should_Update_DateTime_ChangedFields()
//         {
//             // Act
//             var entity = new SecondAuditableEntity
//             {
//                 Name = "test",
//             };
//             DummyDbContext.Add(entity);
//
//             // We need to save to checkout and provide changes in audit
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(1000);
//
//             entity.Date = DateTime.UtcNow;
//
//             DummyDbContext.Update(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             // Arrange
//             var audits = entity.GetAudits();
//             audits.Should().NotBeEmpty();
//
//             audits.Should().HaveCount(2);
//
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             lastAudit.Flag.Should().Be(AuditFlag.Changed);
//
//             var lastChange = lastAudit.Changes[0];
//             lastChange.Key.Should().Be("Date");
//             lastChange.OldValue.Should().BeNull();
//             lastChange.NewValue.Should().Be(entity.Date.ToString());
//         }
//
//         [Fact]
//         public async Task Should_NotUpdate_Ignored_Property()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "Arash"
//             };
//             DummyDbContext.Add(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             entity.Name = "Arash 2";
//             entity.LastName = "Shabbeh";
//
//             DummyDbContext.Update(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             // Arrange
//             var audits = entity.GetAudits();
//             Assert.NotEmpty(audits);
//
//             audits.Should().HaveCount(2);
//             
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             lastAudit.Flag.Should().Be(AuditFlag.Changed);
//             
//             var changes = lastAudit.Changes;
//             changes.Should().NotBeEmpty();
//             changes.Should().ContainSingle();
//             changes.Should().Contain(change => change.Key == "Name" && change.NewValue.ToString() == "Arash 2" && change.OldValue.ToString() == "Arash");
//             changes.Should().NotContain(change => change.Key == "LastName");
//         }
//
//         [Fact]
//         public async Task Should_Update_String_ChangedFields()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "Original"
//             };
//             DummyDbContext.Add(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             entity.Name = "Updated";
//
//             DummyDbContext.Update(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             // Arrange
//             var audits = entity.GetAudits();
//             audits.Should().NotBeEmpty();
//
//             audits.Should().HaveCount(2);
//             
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             lastAudit.Flag.Should().Be(AuditFlag.Changed);
//             
//             var lastChange = lastAudit.Changes[0];
//             lastChange.Key.Should().Be("Name");
//             lastChange.OldValue.Should().Be("Original");
//             lastChange.NewValue.Should().Be("Updated");
//         }
//
//         [Fact]
//         public async Task Should_Set_IsDeleted_True_When_Removed()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "Iran",
//             };
//             DummyDbContext.Add(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             DummyDbContext.Remove(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             // Arrange
//             var audits = entity.GetAudits();
//             audits.Should().NotBeEmpty();
//
//             audits.Should().HaveCount(2);
//             
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             lastAudit.Flag.Should().Be(AuditFlag.Deleted);
//         }
//
//         [Fact]
//         public async Task Should_DeletedPermanently_When_Entity_IsNotDeletable()
//         {
//             // Act
//             var entity = new ThirdEntity
//             {
//                 Name = "1.0.0",
//             };
//             DummyDbContext.Add(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             DummyDbContext.Remove(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             entity = await DummyDbContext.ThirdEntities.FirstOrDefaultAsync(x => x.Name == "1.0.0");
//
//             // Arrange
//             entity.Should().BeNull();
//         }
//
//         [Fact]
//         public async Task Should_Add_Changes_When_Operations_Done()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "Premier League",
//             };
//             DummyDbContext.Add(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             DummyDbContext.Remove(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             entity.IsDeleted = false;
//             DummyDbContext.Update(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             entity.Name = "LaLiga";
//             DummyDbContext.Update(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             entity = await DummyDbContext.FirstEntities.FirstOrDefaultAsync(x => x.Name == "LaLiga");
//
//             // Arrange
//             entity.Should().NotBeNull();
//
//             var audits = entity.GetAudits();
//             audits.Should().NotBeEmpty();
//             audits.Should().HaveCount(4);
//             
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             lastAudit.Flag.Should().Be(AuditFlag.Changed);
//         }
//
//         [Fact]
//         public async Task Should_Set_IsDeleted_True_When_Removed_Twice()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "Iraq",
//             };
//             DummyDbContext.Add(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             DummyDbContext.Remove(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             DummyDbContext.Remove(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             // Arrange
//             entity.Should().NotBeNull();
//
//             var audits = entity.GetAudits();
//             audits.Should().NotBeEmpty();
//
//             audits.Should().HaveCount(2);
//             
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             lastAudit.Flag.Should().Be(AuditFlag.Deleted);
//         }
//
//         [Fact]
//         public async Task Should_Have_EntityNavigation_When_Updated_ForeignKeyId()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "Tag",
//             };
//             DummyDbContext.Add(entity);
//             await DummyDbContext.SaveChangesAsync();
//
//             var entity2 = new SecondAuditableEntity
//             {
//                 Name = "Tag2",
//             };
//             DummyDbContext.Add(entity2);
//             await DummyDbContext.SaveChangesAsync();
//
//             await Task.Delay(500);
//
//             entity2.FirstEntityId = entity.Id;
//             DummyDbContext.Update(entity2);
//             await DummyDbContext.SaveChangesAsync();
//
//             var audits = entity2.GetAudits();
//             var changes = audits.MaxBy(x => x.DateTime).Changes;
//
//             // Arrange
//             changes.Should().ContainSingle();
//             changes.Should().Contain(x => x.Key == nameof(SecondAuditableEntity.FirstEntityId));
//         }
//
//         [Fact]
//         public async Task Should_Add_Created_When_Added()
//         {
//             // Act
//             var entity = new FirstAuditableEntity
//             {
//                 Name = "Iran",
//             };
//
//             DummyDbContext.Add(entity);
//
//             await DummyDbContext.SaveChangesAsync();
//
//             var audits = entity.GetAudits();
//             Assert.NotEmpty(audits);
//
//             var lastAudit = audits.MaxBy(x => x.DateTime);
//             var firstAudit = audits.MinBy(x => x.DateTime);
//
//             firstAudit.Flag.Should().Be(lastAudit.Flag);
//             lastAudit.Flag.Should().Be(AuditFlag.Created);
//         }
//     }
// }