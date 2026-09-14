using FluentAssertions;
using Iris.Components.Messaging;
using Iris.Desktop.Brokers;
using Iris.Desktop.History;
using Iris.Desktop.PackageManagement;
using Iris.Desktop.Templates;
using LiteDB;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using HistoryRecord = Iris.History.HistoryRecord;

namespace Iris.Desktop.Test.Infrastructure;

/// <summary>
/// The shape of what Iris actually writes to disk: collection names, field names, and the
/// BSON type of each field, captured as a committed fixture.
///
/// <para>
/// This is the one drift guard that matters after a release has shipped. Renaming a property,
/// or changing one whose CLR type maps to a different BSON type, does not fail a build, does
/// not fail a round-trip test written against the new code, and does not throw at runtime.
/// LiteDB simply reads nothing for the field it cannot find, so the user's saved connections,
/// templates or history come back blank, and the old values are still sitting in the file.
/// </para>
///
/// <para>
/// A diff here is not automatically a defect. It is the moment to decide whether
/// <c>IrisLiteDbContext.SchemaVersion</c> needs to go up and a migration needs to exist,
/// which is the whole reason that constant is stamped on the file.
/// </para>
/// </summary>
public class PersistedDocumentShapeTests
{
    [Fact(DisplayName = "Every persisted document matches its committed shape")]
    public async Task Persisted_shapes_match_the_fixture()
    {
        using var database = new TempDatabase();

        await WriteOneOfEachAsync(database);

        // Through a second connection rather than the context, which only hands out
        // collections of ILocalEntity: the point is to read the raw documents, exactly as a
        // future version of Iris would find them.
        var shapes = ReadShapes(database.DatabasePath);

        var actual = shapes.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "persisted-documents.expected.json");
        var expected = File.ReadAllText(expectedPath);

        Normalize(actual).Should().Be(Normalize(expected));
    }

    /// <summary>
    /// Written through the repositories, not through raw collections, so the collection name
    /// each one chooses is part of what the fixture pins. A renamed collection loses the data
    /// just as completely as a renamed field.
    /// </summary>
    private static async Task WriteOneOfEachAsync(TempDatabase database)
    {
        new ConnectionRepository(database.Context, NullLogger<ConnectionRepository>.Instance)
            .Save(new SavedConnection
            {
                Provider = "RabbitMq",
                Address = "amqp://localhost",
                // Every nullable populated: LiteDB omits a null field rather than writing it,
                // so an unset one would simply be missing from the snapshot.
                Uri = "amqp://localhost",
                Username = "guest",
                Password = "guest",
                ConnectionString = "Endpoint=sb://localhost;",
                Region = "eu-west-1",
                CreatedAt = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero),
            });

        new TemplateRepository(database.Context, NullLogger<TemplateRepository>.Instance)
            .Save(new PersistentTemplate
            {
                TemplateId = Guid.NewGuid(),
                Name = "Order placed",
                Json = "{}",
                Version = 2,
            });

        new PackageRepository(database.Context, NullLogger<PackageRepository>.Instance)
            .Save(new SavedPackage
            {
                FilePath = "/packages/MyApp.Contracts.dll",
                AssemblyName = "MyApp.Contracts",
                AddedAt = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero),
            });

        new HistoryRepository(database.Context, new HistorySettings(), NullLogger<HistoryRepository>.Instance)
            .AddHistoryRecord(new HistoryRecord("MessageSent", "Local", "orders")
            {
                Action = "MessageSent",
                Source = "Local",
                EventAction = "SendMessage",
                EventParameters = new Dictionary<string, object> { ["Address"] = "amqp://localhost" },
                Details = "{}",
                Timestamp = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero),
            });

        var layouts = new MessageLayoutRepository(database.Context);
        var layout = new LayoutState(layouts).GetDefaultTabLayout();
        // BadgeCount is the only nullable on a tab, and LiteDB writes nothing for a null, so
        // the fixture would not cover it at all if every tab were left unbadged.
        layout[0].BadgeCount = 3;
        await layouts.SaveLayoutAsync(layout);
    }

    [Fact(DisplayName = "A stored timestamp keeps its instant and comes back as UTC")]
    public void Stored_timestamps_come_back_as_utc()
    {
        // The shape fixture records DateTime for every DateTimeOffset property, so the offset
        // the record was written with is not on disk. The instant survives and the offset
        // reads back as zero. Nothing renders one of these raw (the History grid hands it to
        // LocalTime), so this is a note rather than a defect, but it is the kind of thing a
        // future reader of a persisted timestamp has to know before comparing one.
        using var database = new TempDatabase();
        var written = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.FromHours(5));

        var repository = new HistoryRepository(database.Context, new HistorySettings(), NullLogger<HistoryRepository>.Instance);
        repository.AddHistoryRecord(new HistoryRecord("MessageSent", "Local", "orders")
        {
            Action = "MessageSent",
            Source = "Local",
            Timestamp = written,
        });

        var read = repository.GetHistoryRecords().Single().Timestamp;

        read.UtcDateTime.Should().Be(written.UtcDateTime, "the instant survives");
        read.Offset.Should().Be(TimeSpan.Zero, "the offset it was written with is not stored");
    }

    private static JsonObject ReadShapes(string path)
    {
        using var raw = new LiteDatabase(new ConnectionString { Filename = path, Connection = ConnectionType.Shared });

        var shapes = new JsonObject();

        // Sorted, because LiteDB lists collections in creation order and the writes above
        // could be reordered without meaning anything.
        foreach (var collection in raw.GetCollectionNames().OrderBy(n => n, StringComparer.Ordinal))
        {
            var document = raw.GetCollection(collection).FindAll().FirstOrDefault();
            if (document is null)
                continue;

            shapes[collection] = Describe(document);
        }

        return shapes;
    }

    /// <summary>
    /// Field names and BSON types, not values. Values are the test data; the types are the
    /// contract, and a type change is the half that reads back as blank rather than throwing.
    /// Nested documents recurse, because the layout is stored as an array of them.
    /// </summary>
    private static JsonObject Describe(BsonDocument document)
    {
        var described = new JsonObject();

        foreach (var (name, value) in document.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            described[name] = value switch
            {
                { IsDocument: true } => Describe(value.AsDocument),
                { IsArray: true } => DescribeArray(value.AsArray),
                _ => JsonValue.Create(value.Type.ToString()),
            };
        }

        return described;
    }

    private static JsonNode DescribeArray(BsonArray array)
    {
        var first = array.FirstOrDefault();

        // The element shape, not the length: how many tabs the default layout happens to
        // have is not a storage contract.
        return first is { IsDocument: true }
            ? new JsonArray(Describe(first.AsDocument))
            : new JsonArray(JsonValue.Create(first?.Type.ToString() ?? "Null"));
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();
}
