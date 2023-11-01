using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;
using Xunit.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests;

public class Audit_UnitTests
{
    private readonly ITestOutputHelper _outputHelper;

    public Audit_UnitTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        AuditStatic.JsonStaticOptions = new EntityFrameworkAuditProviderOptions().JsonOptions;
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
        public IEnumerable<PropertyEntry> Members { get; }
        public Type EntityType { get; }

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

        var entity = new MyAuditableEntity();
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

        var entity = new MyAuditableEntity { Name = "Foo" };
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
        lastAudit.User.Value.AdditionalData.Should().ContainKey("Username");
        lastAudit.User.Value.AdditionalData["Username"].Should().Be("Foo");

        var lastChange = lastAudit.Changes[0];
        lastChange.Key.Should().Be(nameof(MyAuditableEntity.Name));
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

        var entity = new MyAuditableEntity { Name = "Foo" };
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

        var entity = new MyAuditableEntity { Name = "Foo" };

        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Added, entity, creationMembers);
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        success.Should().BeTrue();

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeTrue();

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
        firstAudit.Changes[0].Key.Should().Be(nameof(MyAuditableEntity.Name));
        firstAudit.Changes[0].OldValue.Should().Be("Foo");
        firstAudit.Changes[0].NewValue.Should().Be("Bar");

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
    }

    [Fact]
    public async Task should_throw_exception_when_using_modification_and_deletion_simultaneously()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

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
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

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
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new MyAuditableEntity { Name = "Foo" };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeFalse();
    }

    [Fact]
    public async Task should_not_store_anything_when_not_changes_made2()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new MyAuditableEntity { Name = "Foo" };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Foo") };
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

        var entity = new MyAuditableEntity { LastName = "Foo" };

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

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().HaveCount(1);

        var firstAudit = audits[0];
        firstAudit.Flag.Should().Be(AuditFlag.Changed);
        firstAudit.Changes.Should().NotBeNullOrEmpty();
        firstAudit.Changes.Should().ContainSingle();
        firstAudit.Changes[0].Key.Should().Be(nameof(MyAuditableEntity.Name));
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
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new MyEntity();

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntry(entity, x => x.Name) };
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Deleted, entity, modificationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeFalse();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task should_not_store_audit_changes_when_entity_is_not_auditable_on_deletion()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new MyEntity { Name = "Foo" };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        var modificationEntry = new Audit_UnitTests.MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeFalse();

        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task should_store_composite_changed_then_changed_again()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new MyAuditableEntity { Name = "Foo" };

        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Bar") };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        success.Should().BeTrue();

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Name, "Foo") };
        var modificationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, modificationMembers);
        success = await interceptor.StoringAuditAsync(modificationEntry, dbContext);
        success.Should().BeTrue();

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().HaveCount(2);

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Changed);
        lastAudit.Changes.Should().NotBeNullOrEmpty();
        lastAudit.Changes.Should().ContainSingle();
        lastAudit.Changes[0].Key.Should().Be(nameof(MyAuditableEntity.Name));
        lastAudit.Changes[0].OldValue.Should().Be("Foo");
        lastAudit.Changes[0].NewValue.Should().Be("Bar");

        var firstAudit = audits[1];
        firstAudit.Flag.Should().Be(AuditFlag.Changed);
        firstAudit.Changes.Should().NotBeNullOrEmpty();
        firstAudit.Changes.Should().ContainSingle();
        firstAudit.Changes[0].Key.Should().Be(nameof(MyAuditableEntity.Name));
        firstAudit.Changes[0].OldValue.Should().Be("Bar");
        firstAudit.Changes[0].NewValue.Should().Be("Foo");

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
    }

    [Fact]
    public async Task should_store_changes_of_unknown_types_by_interceptor()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new MyAuditableEntity { Payload = JsonDocument.Parse(@"[{""name"": ""arash""}]") };

        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Payload, JsonDocument.Parse(@"[{""name"": ""abood""}]")) };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeTrue();

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var lastAudit = audits[0];
        lastAudit.Flag.Should().Be(AuditFlag.Changed);
        lastAudit.Changes.Should().NotBeNullOrEmpty();
        lastAudit.Changes.Should().ContainSingle();
        lastAudit.Changes[0].Key.Should().Be(nameof(MyAuditableEntity.Payload));
        lastAudit.Changes[0].OldValue.Should().Be(@"[{""name"":""arash""}]");
        lastAudit.Changes[0].NewValue.Should().Be(@"[{""name"":""abood""}]");

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task should_store_changes_of_datetime_types()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

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

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().HaveCount(2);
        for (var i = 0; i < audit.Changes!.Length; i++)
        {
            var change = audit.Changes[i];
            change.Key.Should().Be(i switch
            {
                0 => nameof(MyAuditableEntity.Date),
                1 => nameof(MyAuditableEntity.DateOffset),
                _ => throw new ArgumentOutOfRangeException()
            });
            change.OldValue.Should().Be(i switch
            {
                0 => @"2021-01-01T01:01:01Z",
                1 => @"2021-01-01T01:01:01+03:00",
                _ => throw new ArgumentOutOfRangeException()
            });
            change.NewValue.Should().Be(i switch
            {
                0 => @"2022-01-01T01:01:01Z",
                1 => @"2022-01-01T01:01:01+03:00",
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }
    
    [Fact]
    public async Task should_store_changes_when_set_to_null_types()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new MyAuditableEntity { NullableInt = 3 };

        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.NullableInt, null) };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeTrue();

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().ContainSingle();
        var change = audit.Changes![0];
        change.Key.Should().Be(nameof(MyAuditableEntity.NullableInt));
        change.OldValue.Should().Be("3");
        change.NewValue.Should().BeNull();

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }
    
    [Fact]
    public async Task should_store_changes_when_set_from_null_types()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new MyAuditableEntity { NullableInt = null };

        var creationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.NullableInt, 3) };
        var creationEntry = new MockingAuditEntityEntry(EntityState.Modified, entity, creationMembers);
        var stopWatch = Stopwatch.StartNew();
        var success = await interceptor.StoringAuditAsync(creationEntry, dbContext);
        stopWatch.Stop();
        success.Should().BeTrue();

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().ContainSingle();
        var change = audit.Changes![0];
        change.Key.Should().Be(nameof(MyAuditableEntity.NullableInt));
        change.OldValue.Should().BeNull();
        change.NewValue.Should().Be("3");

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }
    
    [Fact]
    public async Task should_store_changes_of_list_types()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

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

        var audits = entity.GetAudits();
        audits.Should().NotBeNull();
        audits.Should().ContainSingle();

        var audit = audits[0];
        audit.Flag.Should().Be(AuditFlag.Changed);
        audit.Changes.Should().NotBeNullOrEmpty();
        audit.Changes.Should().HaveCount(3);
        for (var i = 0; i < audit.Changes!.Length; i++)
        {
            var change = audit.Changes[i];
            change.Key.Should().Be(i switch
            {
                0 => nameof(MyAuditableEntity.ListOfStrings),
                1 => nameof(MyAuditableEntity.ArrayOfDoubles),
                2 => nameof(MyAuditableEntity.ListOfIntegers),
                _ => throw new ArgumentOutOfRangeException()
            });
            change.OldValue.Should().Be(i switch
            {
                0 => @"[""Foo"",""Bar""]",
                1 => @"[1.1,2.2]",
                2 => @"[1,2]",
                _ => throw new ArgumentOutOfRangeException()
            });
            change.NewValue.Should().Be(i switch
            {
                0 => @"[""Foo"",""Bar"",""Baz""]",
                1 => @"[1.1,2.2,3.3]",
                2 => @"[1,2,3]",
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }
}