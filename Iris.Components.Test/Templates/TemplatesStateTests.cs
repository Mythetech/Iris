using FluentAssertions;
using Iris.Components.Templates;
using Iris.Contracts.Templates.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Iris.Components.Test.Templates;

/// <summary>
/// The template cache: what the store is told, what the cache ends up holding, and who hears
/// about it.
///
/// <para>
/// The cache loads lazily, and every writing method used to assume something had already
/// loaded it. Three of the four ways to reach a write do not go through a list first: the
/// editor page, the rename field on the messaging tab, and the native menu.
/// </para>
/// </summary>
public class TemplatesStateTests
{
    private static Template Template(string name, string json = "{}") =>
        new() { TemplateId = Guid.NewGuid(), Name = name, Json = json };

    /// <summary>
    /// A substitute that actually stores, rather than one that returns a fixed list. The
    /// order of the write against the load is load-bearing here: a cache loaded after the
    /// write reads back the row just written, and adding it again on top of that is a
    /// duplicate no assertion against a fixed list could see.
    /// </summary>
    private static (TemplatesState State, ITemplateService Service) Create(params Template[] stored)
    {
        var store = stored.ToList();
        var service = Substitute.For<ITemplateService>();

        // A copy per call: the cache takes ownership of what it is handed, and sharing the
        // store's own list would hide a write landing in the wrong place.
        service.GetTemplatesAsync().Returns(_ => store.ToList());
        service.CreateTemplateAsync(Arg.Any<Template>()).Returns(call =>
        {
            store.Add(call.Arg<Template>());
            return Task.CompletedTask;
        });
        service.UpdateTemplateAsync(Arg.Any<Template>(), Arg.Any<bool>()).Returns(call =>
        {
            var template = call.Arg<Template>();
            var index = store.FindIndex(t => t.TemplateId == template.TemplateId);
            if (index >= 0)
                store[index] = template;
            return Task.CompletedTask;
        });
        service.DeleteTemplateAsync(Arg.Any<Template>()).Returns(call =>
        {
            store.RemoveAll(t => t.TemplateId == call.Arg<Template>().TemplateId);
            return Task.CompletedTask;
        });

        return (new TemplatesState(service, NullLogger<TemplatesState>.Instance), service);
    }

    [Fact(DisplayName = "Templates load from the store and are published")]
    public async Task Load_reads_the_store()
    {
        var (state, _) = Create(Template("First"), Template("Second"));
        var notified = 0;
        state.TemplateStateChanged += () => notified++;

        var templates = await state.GetTemplatesAsync();

        templates.Select(t => t.Name).Should().Equal("First", "Second");
        state.Templates.Should().BeSameAs(templates);
        notified.Should().Be(1);
    }

    [Fact(DisplayName = "A second read is served from the cache and does not re-notify")]
    public async Task Load_is_cached()
    {
        var (state, service) = Create(Template("First"));
        await state.GetTemplatesAsync();
        var notified = 0;
        state.TemplateStateChanged += () => notified++;

        await state.GetTemplatesAsync();

        await service.Received(1).GetTemplatesAsync();
        notified.Should().Be(0);
    }

    [Fact(DisplayName = "Creating persists first, then joins the cache")]
    public async Task Create_persists_and_caches()
    {
        var (state, service) = Create(Template("First"));
        await state.GetTemplatesAsync();
        var created = Template("Second");
        var notified = 0;
        state.TemplateStateChanged += () => notified++;

        await state.CreateTemplateAsync(created);

        await service.Received(1).CreateTemplateAsync(created);
        state.Templates!.Select(t => t.Name).Should().Equal("First", "Second");
        notified.Should().Be(1);
    }

    [Fact(DisplayName = "Creating before anything has been listed keeps the stored templates")]
    public async Task Create_before_a_load_keeps_the_store()
    {
        // The editor can create without the list ever having been opened. The cache used to
        // start as an empty list in that case, so the next read of the templates panel
        // showed the one just created and nothing else, until a restart. Loading in the
        // other order is the opposite failure: the load reads back the row just written and
        // the add puts it there twice.
        var (state, _) = Create(Template("First"));

        await state.CreateTemplateAsync(Template("Second"));

        var templates = await state.GetTemplatesAsync();
        templates.Select(t => t.Name).Should().Equal("First", "Second");
    }

    [Fact(DisplayName = "A store that refuses the create leaves the cache alone")]
    public async Task Create_failure_does_not_cache()
    {
        var (state, service) = Create(Template("First"));
        service.CreateTemplateAsync(Arg.Any<Template>()).ThrowsAsync(new InvalidOperationException("full"));

        var act = () => state.CreateTemplateAsync(Template("Second"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await state.GetTemplatesAsync()).Select(t => t.Name).Should().Equal("First");
    }

    [Fact(DisplayName = "Updating persists and swaps the cached copy for the one that was saved")]
    public async Task Update_replaces_the_cached_copy()
    {
        var first = Template("First");
        var (state, service) = Create(first, Template("Second"));
        await state.GetTemplatesAsync();
        var edited = new Template { TemplateId = first.TemplateId, Name = "Renamed", Json = "{\"a\":1}" };

        await state.UpdateTemplateAsync(edited, newVersion: true);

        await service.Received(1).UpdateTemplateAsync(edited, true);
        state.Templates!.Should().HaveCount(2);
        state.Templates.Should().Contain(edited);
        state.Templates!.Select(t => t.Name).Should().Equal("Renamed", "Second");
    }

    [Fact(DisplayName = "Updating before anything has been listed loads the cache rather than throwing")]
    public async Task Update_before_a_load_does_not_throw()
    {
        // The rename field on the messaging templates tab reaches this without the templates
        // page ever having been opened. It used to dereference a null cache.
        var first = Template("First");
        var (state, _) = Create(first);
        var edited = new Template { TemplateId = first.TemplateId, Name = "Renamed" };

        await state.UpdateTemplateAsync(edited);

        state.Templates!.Select(t => t.Name).Should().Equal("Renamed");
    }

    [Fact(DisplayName = "Updating a template the cache does not hold adds nothing")]
    public async Task Update_of_an_unknown_template_adds_nothing()
    {
        var (state, _) = Create(Template("First"));
        await state.GetTemplatesAsync();

        await state.UpdateTemplateAsync(Template("Stranger"));

        state.Templates!.Select(t => t.Name).Should().Equal("First");
    }

    [Fact(DisplayName = "Deleting persists and drops the cached copy")]
    public async Task Delete_removes_from_the_cache()
    {
        var first = Template("First");
        var (state, service) = Create(first, Template("Second"));
        await state.GetTemplatesAsync();
        var notified = 0;
        state.TemplateStateChanged += () => notified++;

        await state.DeleteTemplateAsync(first);

        await service.Received(1).DeleteTemplateAsync(first);
        state.Templates!.Select(t => t.Name).Should().Equal("Second");
        notified.Should().Be(1);
    }

    [Fact(DisplayName = "Deleting before anything has been listed loads the cache rather than throwing")]
    public async Task Delete_before_a_load_does_not_throw()
    {
        var first = Template("First");
        var (state, _) = Create(first, Template("Second"));

        await state.DeleteTemplateAsync(first);

        state.Templates!.Select(t => t.Name).Should().Equal("Second");
    }

    [Fact(DisplayName = "Duplicating copies the body under a new name and a new identity")]
    public async Task Duplicate_copies_the_body()
    {
        var original = new Template { TemplateId = Guid.NewGuid(), Name = "Order", Json = "{\"id\":1}", Version = 4 };
        var (state, service) = Create(original);
        await state.GetTemplatesAsync();

        var copy = await state.DuplicateTemplateAsync(original);

        copy.Name.Should().Be("Order (Copy)");
        copy.Json.Should().Be(original.Json);
        copy.TemplateId.Should().NotBe(original.TemplateId);
        copy.Version.Should().Be(0, "a copy starts its own version history rather than inheriting one");
        await service.Received(1).CreateTemplateAsync(copy);
        state.Templates!.Should().Contain(copy);
    }

    [Fact(DisplayName = "Loading a template into the editor is announced with the template itself")]
    public void Load_template_announces_the_template()
    {
        var (state, _) = Create();
        var template = Template("First");
        Template? announced = null;
        state.TemplateLoaded += t => announced = t;

        state.LoadTemplate(template);

        announced.Should().BeSameAs(template);
    }

    [Fact(DisplayName = "Every subscriber is notified, and unsubscribing stops delivery")]
    public async Task Subscribers_coexist()
    {
        var (state, _) = Create(Template("First"));
        var first = 0;
        var second = 0;
        void Second() => second++;
        state.TemplateStateChanged += () => first++;
        state.TemplateStateChanged += Second;

        await state.GetTemplatesAsync();
        state.TemplateStateChanged -= Second;
        await state.CreateTemplateAsync(Template("Second"));

        first.Should().Be(2);
        second.Should().Be(1);
    }

    [Fact(DisplayName = "The notifications are events, not settable delegate properties")]
    public void Notifications_are_events()
    {
        // As public settable Action properties, any subscriber writing = instead of +=
        // silently dropped every other subscriber's handler, and nothing anywhere would
        // report it. There are four subscribers to TemplateStateChanged.
        foreach (var name in new[] { nameof(TemplatesState.TemplateStateChanged), nameof(TemplatesState.TemplateLoaded) })
        {
            typeof(TemplatesState).GetProperty(name).Should().BeNull(name);
            typeof(TemplatesState).GetEvent(name).Should().NotBeNull(name);
            typeof(ITemplatesState).GetEvent(name).Should().NotBeNull($"{name} is how Messaging subscribes");
        }
    }

    [Fact(DisplayName = "The interface carries everything its consumers need, so nobody has to downcast")]
    public void Interface_covers_its_consumers()
    {
        // Messaging.razor injected ITemplatesState and then pattern-matched back to the
        // concrete class to reach TemplateLoaded, which silently no-ops if the registration
        // ever hands out anything else.
        typeof(ITemplatesState).GetProperty(nameof(ITemplatesState.Templates)).Should().NotBeNull();
        typeof(ITemplatesState).GetMethod(nameof(ITemplatesState.LoadTemplate)).Should().NotBeNull();
    }
}
