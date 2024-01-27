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
using Xunit.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests;

public class Audit_UnitTests
{
    private readonly ITestOutputHelper _outputHelper;

    public Audit_UnitTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        AuditProviderConfiguration.JsonOptions = new AuditProviderOptions().JsonOptions;
    }

    public class MockingAuditEntityEntry : IEntityEntry
    {
        public MockingAuditEntityEntry(EntityState state, object entity, IEnumerable<PropertyEntry> members)
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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity();
        var entry = new MockingAuditEntityEntry(state, entity, Array.Empty<PropertyEntry>());

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeFalse();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_return_creation_audit_when_GetCreated_called()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

        var creationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar"),
        };
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

        var creationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar"),
        };
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

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
        options.IncludedFlags.Remove(AuditFlag.Created);
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeFalse();

        var creationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar"),
        };
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

        var creationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar"),
        };
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

        members = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

        members = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.IsDeleted, true) };
        entry = new MockingAuditEntityEntry(EntityState.Deleted, entity, members);
        success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

        members = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);
        success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

        members = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.IsDeleted, true) };
        entry = new MockingAuditEntityEntry(EntityState.Deleted, entity, members);
        success = await interceptor.StoringAuditAsync(entry, dbContext);
        success.Should().BeTrue();

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
        options.IncludedFlags.Remove(AuditFlag.Created);
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeFalse();

        entity.Audits.Should().BeNull();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_creation()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntry(entity, x => x.Name),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.IsDeleted, true)
        };
        var entry = new MockingAuditEntityEntry(EntityState.Deleted, entity, members);

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeTrue();

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
    public async Task should_not_store_deletion_when_excluded()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        options.IncludedFlags.Remove(AuditFlag.Deleted);
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntry(entity, x => x.Name),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.IsDeleted, true)
        };
        var entry = new MockingAuditEntityEntry(EntityState.Deleted, entity, members);

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeFalse();

        entity.Audits.Should().BeNull();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_undeletion()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo", IsDeleted = true };
        var members = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntry(entity, x => x.Name),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.IsDeleted, false)
        };
        var entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeTrue();

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
        options.IncludedFlags.Remove(AuditFlag.UnDeleted);
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo", IsDeleted = true };
        var members = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntry(entity, x => x.Name),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.IsDeleted, false)
        };
        var entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeFalse();

        entity.Audits.Should().BeNull();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_composite_creation_then_changed()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };

        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Added, entity, creationMembers);
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        success.Should().BeTrue();

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };

        var modificationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar"),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.IsDeleted, true)
        };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        await Assert.ThrowsAsync<NotSupportedException>(async () => await interceptor.StoringAuditAsync(modificationEntry, dbContext));
    }

    [Fact]
    public async Task should_throw_exception_when_using_modification_and_undeletion_simultaneously()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo", IsDeleted = true };

        var modificationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar"),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.IsDeleted, false)
        };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        await Assert.ThrowsAsync<NotSupportedException>(async () => await interceptor.StoringAuditAsync(modificationEntry, dbContext));
    }

    [Fact]
    public async Task should_not_store_anything_when_not_changes_made()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeFalse();
    }

    [Fact]
    public async Task should_not_store_anything_when_not_changes_made2()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Foo") };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeFalse();
    }

    [Fact]
    public async Task should_not_store_properties_with_ignored_attribute()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { LastName = "Foo" };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.LastName, "Bar") };
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeFalse();
    }

    [Fact]
    public async Task should_store_properties_without_ignored_attribute()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo", LastName = "Foo" };

        var modificationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar"),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.LastName, "Bar")
        };
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeTrue();

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
    public async Task should_delete_permanently_when_entity_is_not_auditable_on_deletion()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyEntity();

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Deleted, entity, modificationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeFalse();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_not_store_deletion_state_when_not_implements_IAuditableDelete_even_implements_IAuditable()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntityWithoutSoftDelete();

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Deleted, entity, modificationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeFalse();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_not_store_audit_changes_when_entity_is_not_auditable_on_deletion()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyEntity { Name = "Foo" };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeFalse();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_composite_changed_then_changed_again()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Name = "Foo" };

        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        success.Should().BeTrue();

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Foo") };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { Payload = JsonDocument.Parse(@"[{""name"": ""arash""}]") };

        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Payload, JsonDocument.Parse(@"[{""name"": ""abood""}]")) };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            Date = new DateTime(2021, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            DateOffset = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.FromHours(3))
        };

        var creationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.Date, new DateTime(2022, 1, 1, 1, 1, 1, DateTimeKind.Utc)),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.DateOffset, new DateTimeOffset(2022, 1, 1, 1, 1, 1, TimeSpan.FromHours(3)))
        };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { NullableInt = 3 };

        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.NullableInt, null) };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity { NullableInt = null };

        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.NullableInt, 3) };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeTrue();

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
    public async Task should_store_changes_of_list_types()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            ListOfStrings = new List<string> { "Foo", "Bar" },
            ArrayOfDoubles = new[] { 1.1, 2.2 },
            ListOfIntegers = new List<int> { 1, 2 }
        };

        var creationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.ListOfStrings, new List<string> { "Foo", "Bar", "Baz" }),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.ArrayOfDoubles, new[] { 1.1, 2.2, 3.3 }),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.ListOfIntegers, new List<int> { 1, 2, 3 })
        };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeTrue();

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
            change.Column.Should().Be(i switch
            {
                0 => nameof(MyAuditableEntity.ListOfStrings),
                1 => nameof(MyAuditableEntity.ArrayOfDoubles),
                2 => nameof(MyAuditableEntity.ListOfIntegers),
                _ => throw new ArgumentOutOfRangeException()
            });
            change.OldValue.Value.GetRawText().Should().Be(i switch
            {
                0 => @"[""Foo"",""Bar""]",
                1 => @"[1.1,2.2]",
                2 => @"[1,2]",
                _ => throw new ArgumentOutOfRangeException()
            });
            change.NewValue.Value.GetRawText().Should().Be(i switch
            {
                0 => @"[""Foo"",""Bar"",""Baz""]",
                1 => @"[1.1,2.2,3.3]",
                2 => @"[1,2,3]",
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        _outputHelper.WriteLine(entity.Audits.Value.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_not_store_changes_when_excluded()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        options.IncludedFlags.Remove(AuditFlag.Changed);
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            ListOfStrings = new List<string> { "Foo", "Bar" },
            ArrayOfDoubles = new[] { 1.1, 2.2 },
            ListOfIntegers = new List<int> { 1, 2 }
        };

        var creationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.ListOfStrings, new List<string> { "Foo", "Bar", "Baz" }),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.ArrayOfDoubles, new[] { 1.1, 2.2, 3.3 }),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.ListOfIntegers, new List<int> { 1, 2, 3 })
        };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeFalse();

        entity.Audits.Should().BeNull();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_changes_of_list_types_with_empty_values()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            ListOfStrings = new List<string>(),
            ArrayOfDoubles = Array.Empty<double>(),
            ListOfIntegers = new List<int>()
        };

        var creationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntry(entity, x => x.ListOfStrings),
            dbContext.GetPropertyEntry(entity, x => x.ArrayOfDoubles),
            dbContext.GetPropertyEntry(entity, x => x.ListOfIntegers)
        };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeFalse();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms -or- {stopWatch.Elapsed.TotalMicroseconds()}μs");
    }

    [Fact]
    public async Task should_store_changes_of_list_types_with_null_values()
    {
        var dbContext = CreateDbContext();

        var options = new AuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            NullableListOfLongs = null
        };

        var creationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.NullableListOfLongs, new List<long> { 1, 2, 3 })
        };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeTrue();

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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, CreateServiceProvider(), logger);

        var entity = new MyAuditableEntity
        {
            Double = 0
        };

        var creationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.Double, 5)
        };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeTrue();

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