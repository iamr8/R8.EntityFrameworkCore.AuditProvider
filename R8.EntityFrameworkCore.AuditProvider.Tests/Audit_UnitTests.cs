using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using R8.EntityFrameworkCore.AuditProvider.Converters;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;
using Xunit.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests;

public class Audit_UnitTests
{
    private readonly ITestOutputHelper _outputHelper;

    public Audit_UnitTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        EntityFrameworkAuditProviderOptions.JsonStaticOptions = new EntityFrameworkAuditProviderOptions().JsonOptions;
    }

    public class MockingAuditEntityEntry : IEntityEntry
    {
        public MockingAuditEntityEntry(EntityState state, object entity, IEnumerable<PropertyEntry> members)
        {
            State = state;
            Entity = entity;
            Members = members;
        }

        public EntityState State { get; set; }
        public object Entity { get; }
        public IEnumerable<PropertyEntry> Members { get; }

        public void DetectChanges()
        {
        }

        public Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(EntityState.Detached)]
    [InlineData(EntityState.Unchanged)]
    public async Task should_ignore_auditing_on_ignored_state(EntityState state)
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity();
        var entry = new MockingAuditEntityEntry(state, entity, Array.Empty<PropertyEntry>());

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeFalse();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task should_find_changes_including_stacktrace(bool includeStackTrace)
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions
        {
            IncludeStackTrace = includeStackTrace,
            UserProvider = sp => new EntityFrameworkAuditUser("1", new Dictionary<string, string>
            {
                { "Username", "Foo" }
            })
        };
        options.ExcludedNamespacesInStackTrace.Add("Xunit");
        options.ExcludedNamespacesInStackTrace.Add("JetBrains");

        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        var entry = new MockingAuditEntityEntry(EntityState.Modified, entity, members);

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeTrue();

        entity.Audits.Should().NotBeNull();
        entity.Audits.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        if (includeStackTrace)
        {
            lastAudit.StackTrace.Should().NotBeNullOrEmpty();
        }
        else
        {
            lastAudit.StackTrace.Should().BeNullOrEmpty();
        }

        lastAudit.Flag.Should().Be(AuditFlag.Changed);
        lastAudit.Changes.Should().NotBeNullOrEmpty();
        lastAudit.Changes.Should().ContainSingle();
        lastAudit.User.HasValue.Should().BeTrue();
        lastAudit.User.Value.UserId.Should().Be("1");
        lastAudit.User.Value.AdditionalData.Should().NotBeNullOrEmpty();
        lastAudit.User.Value.AdditionalData.Should().ContainKey("username");
        lastAudit.User.Value.AdditionalData["username"].Should().Be("Foo");

        var lastChange = lastAudit.Changes[0];
        lastChange.Key.Should().Be(nameof(FirstAuditableEntity.Name));
        lastChange.OldValue.Should().Be("Foo");
        lastChange.NewValue.Should().Be("Bar");

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task should_store_creation()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity { Name = "Foo" };
        var members = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var entry = new MockingAuditEntityEntry(EntityState.Added, entity, members);

        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(entry, dbContext);
        stopWatch.Stop();

        success.Should().BeTrue();

        entity.Audits.Should().NotBeNull();
        entity.Audits.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Created);
        lastAudit.Changes.Should().BeNullOrEmpty();

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task should_store_deletion()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity { Name = "Foo" };
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
        entity.Audits.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Deleted);
        lastAudit.Changes.Should().BeNullOrEmpty();

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task should_store_undeletion()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity { Name = "Foo", IsDeleted = true };
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
        entity.Audits.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.UnDeleted);
        lastAudit.Changes.Should().BeNullOrEmpty();

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task should_store_composite_creation_then_changed()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity { Name = "Foo" };

        var stopWatch = Stopwatch.StartNew();
        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Added, entity, creationMembers);
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        success.Should().BeTrue();

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeTrue();
        stopWatch.Stop();

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().HaveCount(2);

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Created);
        lastAudit.Changes.Should().BeNullOrEmpty();

        var firstAudit = audits[1];
        firstAudit.Flag.Should().Be(AuditFlag.Changed);
        firstAudit.Changes.Should().NotBeNullOrEmpty();
        firstAudit.Changes.Should().ContainSingle();
        firstAudit.Changes[0].Key.Should().Be(nameof(FirstAuditableEntity.Name));
        firstAudit.Changes[0].OldValue.Should().Be("Foo");
        firstAudit.Changes[0].NewValue.Should().Be("Bar");

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task should_throw_exception_when_using_modification_and_deletion_simultaneously()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity { Name = "Foo" };

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
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity { Name = "Foo", IsDeleted = true };

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
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity { Name = "Foo" };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeFalse();
    }
    
    [Fact]
    public async Task should_not_store_properties_with_ignored_attribute()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity { LastName = "Foo" };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.LastName, "Bar") };
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeFalse();
    }
    
    [Fact]
    public async Task should_store_properties_without_ignored_attribute()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity { Name = "Foo", LastName = "Foo" };

        var modificationMembers = new List<PropertyEntry>
        {
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar"),
            dbContext.GetPropertyEntryWithNewValue(entity, x => x.LastName, "Bar")
        };
        var stopWatch = Stopwatch.StartNew();
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeTrue();
        stopWatch.Stop();

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().HaveCount(1);

        var firstAudit = audits[0];
        firstAudit.Flag.Should().Be(AuditFlag.Changed);
        firstAudit.Changes.Should().NotBeNullOrEmpty();
        firstAudit.Changes.Should().ContainSingle();
        firstAudit.Changes[0].Key.Should().Be(nameof(FirstAuditableEntity.Name));
        firstAudit.Changes[0].OldValue.Should().Be("Foo");
        firstAudit.Changes[0].NewValue.Should().Be("Bar");

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }
    
    [Fact]
    public async Task should_delete_permanently_when_entity_is_not_auditable_on_deletion()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        options.TypeHandlers.Add(new AuditDateTimeHandler());
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new ThirdEntity();

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var stopWatch = Stopwatch.StartNew();
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Deleted, entity, modificationMembers);
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeFalse();
        stopWatch.Stop();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }
    
    [Fact]
    public async Task should_not_store_audit_changes_when_entity_is_not_auditable_on_deletion()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        options.TypeHandlers.Add(new AuditDateTimeHandler());
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new ThirdEntity { Name = "Foo" };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        var stopWatch = Stopwatch.StartNew();
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeFalse();
        stopWatch.Stop();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }
    
    [Fact]
    public async Task should_store_composite_changed_then_changed_again()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new FirstAuditableEntity { Name = "Foo" };

        var stopWatch = Stopwatch.StartNew();
        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        success.Should().BeTrue();

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Foo") };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeTrue();
        stopWatch.Stop();

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().HaveCount(2);

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Changed);
        lastAudit.Changes.Should().NotBeNullOrEmpty();
        lastAudit.Changes.Should().ContainSingle();
        lastAudit.Changes[0].Key.Should().Be(nameof(FirstAuditableEntity.Name));
        lastAudit.Changes[0].OldValue.Should().Be("Foo");
        lastAudit.Changes[0].NewValue.Should().Be("Bar");

        var firstAudit = audits[1];
        firstAudit.Flag.Should().Be(AuditFlag.Changed);
        firstAudit.Changes.Should().NotBeNullOrEmpty();
        firstAudit.Changes.Should().ContainSingle();
        firstAudit.Changes[0].Key.Should().Be(nameof(FirstAuditableEntity.Name));
        firstAudit.Changes[0].OldValue.Should().Be("Bar");
        firstAudit.Changes[0].NewValue.Should().Be("Foo");

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }
}