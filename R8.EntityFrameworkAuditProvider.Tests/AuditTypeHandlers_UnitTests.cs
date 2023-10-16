using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using R8.EntityFrameworkAuditProvider.Converters;
using R8.EntityFrameworkAuditProvider.Tests.Entities;
using Xunit.Abstractions;

namespace R8.EntityFrameworkAuditProvider.Tests;

public class AuditTypeHandlers_UnitTests
{
    private readonly ITestOutputHelper _outputHelper;

    public AuditTypeHandlers_UnitTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        EntityFrameworkAuditProviderOptions.JsonStaticOptions = new EntityFrameworkAuditProviderOptions().JsonOptions;
    }

    [Fact]
    public async Task should_store_changed_according_to_list_handler()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        options.TypeHandlers.Add(new AuditListHandler());
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var entity = new SecondAuditableEntity { ListOfStrings = new List<string> { "Foo" } };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.ListOfStrings, new List<string> { "Bar" }) };
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
        firstAudit.Changes[0].Key.Should().Be(nameof(SecondAuditableEntity.ListOfStrings));
        firstAudit.Changes[0].OldValue.Should().Be("[\"Foo\"]");
        firstAudit.Changes[0].NewValue.Should().Be("[\"Bar\"]");

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }
    
    [Fact]
    public async Task should_store_changed_according_to_datetime_handler()
    {
        var dbContext = new DummyDbContextFactory().CreateDbContext(Array.Empty<string>());

        var options = new EntityFrameworkAuditProviderOptions();
        options.TypeHandlers.Add(new AuditDateTimeHandler());
        var logger = new LoggerFactory().CreateLogger<EntityFrameworkAuditProviderInterceptor>();
        var interceptor = new EntityFrameworkAuditProviderInterceptor(options, logger);

        var dt = DateTime.UtcNow;
        var dt2 = dt.AddSeconds(1);
        var entity = new SecondAuditableEntity { Date = dt };

        var modificationMembers = new List<PropertyEntry> { dbContext.GetPropertyEntryWithNewValue(entity, x => x.Date, dt2) };
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
        firstAudit.Changes[0].Key.Should().Be(nameof(SecondAuditableEntity.Date));
        firstAudit.Changes[0].OldValue.Should().Be(dt.ToString());
        firstAudit.Changes[0].NewValue.Should().Be(dt2.ToString());

        _outputHelper.WriteLine(entity.Audits.RootElement.GetRawText());
        _outputHelper.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
    }
}