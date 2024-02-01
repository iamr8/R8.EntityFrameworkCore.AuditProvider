using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using NSubstitute;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests;
using R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities;
using R8.XunitLogger;
using Xunit.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests;

public class Audit_UnitTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly ILoggerFactory _loggerFactory;

    public Audit_UnitTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        AuditProviderConfiguration.JsonOptions = new AuditProviderOptions().JsonOptions;
        _loggerFactory = new LoggerFactory().AddXunit(_outputHelper, o => o.MinimumLevel = LogLevel.Debug);
    }
    
    public class MockingAuditEntityEntry : IEntityEntry
    {
        public MockingAuditEntityEntry(EntityState state, object entity, IEnumerable<MemberEntry> members)
        {
            State = state;
            Entity = entity;
            Members = members;
            EntityType = entity.GetType();
        }

        public EntityState State { get; set; }
        public object Entity { get; }
        public IEnumerable<MemberEntry> Members { get; }
        public Type EntityType { get; }

        public void DetectChanges()
        {
        }

        public Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private static DbContext CreateDbContext()
    {
        var dbContext = new PostgreSqlDbContextFactory().CreateDbContext(Array.Empty<string>());
        return dbContext;
    }

    private static IServiceProvider CreateServiceProvider()
    {
        return Substitute.For<IServiceProvider>();
    }

    [Theory]
    [InlineData(EntityState.Detached)]
    [InlineData(EntityState.Unchanged)]
    public async Task should_ignore_auditing_on_ignored_state(EntityState state)
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity();
        var entry = new MockingAuditEntityEntry(state, entity, Array.Empty<PropertyEntry>());

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_return_creation_audit_when_GetCreated_called()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);

        members.Update(x => x.Name, "Bar");
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);

        entity.Audits.Should().NotBeNull();
        var auditCollection = (AuditCollection)entity.Audits.Value;
        auditCollection.Should().NotBeNull();

        var createdAudit = auditCollection.GetCreated();
        createdAudit.Should().NotBeNull();
        createdAudit.Value.Flag.Should().Be(AuditFlag.Created);
        createdAudit.Value.DateTime.Should().NotBe(DateTime.MinValue);
    }

    [Fact]
    public void should_not_return_anything_when_AuditCollection_is_empty()
    {
        const string json = "[]";
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json, AuditProviderConfiguration.JsonOptions);
        var auditCollection = (AuditCollection)jsonElement;
        auditCollection.Should().NotBeNull();

        auditCollection.Element.Should().Be(jsonElement);
        auditCollection.GetCreated().Should().BeNull();
        auditCollection.GetLast().Should().BeNull();
    }

    [Fact]
    public async Task should_return_creation_audit_when_GetCreated_called_and_already_deserialized()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);

        members.Update(x => x.Name, "Bar");
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);


        entity.Audits.Should().NotBeNull();
        var auditCollection = (AuditCollection)entity.Audits.Value;
        auditCollection.Should().NotBeNull();

        var audits = auditCollection.Deserialize();
        audits.Should().NotBeNull();
        audits.Should().HaveCount(2);

        var createdAudit = auditCollection.GetCreated();
        createdAudit.Should().NotBeNull();
        createdAudit.Value.Flag.Should().Be(AuditFlag.Created);
        createdAudit.Value.DateTime.Should().NotBe(DateTime.MinValue);
    }

    [Fact]
    public async Task should_not_return_creation_audit_when_GetCreated_called_but_creation_not_exist()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        options.AuditFlagSupport.Created = AuditFlagState.Excluded;
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);
        
        members.Update(x => x.Name, "Bar"); 
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);


        entity.Audits.Should().NotBeNull();
        var auditCollection = (AuditCollection)entity.Audits.Value;
        auditCollection.Should().NotBeNull();

        var createdAudit = auditCollection.GetCreated();
        createdAudit.Should().BeNull();
    }

    [Fact]
    public async Task should_return_last_audit_when_GetLast_called()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);
        
        members.Update(x => x.Name, "Bar");
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);


        entity.Audits.Should().NotBeNull();
        var auditCollection = (AuditCollection)entity.Audits.Value;
        auditCollection.Should().NotBeNull();

        var lastAudit = auditCollection.GetLast();
        lastAudit.Should().NotBeNull();
        lastAudit.Value.Flag.Should().Be(AuditFlag.Changed);
        lastAudit.Value.DateTime.Should().NotBe(DateTime.MinValue);
        lastAudit.Value.Changes.Should().Contain(x => x.Column == nameof(MyAuditableEntity.Name));
    }

    [Fact]
    public async Task should_not_return_deletion_audit_when_GetLast_called_but_deleted_not_included()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);

        members.Update(x => x.Name, "Bar");
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);
        
        members.Update(x => x.IsDeleted, true);
        entry = new MockingAuditEntityEntry(EntityState.Deleted, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);

        entity.Audits.Should().NotBeNull();
        var auditCollection = (AuditCollection)entity.Audits.Value;
        auditCollection.Should().NotBeNull();

        var lastAudit = auditCollection.GetLast(false);
        lastAudit.Should().NotBeNull();
        lastAudit.Value.Flag.Should().Be(AuditFlag.Changed);
        lastAudit.Value.DateTime.Should().NotBe(DateTime.MinValue);
        lastAudit.Value.Changes.Should().Contain(x => x.Column == nameof(MyAuditableEntity.Name));
    }

    [Fact]
    public async Task should_return_deletion_audit_when_GetLast_called_when_deleted_included()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);

        members.Update(x => x.Name, "Bar");
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);
        
        members.Update(x => x.IsDeleted, true);
        entry = new MockingAuditEntityEntry(EntityState.Deleted, entity, members);
        await interceptor.AckAuditsAsync(entry, dbContext);

        entity.Audits.Should().NotBeNull();
        var auditCollection = (AuditCollection)entity.Audits.Value;
        auditCollection.Should().NotBeNull();

        var lastAudit = auditCollection.GetLast(true);
        lastAudit.Should().NotBeNull();
        lastAudit.Value.Flag.Should().Be(AuditFlag.Deleted);
        lastAudit.Value.DateTime.Should().NotBe(DateTime.MinValue);
    }

    [Fact]
    public async Task should_not_store_creation_when_excluded()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        options.AuditFlagSupport.Created = AuditFlagState.Excluded;
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();

        entity.Audits.Should().BeNull();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_creation()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();


        entity.Audits.Should().NotBeNull();
        entity.Audits.Value.ValueKind.Should().Be(JsonValueKind.Array);

        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Created);
        lastAudit.Changes.Should().BeNullOrEmpty();

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_createdate_when_included()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntityWithCreateDate { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();

        entity.Audits.Should().NotBeNull();
        entity.Audits.Value.ValueKind.Should().Be(JsonValueKind.Array);

        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Created);
        lastAudit.Changes.Should().BeNullOrEmpty();

        entity.CreateDate.Should().NotBeNull();
        entity.CreateDate.Should().Be(lastAudit.DateTime);

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_maintainer_audit_user()
    {
        var dbContext = CreateDbContext();

        var auditUser = new AuditProviderUser("1", new Dictionary<string, string>
        {
            { "Username", "iamr8" },
            { "Email", "arash.shabbeh@gmail.com" },
            { "FirstName", "Arash" },
            { "LastName", "Shabbeh" },
            { "Phone", "+989364091209" }
        });
        var options = new AuditProviderOptions { UserProvider = sp => auditUser };
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();

        entity.Audits.Should().NotBeNull();
        entity.Audits.Value.ValueKind.Should().Be(JsonValueKind.Array);

        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Created);
        lastAudit.Changes.Should().BeNullOrEmpty();
        lastAudit.User.HasValue.Should().BeTrue();
        lastAudit.User.Value.UserId.Should().Be(auditUser.UserId);
        lastAudit.User.Value.AdditionalData.Should().NotBeNullOrEmpty();
        lastAudit.User.Value.AdditionalData.Should().ContainKey("Username");
        lastAudit.User.Value.AdditionalData.Should().ContainKey("Email");
        lastAudit.User.Value.AdditionalData.Should().ContainKey("FirstName");
        lastAudit.User.Value.AdditionalData.Should().ContainKey("LastName");
        lastAudit.User.Value.AdditionalData.Should().ContainKey("Phone");

        lastAudit.User.Value.AdditionalData["Username"].Should().Be(auditUser.AdditionalData["Username"]);
        lastAudit.User.Value.AdditionalData["Email"].Should().Be(auditUser.AdditionalData["Email"]);
        lastAudit.User.Value.AdditionalData["FirstName"].Should().Be(auditUser.AdditionalData["FirstName"]);
        lastAudit.User.Value.AdditionalData["LastName"].Should().Be(auditUser.AdditionalData["LastName"]);
        lastAudit.User.Value.AdditionalData["Phone"].Should().Be(auditUser.AdditionalData["Phone"]);

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_maintainer_audit_user_without_additional_user_data()
    {
        var dbContext = CreateDbContext();

        var auditUser = new AuditProviderUser("1");
        var options = new AuditProviderOptions { UserProvider = sp => auditUser };
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();


        entity.Audits.Should().NotBeNull();
        entity.Audits.Value.ValueKind.Should().Be(JsonValueKind.Array);

        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Created);
        lastAudit.Changes.Should().BeNullOrEmpty();
        lastAudit.User.HasValue.Should().BeTrue();
        lastAudit.User.Value.UserId.Should().Be(auditUser.UserId);
        lastAudit.User.Value.AdditionalData.Should().BeNull();

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_deletion()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.IsDeleted, true);
        var entry = new MockingAuditEntityEntry(EntityState.Deleted, entity, members);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();

        entry.State.Should().Be(EntityState.Modified);

        entity.Audits.Should().NotBeNull();
        entity.Audits.Value.ValueKind.Should().Be(JsonValueKind.Array);

        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Deleted);
        lastAudit.Changes.Should().BeNullOrEmpty();

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_deletedate_when_included()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntityWithDeleteDate { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.IsDeleted, true);
        var entry = new MockingAuditEntityEntry(EntityState.Deleted, entity, members);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();


        entity.Audits.Should().NotBeNull();
        entity.Audits.Value.ValueKind.Should().Be(JsonValueKind.Array);

        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        entity.DeleteDate.Should().NotBeNull();
        entity.DeleteDate.Should().Be(audits[0].DateTime);

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Deleted);
        lastAudit.Changes.Should().BeNullOrEmpty();

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_updatedate_when_updated_and_deletedate_included()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntityWithUpdateDateAndDeleteDate
        {
            Name = "Foo",
            DeleteDate = DateTime.UtcNow.AddDays(-1)
        };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Name, "Bar");
        var entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();


        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().ContainSingle();

        entity.DeleteDate.Should().BeNull();
        entity.UpdateDate.Should().NotBeNull();
        entity.UpdateDate.Should().Be(audits[0].DateTime);

        var change = audit.Changes![0];
        change.Column.Should().Be(nameof(MyAuditableEntityWithUpdateDate.Name));
        change.OldValue.Value.GetRawText().Should().Be("\"Foo\"");
        change.NewValue.Value.GetRawText().Should().Be("\"Bar\"");

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_updatedate_when_not_deleted_and_deletedate_included()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntityWithUpdateDateAndDeleteDate
        {
            Name = "Foo",
            IsDeleted = true,
        };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.IsDeleted, false);
        var entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();


        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.UnDeleted);
        audit.Changes.Should().BeNullOrEmpty();

        entity.DeleteDate.Should().BeNull();
        entity.UpdateDate.Should().NotBeNull();
        entity.UpdateDate.Should().Be(audits[0].DateTime);

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_not_store_deletion_when_excluded()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        options.AuditFlagSupport.Deleted = AuditFlagState.Excluded;
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.IsDeleted, true);
        var entry = new MockingAuditEntityEntry(EntityState.Deleted, entity, members);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();


        entity.Audits.Should().BeNull();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_undeletion()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo", IsDeleted = true };
        var propEntries = entity.GetChangeTrackerMembers(dbContext);
        propEntries.Update(x => x.IsDeleted, false);
        var entry = new MockingAuditEntityEntry(EntityState.Modified, entity, propEntries);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();

        entity.Audits.Should().NotBeNull();
        entity.Audits.Value.ValueKind.Should().Be(JsonValueKind.Array);

        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.UnDeleted);
        lastAudit.Changes.Should().BeNullOrEmpty();

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_not_store_undeletion_when_excluded()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        options.AuditFlagSupport.UnDeleted = AuditFlagState.Excluded;
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo", IsDeleted = true };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.IsDeleted, false);
        var entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);

        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(entry, dbContext);
        stopWatch.Stop();


        entity.Audits.Should().BeNull();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_composite_creation_then_changed()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };

        var members = entity.GetChangeTrackerMembers(dbContext);
        var creationEntry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        
        members.Update(x => x.Name, "Bar");
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(modificationEntry, dbContext);

        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().HaveCount(2);

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Created);
        lastAudit.Changes.Should().BeNullOrEmpty();

        var firstAudit = audits[1];
        firstAudit.Flag.Should().Be(AuditFlag.Changed);
        firstAudit.Changes.Should().NotBeNullOrEmpty();
        firstAudit.Changes.Should().ContainSingle();
        firstAudit.Changes[0].Column.Should().Be(nameof(MyAuditableEntity.Name));
        firstAudit.Changes[0].OldValue.Value.GetRawText().Should().Be("\"Foo\"");
        firstAudit.Changes[0].NewValue.Value.GetRawText().Should().Be("\"Bar\"");

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
    }

    [Fact]
    public void should_not_store_audits_more_than_provided_limit_when_has_created()
    {
        var options = new AuditProviderOptions
        {
            MaxStoredAudits = 10
        };
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var mockingAudits = new Audit[]
        {
            new Audit { Flag = AuditFlag.Created, DateTime = DateTime.UtcNow.AddSeconds(-11) },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-10), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Deleted, DateTime = DateTime.UtcNow.AddSeconds(-9) },
            new Audit { Flag = AuditFlag.UnDeleted, DateTime = DateTime.UtcNow.AddSeconds(-8) },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-7), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-6), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-5), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-4), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-3), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-2), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-1), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow, Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } }
        };
        var entity = new MyAuditableEntity
        {
            Name = "Foo",
            Audits = JsonSerializer.SerializeToElement(mockingAudits, AuditProviderConfiguration.JsonOptions)
        };

        var newAudit = new Audit
        {
            Flag = AuditFlag.Deleted,
            DateTime = DateTime.UtcNow.AddSeconds(1)
        };
        var audits = interceptor.AppendAudit(entity, newAudit);
        audits.Should().NotBeNull();
        audits.Should().HaveCount(options.MaxStoredAudits.Value);
        audits[0].Flag.Should().Be(AuditFlag.Created);
        audits[0].DateTime.Should().Be(mockingAudits[0].DateTime);

        audits[1].Flag.Should().Be(mockingAudits[4].Flag);
        audits[1].DateTime.Should().Be(mockingAudits[4].DateTime);

        audits[^2].Flag.Should().Be(mockingAudits[^1].Flag);
        audits[^2].DateTime.Should().Be(mockingAudits[^1].DateTime);

        audits[^1].Flag.Should().Be(newAudit.Flag);
        audits[^1].DateTime.Should().Be(newAudit.DateTime);
    }

    [Fact]
    public void should_not_store_audits_more_than_provided_limit_when_hasnt_created()
    {
        var options = new AuditProviderOptions
        {
            MaxStoredAudits = 10
        };
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var mockingAudits = new Audit[]
        {
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-10), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Deleted, DateTime = DateTime.UtcNow.AddSeconds(-9) },
            new Audit { Flag = AuditFlag.UnDeleted, DateTime = DateTime.UtcNow.AddSeconds(-8) },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-7), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-6), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-5), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-4), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-3), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-2), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-1), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow, Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } }
        };
        var entity = new MyAuditableEntity
        {
            Name = "Foo",
            Audits = JsonSerializer.SerializeToElement(mockingAudits, AuditProviderConfiguration.JsonOptions)
        };

        var newAudit = new Audit
        {
            Flag = AuditFlag.Deleted,
            DateTime = DateTime.UtcNow.AddSeconds(1)
        };
        var audits = interceptor.AppendAudit(entity, newAudit);
        audits.Should().NotBeNull();
        audits.Should().HaveCount(options.MaxStoredAudits.Value);
        audits[0].Flag.Should().Be(mockingAudits[2].Flag);
        audits[0].DateTime.Should().Be(mockingAudits[2].DateTime);

        audits[1].Flag.Should().Be(mockingAudits[3].Flag);
        audits[1].DateTime.Should().Be(mockingAudits[3].DateTime);

        audits[^2].Flag.Should().Be(mockingAudits[^1].Flag);
        audits[^2].DateTime.Should().Be(mockingAudits[^1].DateTime);

        audits[^1].Flag.Should().Be(newAudit.Flag);
        audits[^1].DateTime.Should().Be(newAudit.DateTime);
    }

    [Fact]
    public void should_not_store_audits_more_than_provided_limit_when_limit_equals_to_length()
    {
        var options = new AuditProviderOptions
        {
            MaxStoredAudits = 10
        };
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var mockingAudits = new Audit[]
        {
            new Audit { Flag = AuditFlag.Deleted, DateTime = DateTime.UtcNow.AddSeconds(-9) },
            new Audit { Flag = AuditFlag.UnDeleted, DateTime = DateTime.UtcNow.AddSeconds(-8) },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-7), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-6), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-5), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-4), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-3), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-2), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-1), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow, Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } }
        };
        var entity = new MyAuditableEntity
        {
            Name = "Foo",
            Audits = JsonSerializer.SerializeToElement(mockingAudits, AuditProviderConfiguration.JsonOptions)
        };

        var newAudit = new Audit
        {
            Flag = AuditFlag.Deleted,
            DateTime = DateTime.UtcNow.AddSeconds(1)
        };
        var audits = interceptor.AppendAudit(entity, newAudit);
        audits.Should().NotBeNull();
        audits.Should().HaveCount(options.MaxStoredAudits.Value);
        audits[0].Flag.Should().Be(mockingAudits[1].Flag);
        audits[0].DateTime.Should().Be(mockingAudits[1].DateTime);

        audits[1].Flag.Should().Be(mockingAudits[2].Flag);
        audits[1].DateTime.Should().Be(mockingAudits[2].DateTime);

        audits[^2].Flag.Should().Be(mockingAudits[^1].Flag);
        audits[^2].DateTime.Should().Be(mockingAudits[^1].DateTime);

        audits[^1].Flag.Should().Be(newAudit.Flag);
        audits[^1].DateTime.Should().Be(newAudit.DateTime);
    }

    [Fact]
    public void should_store_audits_when_length_less_than_limit()
    {
        var options = new AuditProviderOptions
        {
            MaxStoredAudits = 10
        };
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var mockingAudits = new Audit[]
        {
            new Audit { Flag = AuditFlag.Deleted, DateTime = DateTime.UtcNow.AddSeconds(-9) },
            new Audit { Flag = AuditFlag.UnDeleted, DateTime = DateTime.UtcNow.AddSeconds(-8) },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-7), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-6), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-5), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
            new Audit { Flag = AuditFlag.Changed, DateTime = DateTime.UtcNow.AddSeconds(-4), Changes = new[] { new AuditChange { Column = "Foo", NewValue = JsonSerializer.Deserialize<JsonElement>("1"), OldValue = JsonSerializer.Deserialize<JsonElement>("1") } } },
        };
        var entity = new MyAuditableEntity
        {
            Name = "Foo",
            Audits = JsonSerializer.SerializeToElement(mockingAudits, AuditProviderConfiguration.JsonOptions)
        };

        var newAudit = new Audit
        {
            Flag = AuditFlag.Deleted,
            DateTime = DateTime.UtcNow.AddSeconds(1)
        };
        var audits = interceptor.AppendAudit(entity, newAudit);
        audits.Should().NotBeNull();
        audits.Should().HaveCount(mockingAudits.Length + 1);
        audits[0].Flag.Should().Be(mockingAudits[0].Flag);
        audits[0].DateTime.Should().Be(mockingAudits[0].DateTime);

        audits[1].Flag.Should().Be(mockingAudits[1].Flag);
        audits[1].DateTime.Should().Be(mockingAudits[1].DateTime);

        audits[^2].Flag.Should().Be(mockingAudits[^1].Flag);
        audits[^2].DateTime.Should().Be(mockingAudits[^1].DateTime);

        audits[^1].Flag.Should().Be(newAudit.Flag);
        audits[^1].DateTime.Should().Be(newAudit.DateTime);
    }

    [Fact]
    public void should_throw_exception_when_limit_equals_to_one_and_created_included()
    {
        var options = new AuditProviderOptions
        {
            MaxStoredAudits = 1
        };
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var mockingAudits = new Audit[]
        {
            new Audit { Flag = AuditFlag.Created, DateTime = DateTime.UtcNow.AddSeconds(-9) },
        };
        var entity = new MyAuditableEntity
        {
            Name = "Foo",
            Audits = JsonSerializer.SerializeToElement(mockingAudits, AuditProviderConfiguration.JsonOptions)
        };

        var newAudit = new Audit
        {
            Flag = AuditFlag.Deleted,
            DateTime = DateTime.UtcNow.AddSeconds(1)
        };

        var action = () => interceptor.AppendAudit(entity, newAudit);
        action.Should().ThrowExactly<InvalidOperationException>();
    }

    [Fact]
    public async Task should_throw_exception_when_using_modification_and_deletion_simultaneously()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Name, "Bar");
        members.Update(x => x.IsDeleted, true);
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await Assert.ThrowsAsync<NotSupportedException>(async () => await interceptor.AckAuditsAsync(modificationEntry, dbContext));
    }

    [Fact]
    public async Task should_throw_exception_when_using_modification_and_undeletion_simultaneously()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo", IsDeleted = true };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Name, "Bar");
        members.Update(x => x.IsDeleted, false);
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await Assert.ThrowsAsync<NotSupportedException>(async () => await interceptor.AckAuditsAsync(modificationEntry, dbContext));
    }

    [Fact]
    public async Task should_not_store_anything_when_not_changes_made()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(modificationEntry, dbContext);
    }

    [Fact]
    public async Task should_not_store_anything_when_not_changes_made2()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Name, "Foo");
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(modificationEntry, dbContext);
    }

    [Fact]
    public async Task should_not_store_properties_with_ignored_attribute()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { LastName = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.LastName, "Bar");
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(modificationEntry, dbContext);
    }

    [Fact]
    public async Task should_store_properties_without_ignored_attribute()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo", LastName = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Name, "Bar");
        members.Update(x => x.LastName, "Bar");
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(modificationEntry, dbContext);
        stopWatch.Stop();


        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().HaveCount(1);

        var firstAudit = audits[0];
        firstAudit.Flag.Should().Be(AuditFlag.Changed);
        firstAudit.Changes.Should().NotBeNullOrEmpty();
        firstAudit.Changes.Should().ContainSingle();
        firstAudit.Changes[0].Column.Should().Be(nameof(MyAuditableEntity.Name));
        firstAudit.Changes[0].OldValue.Value.GetRawText().Should().Be("\"Foo\"");
        firstAudit.Changes[0].NewValue.Value.GetRawText().Should().Be("\"Bar\"");

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_delete_permanently_when_entity_is_not_AuditSoftDelete()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyEntity();
        var members = entity.GetChangeTrackerMembers(dbContext);
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Deleted, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(modificationEntry, dbContext);
        stopWatch.Stop();

        modificationEntry.State.Should().Be(EntityState.Deleted);

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_delete_permanently_when_entity_is_AuditSoftDelete_but_AuditActivator_excluded()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntityWithSoftDeleteWithoutActivator();
        var members = entity.GetChangeTrackerMembers(dbContext);
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Deleted, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(modificationEntry, dbContext);
        stopWatch.Stop();

        modificationEntry.State.Should().Be(EntityState.Deleted);

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_not_store_deletion_state_when_not_implements_IAuditableDelete_even_implements_IAuditable()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntityWithoutSoftDelete();
        var members = entity.GetChangeTrackerMembers(dbContext);
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Deleted, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(modificationEntry, dbContext);
        stopWatch.Stop();


        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_not_store_audit_changes_when_entity_is_not_auditable_on_deletion()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Name, "Bar");
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(modificationEntry, dbContext);
        stopWatch.Stop();


        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_composite_changed_then_changed_again()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Name, "Bar");
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(creationEntry, dbContext);

        members.Update(x => x.Name, "Foo");
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        await interceptor.AckAuditsAsync(modificationEntry, dbContext);

        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().HaveCount(2);

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Changed);
        lastAudit.Changes.Should().NotBeNullOrEmpty();
        lastAudit.Changes.Should().ContainSingle();
        lastAudit.Changes[0].Column.Should().Be(nameof(MyAuditableEntity.Name));
        lastAudit.Changes[0].OldValue.Value.GetRawText().Should().Be("\"Foo\"");
        lastAudit.Changes[0].NewValue.Value.GetRawText().Should().Be("\"Bar\"");

        var firstAudit = audits[1];
        firstAudit.Flag.Should().Be(AuditFlag.Changed);
        firstAudit.Changes.Should().NotBeNullOrEmpty();
        firstAudit.Changes.Should().ContainSingle();
        firstAudit.Changes[0].Column.Should().Be(nameof(MyAuditableEntity.Name));
        firstAudit.Changes[0].OldValue.Value.GetRawText().Should().Be("\"Bar\"");
        firstAudit.Changes[0].NewValue.Value.GetRawText().Should().Be("\"Foo\"");

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
    }

    [Fact]
    public async Task should_store_changes_of_unknown_types_by_interceptor()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Payload = JsonDocument.Parse(@"[{""name"": ""arash""}]") };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Payload, JsonDocument.Parse(@"[{""name"": ""abood""}]"));
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        stopWatch.Stop();

        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Changed);
        lastAudit.Changes.Should().NotBeNullOrEmpty();
        lastAudit.Changes.Should().ContainSingle();
        lastAudit.Changes[0].Column.Should().Be(nameof(MyAuditableEntity.Payload));
        lastAudit.Changes[0].OldValue.Value.GetRawText().Should().Be(@"[{""name"":""arash""}]");
        lastAudit.Changes[0].NewValue.Value.GetRawText().Should().Be(@"[{""name"":""abood""}]");

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_changes_of_datetime_types()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            Date = new DateTime(2021, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            DateOffset = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.FromHours(3))
        };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Date, new DateTime(2022, 1, 1, 1, 1, 1, DateTimeKind.Utc));
        members.Update(x => x.DateOffset, new DateTimeOffset(2022, 1, 1, 1, 1, 1, TimeSpan.FromHours(3)));
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        stopWatch.Stop();


        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().HaveCount(2);
        for (var i = 0; i < audit.Changes!.Length; i++)
        {
            var change = audit.Changes[i];
            change.Column.Should().Be(i switch
            {
                0 => nameof(MyAuditableEntity.Date),
                1 => nameof(MyAuditableEntity.DateOffset),
                _ => throw new ArgumentOutOfRangeException()
            });
            change.OldValue.Value.GetRawText().Should().Be(i switch
            {
                0 => "\"2021-01-01T01:01:01Z\"",
                1 => "\"2021-01-01T01:01:01+03:00\"",
                _ => throw new ArgumentOutOfRangeException()
            });
            change.NewValue.Value.GetRawText().Should().Be(i switch
            {
                0 => "\"2022-01-01T01:01:01Z\"",
                1 => "\"2022-01-01T01:01:01+03:00\"",
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_changes_when_set_to_null_types()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { NullableInt = 3 };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.NullableInt, null);
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        stopWatch.Stop();


        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().ContainSingle();
        var change = audit.Changes![0];
        change.Column.Should().Be(nameof(MyAuditableEntity.NullableInt));
        change.OldValue.Value.GetRawText().Should().Be("3");
        change.NewValue.HasValue.Should().BeFalse();

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_changes_when_set_from_null_types()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { NullableInt = null };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.NullableInt, 3);
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        stopWatch.Stop();


        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().ContainSingle();
        var change = audit.Changes![0];
        change.Column.Should().Be(nameof(MyAuditableEntity.NullableInt));
        change.OldValue.HasValue.Should().BeFalse();
        change.NewValue.Value.GetRawText().Should().Be("3");

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_updatedate_when_included()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntityWithUpdateDate { Name = null };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Name, "iamr8");
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        stopWatch.Stop();


        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().ContainSingle();

        entity.UpdateDate.Should().NotBeNull();
        entity.UpdateDate.Should().Be(audit.DateTime);

        var change = audit.Changes![0];
        change.Column.Should().Be(nameof(MyAuditableEntityWithUpdateDate.Name));
        change.OldValue.HasValue.Should().BeFalse();
        change.NewValue.Value.GetRawText().Should().Be("\"iamr8\"");

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_changes_of_list_types()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            ListOfStrings = new List<string> { "Foo", "Bar" },
            ArrayOfDoubles = new[] { 1.1, 2.2 },
            ListOfIntegers = new List<int> { 1, 2 }
        };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.ListOfStrings, new List<string> { "Foo", "Bar", "Baz" });
        members.Update(x => x.ArrayOfDoubles, new[] { 1.1, 2.2, 3.3 });
        members.Update(x => x.ListOfIntegers, new List<int> { 1, 2, 3 });
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        stopWatch.Stop();


        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().HaveCount(3);
        for (var i = 0; i < audit.Changes!.Length; i++)
        {
            var change = audit.Changes[i];
            switch (change.Column)
            {
                case nameof(MyAuditableEntity.ListOfStrings):
                    change.OldValue.Value.GetRawText().Should().Be(@"[""Foo"",""Bar""]");
                    change.NewValue.Value.GetRawText().Should().Be(@"[""Foo"",""Bar"",""Baz""]");
                    break;
                case nameof(MyAuditableEntity.ArrayOfDoubles):
                    change.OldValue.Value.GetRawText().Should().Be(@"[1.1,2.2]");
                    change.NewValue.Value.GetRawText().Should().Be(@"[1.1,2.2,3.3]");
                    break;
                case nameof(MyAuditableEntity.ListOfIntegers):
                    change.OldValue.Value.GetRawText().Should().Be(@"[1,2]");
                    change.NewValue.Value.GetRawText().Should().Be(@"[1,2,3]");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_updatedate_while_auditstorage_excluded()
    {
        var dbContext = CreateDbContext();

        var dt = DateTime.UtcNow.AddDays(-1);
        var options = new AuditProviderOptions
        {
            DateTimeProvider = _ => dt
        };
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntityWithoutAuditStorage
        {
            Name = "Foo",
        };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Name, "Bar");
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        stopWatch.Stop();

        entity.UpdateDate.Should().NotBeNull();
        entity.UpdateDate.Should().BeCloseTo(dt, TimeSpan.FromMilliseconds(100));

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_not_store_changes_when_excluded()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        options.AuditFlagSupport.Changed = AuditFlagState.Excluded;
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            ListOfStrings = new List<string> { "Foo", "Bar" },
            ArrayOfDoubles = new[] { 1.1, 2.2 },
            ListOfIntegers = new List<int> { 1, 2 }
        };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.ListOfStrings, new List<string> { "Foo", "Bar", "Baz" });
        members.Update(x => x.ArrayOfDoubles, new[] { 1.1, 2.2, 3.3 });
        members.Update(x => x.ListOfIntegers, new List<int> { 1, 2, 3 });
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        stopWatch.Stop();


        entity.Audits.Should().BeNull();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_changes_of_list_types_with_empty_values()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            ListOfStrings = new List<string>(),
            ArrayOfDoubles = Array.Empty<double>(),
            ListOfIntegers = new List<int>()
        };
        var members = entity.GetChangeTrackerMembers(dbContext);
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        stopWatch.Stop();


        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_changes_of_list_types_with_null_values()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            NullableListOfLongs = null
        };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.NullableListOfLongs, new List<long> { 1, 2, 3 });
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        stopWatch.Stop();

        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().HaveCount(1);
        for (var i = 0; i < audit.Changes!.Length; i++)
        {
            var change = audit.Changes[0];
            change.Column.Should().Be(nameof(MyAuditableEntity.NullableListOfLongs));
            change.OldValue.HasValue.Should().BeFalse();
            change.NewValue.Value.GetRawText().Should().Be("[1,2,3]");
        }

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_changes_of_double_type()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = _loggerFactory.CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            Double = 0
        };
        var members = entity.GetChangeTrackerMembers(dbContext);
        members.Update(x => x.Double, 5);
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        var stopWatch = Stopwatch.StartNew();
        await interceptor.AckAuditsAsync(creationEntry, dbContext);
        stopWatch.Stop();


        var audits = ((AuditCollection)entity.Audits.Value).Deserialize();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().HaveCount(1);
        for (var i = 0; i < audit.Changes!.Length; i++)
        {
            var change = audit.Changes[0];
            change.Column.Should().Be(nameof(MyAuditableEntity.Double));
            change.OldValue.Value.GetRawText().Should().Be("0");
            change.NewValue.Value.GetRawText().Should().Be("5");
        }

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }
}