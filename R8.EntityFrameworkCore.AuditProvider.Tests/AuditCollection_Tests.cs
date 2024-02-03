using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests;

public class AuditCollection_Tests
{
    public AuditCollection_Tests()
    {
        AuditProviderConfiguration.JsonOptions = new AuditProviderOptions().JsonOptions;
    }

    public class MockingEntity : IAuditActivator, IAuditStorage
    {
        public string Name { get; set; }
        public JsonElement? Audits { get; set; }
    }

    public static JsonElement GetJsonElement(string str)
    {
        return JsonSerializer.Deserialize<JsonElement>(str, AuditProviderConfiguration.JsonOptions);
    }

    [Fact]
    public void should_return_AuditCollection()
    {
        var audits = new Audit[]
        {
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Created,
            },
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Changed,
                Changes = new[]
                {
                    new AuditChange(nameof(MockingEntity.Name), null, GetJsonElement("\"Foo\""))
                }
            },
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Deleted,
            }
        };
        var entity = new MockingEntity { Audits = JsonSerializer.SerializeToElement(audits, AuditProviderConfiguration.JsonOptions) };

        var obj = entity.GetAuditCollection();
        obj.Should().NotBeNullOrEmpty();
        obj.Count.Should().Be(3);

        obj[0].Flag.Should().Be(AuditFlag.Created);
        obj[0].DateTime.Should().Be(audits[0].DateTime);

        obj[1].Flag.Should().Be(AuditFlag.Changed);
        obj[1].DateTime.Should().Be(audits[1].DateTime);
        obj[1].Changes.Should().NotBeNullOrEmpty();
        obj[1].Changes.Length.Should().Be(1);
        obj[1].Changes[0].Column.Should().Be(audits[1].Changes[0].Column);
        obj[1].Changes[0].OldValue.Should().Be(audits[1].Changes[0].OldValue);
        obj[1].Changes[0].NewValue.Value.GetRawText().Should().Be(audits[1].Changes[0].NewValue.Value.GetRawText());

        obj[2].Flag.Should().Be(AuditFlag.Deleted);
        obj[2].DateTime.Should().Be(audits[2].DateTime);
    }

    [Fact]
    public void should_not_return_created_when_no_audit_provided()
    {
        var audits = Array.Empty<Audit>();
        var entity = new MockingEntity { Audits = JsonSerializer.SerializeToElement(audits, AuditProviderConfiguration.JsonOptions) };

        var obj = entity.GetAuditCollection();
        obj.Should().NotBeNull();
        obj.Should().BeEmpty();
        var first = obj.First();
        first.Should().BeNull();
    }

    [Fact]
    public void should_not_return_created_when_no_Created_provided()
    {
        var audits = new Audit[]
        {
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Changed,
                Changes = new[]
                {
                    new AuditChange(nameof(MockingEntity.Name), null, GetJsonElement("\"Foo\""))
                }
            },
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Deleted,
            }
        };
        var entity = new MockingEntity { Audits = JsonSerializer.SerializeToElement(audits, AuditProviderConfiguration.JsonOptions) };

        var obj = entity.GetAuditCollection();
        obj.Should().NotBeNullOrEmpty();
        var first = obj.First();
        first.Should().BeNull();
    }

    [Fact]
    public void should_return_created_flag()
    {
        var audits = new Audit[]
        {
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Created,
            },
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Changed,
            },
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Deleted,
            }
        };
        var entity = new MockingEntity { Audits = JsonSerializer.SerializeToElement(audits, AuditProviderConfiguration.JsonOptions) };

        var obj = entity.GetAuditCollection();
        obj.Should().NotBeNullOrEmpty();
        var first = obj.First();
        first.Should().NotBeNull();

        first.Value.Changes.Should().BeNullOrEmpty();
        first.Value.Flag.Should().Be(AuditFlag.Created);
    }

    [Fact]
    public void should_return_first_created_flag_when_have_two()
    {
        var audits = new Audit[]
        {
            new Audit
            {
                DateTime = DateTime.UtcNow.AddSeconds(-1),
                Flag = AuditFlag.Created,
            },
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Created,
            },
        };
        var entity = new MockingEntity { Audits = JsonSerializer.SerializeToElement(audits, AuditProviderConfiguration.JsonOptions) };

        var obj = entity.GetAuditCollection();
        obj.Should().NotBeNullOrEmpty();
        var first = obj.First();
        first.Should().NotBeNull();

        first.Value.Changes.Should().BeNullOrEmpty();
        first.Value.Flag.Should().Be(AuditFlag.Created);
        first.Value.DateTime.Should().Be(audits[0].DateTime);
    }

    [Fact]
    public void should_return_last_Changed_audit()
    {
        var audits = new Audit[]
        {
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Created,
            },
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Changed,
                Changes = new[]
                {
                    new AuditChange(nameof(MockingEntity.Name), null, GetJsonElement("\"Foo\""))
                }
            },
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Deleted,
            }
        };
        var entity = new MockingEntity { Audits = JsonSerializer.SerializeToElement(audits, AuditProviderConfiguration.JsonOptions) };

        var obj = entity.GetAuditCollection();
        obj.Should().NotBeNullOrEmpty();
        var last = obj.Last(false);
        last.Should().NotBeNull();

        last.Value.Changes.Should().NotBeNullOrEmpty();
        last.Value.Flag.Should().Be(AuditFlag.Changed);
        last.Value.DateTime.Should().Be(audits[1].DateTime);
        last.Value.Changes.Should().NotBeNullOrEmpty();
        last.Value.Changes.Length.Should().Be(1);
        last.Value.Changes[0].Column.Should().Be(audits[1].Changes[0].Column);
        last.Value.Changes[0].OldValue.Should().Be(audits[1].Changes[0].OldValue);
        last.Value.Changes[0].NewValue.Value.GetRawText().Should().Be(audits[1].Changes[0].NewValue.Value.GetRawText());
    }

    [Fact]
    public void should_return_last_audit()
    {
        var audits = new Audit[]
        {
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Created,
            },
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Changed,
            },
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Deleted,
            }
        };
        var entity = new MockingEntity { Audits = JsonSerializer.SerializeToElement(audits, AuditProviderConfiguration.JsonOptions) };

        var obj = entity.GetAuditCollection();
        obj.Should().NotBeNullOrEmpty();
        var last = obj.Last(true);
        last.Should().NotBeNull();

        last.Value.Changes.Should().BeNullOrEmpty();
        last.Value.Flag.Should().Be(AuditFlag.Deleted);
        last.Value.DateTime.Should().Be(audits[2].DateTime);
    }

    [Fact]
    public void should_return_last_audit_when_no_audit_provided()
    {
        var audits = Array.Empty<Audit>();
        var entity = new MockingEntity { Audits = JsonSerializer.SerializeToElement(audits, AuditProviderConfiguration.JsonOptions) };

        var obj = entity.GetAuditCollection();
        obj.Should().NotBeNull();
        obj.Should().BeEmpty();
        var last = obj.Last(true);
        last.Should().BeNull();
    }

    [Fact]
    public void should_track_changes_of_property()
    {
        var audits = new Audit[]
        {
            new Audit
            {
                DateTime = DateTime.UtcNow.AddDays(-2),
                Flag = AuditFlag.Created,
            },
            new Audit
            {
                DateTime = DateTime.UtcNow.AddDays(-1),
                Flag = AuditFlag.Changed,
                Changes = new[]
                {
                    new AuditChange(nameof(MockingEntity.Name), null, GetJsonElement("\"Foo\""))
                }
            },
            new Audit
            {
                DateTime = DateTime.UtcNow,
                Flag = AuditFlag.Changed,
                Changes = new[]
                {
                    new AuditChange(nameof(MockingEntity.Name), GetJsonElement("\"Foo\""), GetJsonElement("\"Bar\""))
                }
            }
        };
        var entity = new MockingEntity { Audits = JsonSerializer.SerializeToElement(audits, AuditProviderConfiguration.JsonOptions) };

        var obj = entity.GetAuditCollection();
        obj.Should().NotBeNullOrEmpty();
        var changes = obj.Track(nameof(MockingEntity.Name));
        changes.Should().NotBeNullOrEmpty();

        changes[0].DateTime.Should().Be(audits[1].DateTime);
        changes[0].Flag.Should().Be(AuditFlag.Changed);
        changes[0].Changes.Should().NotBeNullOrEmpty();
        changes[0].Changes.Length.Should().Be(1);
        changes[0].Changes[0].Column.Should().Be(audits[1].Changes[0].Column);
        changes[0].Changes[0].OldValue.Should().Be(audits[1].Changes[0].OldValue);
        changes[0].Changes[0].NewValue.Value.GetRawText().Should().Be(audits[1].Changes[0].NewValue.Value.GetRawText());

        changes[1].DateTime.Should().Be(audits[2].DateTime);
        changes[1].Flag.Should().Be(AuditFlag.Changed);
        changes[1].Changes.Should().NotBeNullOrEmpty();
        changes[1].Changes.Length.Should().Be(1);
        changes[1].Changes[0].Column.Should().Be(audits[2].Changes[0].Column);
        changes[1].Changes[0].OldValue.Value.GetRawText().Should().Be(audits[2].Changes[0].OldValue.Value.GetRawText());
        changes[1].Changes[0].NewValue.Value.GetRawText().Should().Be(audits[2].Changes[0].NewValue.Value.GetRawText());
    }

    [Fact]
    public void should_not_return_tracked_changes_of_property_when_property_not_changes()
    {
        var audits = Array.Empty<Audit>();
        var entity = new MockingEntity { Audits = JsonSerializer.SerializeToElement(audits, AuditProviderConfiguration.JsonOptions) };

        var obj = entity.GetAuditCollection();
        obj.Should().NotBeNull();
        obj.Should().BeEmpty();
        var changes = obj.Track(nameof(MockingEntity.Name));
        changes.Should().NotBeNull();
        changes.Should().BeEmpty();
    }

    [Fact]
    public void should_support_legacy_format()
    {
        var json = @"[
  {
    ""f"": 0,
    ""dt"": ""2023-09-25T12:00:00.0000000+03:30""
  },
  {
    ""f"": 1,
    ""dt"": ""2023-09-25T12:00:00.0000000+03:30"",
    ""c"": [
      {
        ""n"": ""Name"",
        ""_v"": ""OldName"",
        ""v"": ""NewName""
      },
      {
        ""n"": ""Age"",
        ""_v"": null,
        ""v"": ""33""
      },
      {
        ""n"": ""Age"",
        ""_v"": 33,
        ""v"": 0
      }
    ],
    ""u"": {
      ""id"": ""1"",
      ""ad"": {
        ""Username"": ""Foo""
      }
    }
  },
  {
    ""f"": 2,
    ""dt"": ""2023-09-25T12:00:00.0000000+03:30""
  },
  {
    ""f"": 3,
    ""dt"": ""2023-09-25T12:00:00.0000000+03:30""
  }
]";
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json, AuditProviderConfiguration.JsonOptions);
        var obj = jsonElement.Deserialize<Audit[]>(AuditProviderConfiguration.JsonOptions);
        var collection = new AuditCollection(obj);
        collection.Should().NotBeNull();
        
        collection.Count.Should().Be(4);
        collection[0].Flag.Should().Be(AuditFlag.Created);
        collection[0].DateTime.Should().Be(DateTime.Parse("2023-09-25T12:00:00.0000000+03:30"));
        
        collection[1].Flag.Should().Be(AuditFlag.Changed);
        collection[1].DateTime.Should().Be(DateTime.Parse("2023-09-25T12:00:00.0000000+03:30"));
        collection[1].Changes.Should().NotBeNullOrEmpty();
        collection[1].Changes.Length.Should().Be(3);
        collection[1].Changes[0].Column.Should().Be("Name");
        collection[1].Changes[0].OldValue.Value.GetRawText().Should().Be("\"OldName\"");
        collection[1].Changes[0].NewValue.Value.GetRawText().Should().Be("\"NewName\"");
        collection[1].Changes[1].Column.Should().Be("Age");
        collection[1].Changes[1].OldValue.Should().BeNull();
        collection[1].Changes[1].NewValue.Value.GetRawText().Should().Be("\"33\"");
        collection[1].Changes[2].Column.Should().Be("Age");
        collection[1].Changes[2].OldValue.Value.GetInt32().Should().Be(33);
        collection[1].Changes[2].NewValue.Value.GetInt32().Should().Be(0);
        
        collection[2].Flag.Should().Be(AuditFlag.Deleted);
        collection[2].DateTime.Should().Be(DateTime.Parse("2023-09-25T12:00:00.0000000+03:30"));
        
        collection[3].Flag.Should().Be(AuditFlag.UnDeleted);
        collection[3].DateTime.Should().Be(DateTime.Parse("2023-09-25T12:00:00.0000000+03:30"));
    }
}