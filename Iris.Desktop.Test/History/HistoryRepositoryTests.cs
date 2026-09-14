using FluentAssertions;
using Iris.Desktop.History;
using Iris.Desktop.Infrastructure;
using Iris.History;
using Microsoft.Extensions.Logging.Abstractions;

namespace Iris.Desktop.Test.History;

public class HistoryRepositoryTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly HistorySettings _settings = new();

    private HistoryRepository Repository() =>
        new(_db.Context, _settings, NullLogger<HistoryRepository>.Instance);

    private static HistoryRecord RecordAt(DateTimeOffset when, string target) =>
        new("MessageSent", "Local")
        {
            Action = "MessageSent",
            EventAction = "SendMessage",
            Details = "{}",
            Target = target,
            Timestamp = when,
            Source = "Local",
        };

    private void Seed(HistoryRepository repository, int count)
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < count; i++)
            repository.AddHistoryRecord(RecordAt(start.AddMinutes(i), $"queue-{i}"));
    }

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "A recorded message survives the round trip to disk")]
    public void Round_trips_a_record()
    {
        var repository = Repository();
        var when = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        repository.AddHistoryRecord(RecordAt(when, "orders"));

        var read = repository.GetHistoryRecords().Should().ContainSingle().Subject;
        read.Target.Should().Be("orders");
        read.Action.Should().Be("MessageSent");
        read.Timestamp.Should().Be(when);
    }

    [Fact(DisplayName = "Reads are paged and ordered newest first")]
    public void Pages_in_the_query()
    {
        // Both arguments used to be accepted and discarded: the repository returned every
        // record and the caller sorted the lot in memory.
        _settings.MaxRecords = 0;
        var repository = Repository();
        Seed(repository, 25);

        var firstPage = repository.GetHistoryRecords(page: 1, pageSize: 10);
        var secondPage = repository.GetHistoryRecords(page: 2, pageSize: 10);
        var lastPage = repository.GetHistoryRecords(page: 3, pageSize: 10);

        firstPage.Should().HaveCount(10);
        secondPage.Should().HaveCount(10);
        lastPage.Should().HaveCount(5);

        firstPage[0].Target.Should().Be("queue-24", "the newest record comes first");
        firstPage.Last().Target.Should().Be("queue-15");
        secondPage[0].Target.Should().Be("queue-14");
        lastPage.Last().Target.Should().Be("queue-0");
    }

    [Theory(DisplayName = "Page and size arguments below one do not throw or skip records")]
    [InlineData(0, 10)]
    [InlineData(-3, 10)]
    [InlineData(1, 0)]
    public void Tolerates_nonsense_paging(int page, int pageSize)
    {
        var repository = Repository();
        Seed(repository, 3);

        repository.Invoking(r => r.GetHistoryRecords(page, pageSize)).Should().NotThrow();
    }

    [Fact(DisplayName = "History is pruned to the retention setting as records arrive")]
    public void Prunes_to_the_retention_limit()
    {
        // One row per send, forever, in the same file as the user's connections and
        // templates. Nothing removed anything.
        _settings.MaxRecords = 10;
        var repository = Repository();

        Seed(repository, 25);

        repository.Count().Should().Be(10);

        var kept = repository.GetHistoryRecords(page: 1, pageSize: 100);
        kept.Should().HaveCount(10);
        kept[0].Target.Should().Be("queue-24");
        kept.Last().Target.Should().Be("queue-15", "the oldest records are the ones dropped");
    }

    [Fact(DisplayName = "A retention setting of zero keeps everything")]
    public void Retention_can_be_turned_off()
    {
        _settings.MaxRecords = 0;
        var repository = Repository();

        Seed(repository, 30);

        repository.Count().Should().Be(30);
    }

    [Fact(DisplayName = "The timestamp index exists, so ordered reads are not a full scan")]
    public void Indexes_the_sort_key()
    {
        var repository = Repository();
        repository.AddHistoryRecord(RecordAt(DateTimeOffset.UtcNow, "orders"));

        // Reads and the retention sweep both order by timestamp, and without an index
        // LiteDB sorts the whole collection in memory to answer them.
        _db.Context.ListIndexes("history").Should().Contain("Timestamp");
    }

    [Fact(DisplayName = "Deleting history empties the collection")]
    public void Deletes_everything()
    {
        var repository = Repository();
        Seed(repository, 5);

        repository.DeleteHistory();

        repository.Count().Should().Be(0);
        repository.GetHistoryRecords().Should().BeEmpty();
    }

    [Fact(DisplayName = "The database records a schema version to migrate from")]
    public void Stamps_a_schema_version()
    {
        _db.Context.UserVersion.Should().Be(IrisLiteDbContext.SchemaVersion);
    }
}
