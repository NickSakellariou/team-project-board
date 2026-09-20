using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Modules.Common.Tests.Architecture;

/// <summary>
/// Asserts the rules that hold across every log catalogue at once.
/// </summary>
/// <remarks>
/// <para>
/// A catalogue's own test file pins what each of its events says. Nothing there can see the
/// other catalogues, so nothing there can notice that two modules picked the same event id,
/// or that a new catalogue claimed a range another one already owns. Event ids are one
/// number line shared by the whole solution — this is the only place that sees all of it.
/// </para>
/// <para>
/// The allocation these tests enforce is written down in <c>docs/log-event-ids.md</c>, and
/// the <c>adding-a-log</c> skill describes the steps for adding an event.
/// </para>
/// </remarks>
public class LogCatalogueTests
{
    /// <summary>
    /// A catalogue must declare the range it owns.
    /// </summary>
    /// <remarks>
    /// Everything below is built on those two constants, so a catalogue without them would
    /// quietly opt out of every rule here rather than fail one.
    /// </remarks>
    [Fact]
    public void EveryCatalogue_DeclaresItsRange()
    {
        var withoutRange = LogCatalogues
            .Where(catalogue => RangeOf(catalogue) is null)
            .Select(catalogue => catalogue.FullName)
            .ToList();

        Assert.True(
            withoutRange.Count == 0,
            "These catalogues declare no EventIdRangeStart/EventIdRangeEnd: " +
            string.Join(", ", withoutRange));
    }

    /// <summary>Two catalogues must not claim overlapping ranges.</summary>
    [Fact]
    public void CatalogueRanges_DoNotOverlap()
    {
        var ranges = LogCatalogues
            .Select(catalogue => (Name: catalogue.Name, Range: RangeOf(catalogue)))
            .Where(entry => entry.Range is not null)
            .Select(entry => (entry.Name, Start: entry.Range!.Value.Start, End: entry.Range.Value.End))
            .OrderBy(entry => entry.Start)
            .ToList();

        var overlaps = new List<string>();

        for (var index = 1; index < ranges.Count; index++)
        {
            var previous = ranges[index - 1];
            var current = ranges[index];

            if (current.Start <= previous.End)
            {
                overlaps.Add(
                    $"{previous.Name} ({previous.Start}-{previous.End}) and " +
                    $"{current.Name} ({current.Start}-{current.End})");
            }
        }

        Assert.True(overlaps.Count == 0, $"Overlapping event id ranges: {string.Join("; ", overlaps)}");
    }

    /// <summary>A catalogue must declare a start that is not after its end.</summary>
    [Fact]
    public void CatalogueRanges_AreNotInverted()
    {
        var inverted = LogCatalogues
            .Select(catalogue => (catalogue.Name, Range: RangeOf(catalogue)))
            .Where(entry => entry.Range is not null && entry.Range.Value.Start > entry.Range.Value.End)
            .Select(entry => $"{entry.Name} ({entry.Range!.Value.Start}-{entry.Range.Value.End})")
            .ToList();

        Assert.True(inverted.Count == 0, $"Inverted event id ranges: {string.Join(", ", inverted)}");
    }

    /// <summary>
    /// Every event id sits inside the range its own catalogue claims.
    /// </summary>
    /// <remarks>
    /// The mistake this catches is copying an existing declaration to write a new one and
    /// changing everything except the id's leading digits.
    /// </remarks>
    [Fact]
    public void EveryEventId_IsInsideItsCataloguesRange()
    {
        var outOfRange = LogEvents
            .Where(logEvent => logEvent.Range is not null &&
                               (logEvent.EventId < logEvent.Range.Value.Start ||
                                logEvent.EventId > logEvent.Range.Value.End))
            .Select(logEvent =>
                $"{logEvent.Catalogue}.{logEvent.Method} = {logEvent.EventId.ToString(CultureInfo.InvariantCulture)} " +
                $"(range {logEvent.Range!.Value.Start}-{logEvent.Range.Value.End})")
            .ToList();

        Assert.True(outOfRange.Count == 0, $"Event ids outside their range: {string.Join(", ", outOfRange)}");
    }

    /// <summary>
    /// No two events anywhere share an id.
    /// </summary>
    /// <remarks>
    /// The one failure that is invisible in production rather than merely wrong: two events
    /// with one id means a dashboard filtered on it shows both, and neither count is real.
    /// </remarks>
    [Fact]
    public void EveryEventId_IsUniqueAcrossTheSolution()
    {
        var duplicates = LogEvents
            .GroupBy(logEvent => logEvent.EventId)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"{group.Key.ToString(CultureInfo.InvariantCulture)}: " +
                string.Join(" and ", group.Select(logEvent => $"{logEvent.Catalogue}.{logEvent.Method}")))
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate event ids: {string.Join("; ", duplicates)}");
    }

    /// <summary>
    /// Message templates use PascalCase placeholders.
    /// </summary>
    /// <remarks>
    /// A placeholder is a field name in the log store, not a C# parameter — <c>{UserId}</c>
    /// and <c>{userId}</c> are two different columns, and mixing them is how a saved query
    /// ends up returning half the events it should.
    /// </remarks>
    [Fact]
    public void EveryMessageTemplate_UsesPascalCasePlaceholders()
    {
        var offenders = LogEvents
            .SelectMany(logEvent => PlaceholderPattern
                .Matches(logEvent.Message)
                .Select(match => match.Groups["placeholder"].Value)
                .Where(placeholder => !char.IsUpper(placeholder[0]))
                .Select(placeholder => $"{logEvent.Catalogue}.{logEvent.Method}: {{{placeholder}}}"))
            .ToList();

        Assert.True(offenders.Count == 0, $"Placeholders that are not PascalCase: {string.Join(", ", offenders)}");
    }

    /// <summary>
    /// An event declares a level rather than taking one from its caller.
    /// </summary>
    /// <remarks>
    /// <c>[LoggerMessage]</c> allows a run-time <c>LogLevel</c> parameter. That would make
    /// the severity of an event a property of the call site, so the same id could arrive at
    /// Information from one place and Error from another, and no alert could be built on it.
    /// </remarks>
    [Fact]
    public void EveryEvent_DeclaresItsLevel()
    {
        var withoutLevel = LogEvents
            .Where(logEvent => logEvent.Level is null)
            .Select(logEvent => $"{logEvent.Catalogue}.{logEvent.Method}")
            .ToList();

        Assert.True(withoutLevel.Count == 0, $"Events with no declared level: {string.Join(", ", withoutLevel)}");
    }

    /// <summary>Catalogue classes must be named <c>*Logs</c>, so the rules above find them.</summary>
    [Fact]
    public void EveryEvent_IsDeclaredInACatalogue()
    {
        var strays = CatalogueAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Any(method => method.GetCustomAttribute<LoggerMessageAttribute>() is not null))
            .Where(type => !type.Name.EndsWith("Logs", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(
            strays.Count == 0,
            $"These types declare log events but are not named *Logs: {string.Join(", ", strays)}");
    }

    /// <summary>Confirms the rules above are not passing because they found nothing.</summary>
    [Fact]
    public void TheCatalogues_AreDiscovered()
    {
        Assert.NotEmpty(LogCatalogues);
        Assert.NotEmpty(LogEvents);
    }

    // ----------------------------------------------------------------------------------
    // Discovery
    // ----------------------------------------------------------------------------------

    private static readonly Regex PlaceholderPattern = new(
        @"\{(?<placeholder>\w+)[:,}]",
        RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    // Every assembly that is allowed to declare log events. A new module adds its Domain
    // here at the same time as it adds its catalogue.
    private static readonly Assembly[] CatalogueAssemblies =
    [
        typeof(Common.API.Logging.CommonApiLogs).Assembly,
        typeof(Common.Application.Logging.CommonApplicationLogs).Assembly,
        typeof(Common.Infrastructure.Logging.CommonInfrastructureLogs).Assembly,
        typeof(global::TeamProjectBoard.Host.Logging.HostLogs).Assembly,
        ModuleAssemblies.UsersDomain,
        ModuleAssemblies.UsersFeatures,
        ModuleAssemblies.UsersInfrastructure
    ];

    private static readonly Type[] LogCatalogues = [.. CatalogueAssemblies
        .Distinct()
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => type.IsClass && type.IsAbstract && type.IsSealed)
        .Where(type => type.Name.EndsWith("Logs", StringComparison.Ordinal))
        .OrderBy(type => type.FullName, StringComparer.Ordinal)];

    private static readonly LogEvent[] LogEvents = [.. LogCatalogues
        .SelectMany(catalogue => catalogue
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<LoggerMessageAttribute>()))
            .Where(entry => entry.Attribute is not null)
            .Select(entry => new LogEvent(
                catalogue.Name,
                entry.Method.Name,
                entry.Attribute!.EventId,
                DeclaredLevel(entry.Attribute),
                entry.Attribute.Message ?? string.Empty,
                RangeOf(catalogue))))];

    // An attribute that leaves Level unset stores (LogLevel)(-1), which is not a defined
    // member of the enum. That is the "the call site decides" case, and
    // EveryEvent_DeclaresItsLevel is what rejects it.
    private static LogLevel? DeclaredLevel(LoggerMessageAttribute attribute) =>
        Enum.IsDefined(attribute.Level) ? attribute.Level : null;

    private static (int Start, int End)? RangeOf(Type catalogue)
    {
        var start = ConstantOf(catalogue, "EventIdRangeStart");
        var end = ConstantOf(catalogue, "EventIdRangeEnd");

        return start is null || end is null ? null : (start.Value, end.Value);
    }

    private static int? ConstantOf(Type catalogue, string name) =>
        catalogue.GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as int?;

    private sealed record LogEvent(
        string Catalogue,
        string Method,
        int EventId,
        LogLevel? Level,
        string Message,
        (int Start, int End)? Range);
}
