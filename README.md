# ProphetsWay.Logger

Route log messages to console, file, event, or custom destinations with per-destination severity and label selection.

> **Current tree: unreleased v4 work.** The version file still reads `3.0.1`.
> Explicit-registration route isolation and ordered membership snapshots (M2-B) are implemented.
> Native M3 scopes, entry annotations, full captured context, registration settings and
> whole-entry label selection are now implemented alongside the six native severities.
> M4-A adds shared UTC event time, invariant value rendering and quoted single-line text
> with native scope boundaries. M4-B1 implements explicit-file append/reset, fixed-path output
> and physical encoding/termination. M4-B2 now integrates ordinary and exact-`T` automatic output
> into one shared UTF-8 session file, with initial-only recovery and remembered initialization failure.
> Supplied destinations use guarded entrypoints and protected `LogCore` hooks; subclasses
> must migrate their old `Log` overrides. This describes current source, not the published
> NuGet package, a completed final acceptance gate, or release certification.

Build Status:  
[![Build Status](https://dev.azure.com/ProphetsWay/ProphetsWay%20GitHub%20Projects/_apis/build/status/ProphetManX.ProphetsWay.Logger?repoName=ProphetManX%2FProphetsWay.Logger&branchName=main)](https://dev.azure.com/ProphetsWay/ProphetsWay%20GitHub%20Projects/_build/latest?definitionId=25&repoName=ProphetManX%2FProphetsWay.Logger&branchName=main)

## Why Logger

When you need both a detailed log and a warning/error view, repeating each logging call
for each output adds unnecessary work. Configure destinations once at application startup;
plain Logger calls reach enabled registered destinations whose severity and label restrictions
accept the entry.

- Send the same message to console, files, or callbacks with different reporting levels.
- Pass application metadata to typed destinations without putting it into message text.
- Carry application-defined labels and scope properties, withholding the whole entry from
    destinations whose configured policies reject it.
- Evaluate the same label policy independently when you need a membership-only decision.

Configure your intended destinations explicitly. The automatic file is quick-start behavior,
not a substitute for choosing output paths, access controls, and retention.

## Install

For a published package, use the .NET CLI:

```powershell
dotnet add package ProphetsWay.Logger
```

Or use Visual Studio's NuGet Package Manager Console:

```powershell
Install-Package ProphetsWay.Logger
```

These commands do not install the unreleased API merely because it is documented here.
To use the native severity, scope/context, label-policy and text/formatter examples now, reference the current
[library project](ProphetsWay.Logger/ProphetsWay.Logger.csproj) from your application.
[app-variables.yml](app-variables.yml) still selects `3.0.1`; no v4 package or release is claimed.

### Current Source Targets

| Project | Targets | Meaning |
| --- | --- | --- |
| [Library](ProphetsWay.Logger/ProphetsWay.Logger.csproj) | `netstandard2.0;net10.0` | .NET Standard 2.0 compatibility floor plus a dedicated .NET 10 asset. |
| [Tests](ProphetsWay.Logger.Test/ProphetsWay.Logger.Test.csproj) | `net48;net10.0` | Windows Framework verification uses the Standard 2.0 library; .NET 10 tests use its dedicated asset. |
| [Example](ProphetsWay.Logger.Example/ProphetsWay.Logger.Example.csproj) | `net10.0` | Console example, built but not run in the focused validation below. |

There is no separate `net48` library asset. Older modern runtimes compatible with
.NET Standard 2.0 can still use that floor; removing their dedicated assets does not
automatically strand them. Consumers requiring pre-Standard-2.0 reach cannot use this
new target set. Verification is currently Windows-focused, not equivalent Mac/Linux proof.

## Quick Start

Each C# block below is independent, with its own imports, enclosing types and static entry
method, written for C# 7.3 library compilation. Call its `Main` or `Run` method only when you
intend its output. Examples use current declarations and test call patterns. Expected output
describes the source contract, not a recorded execution of these programs.

### Log To The Console

Register before logging to avoid implicit file creation. Retain the destination reference
so you can remove it. This follows the calls in
[ConsoleDestinationTests.cs](ProphetsWay.Logger.Test/ConsoleDestinationTests.cs).

> **Illustrative** — not currently present in the repo.

```csharp
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;

public static class Program
{
    public static void Main()
    {
        var destination = new ConsoleDestination(LogLevels.Trace);
        Logger.AddDestination(destination);
        try
        {
            Logger.Trace("Detailed startup trace.");
            Logger.Debug("Hello World!");
        }
        finally
        {
            Logger.RemoveDestination(destination);
        }
    }
}
```

The messages use invariant UTC timestamps, quoted single-line text and exact emitted levels,
`TraceOnly` and `DebugOnly`; see [Record Layout](#record-layout).
The explicit `Trace` destination mask accepts all six severities; the constructor's
unchanged default `Debug` mask does not accept Trace.
`Logger.ClearDestinations()` clears plain registrations; `Logger.ClearDestinations<T>()`
clears registrations for that metadata type. Removing or clearing does not dispose your
destinations. Do not log after removing the last destination unless you intend fallback.
For no-setup output and optional startup configuration, see [Automatic File Output](#automatic-file-output).

### Evaluate A Label Policy

This example does not call Logger or write a log file. The identifiers are synthetic,
not predefined classifications. The rules and copied-membership behavior are exercised in
[DestinationLabelPolicyTests.cs](ProphetsWay.Logger.Test/DestinationLabelPolicyTests.cs).

> **Illustrative** — not currently present in the repo.

```csharp
using System;
using System.Collections.Generic;
using ProphetsWay.Utilities;

public static class Program
{
    public static void Main()
    {
        var personal = new SensitivityLabel("PII");
        var internalUse = new SensitivityLabel("Internal");
        var configured = new List<SensitivityLabel>
        {
            personal, internalUse, new SensitivityLabel("PII")
        };
        var allow = new DestinationLabelPolicy(LabelFilterMode.AllowOnly, configured);
        configured.Clear();

        Console.WriteLine(personal.Equals(new SensitivityLabel("PII")));
        Console.WriteLine(allow.Mode);
        Console.WriteLine(allow.Labels.Count);
        Console.WriteLine(allow.Allows(new[] { personal, internalUse }));
        Console.WriteLine(allow.Allows(new[] { new SensitivityLabel("pii") }));
        Console.WriteLine(allow.Allows(new SensitivityLabel[0]));

        var exclude = new DestinationLabelPolicy(LabelFilterMode.Exclude, new[] { personal });
        Console.WriteLine(exclude.Allows(new[] { internalUse }));
        Console.WriteLine(exclude.Allows(new[] { personal, internalUse }));

        var noFilter = new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]);
        Console.WriteLine(noFilter.Allows(new[] { personal }));
    }
}
```

Expected output from the source/test contract, not a recorded execution of this snippet:

```text
True
AllowOnly
2
True
False
False
True
False
True
```

`Allows` returns only label eligibility. It neither registers a destination nor intercepts
a later `Logger.Debug` call by itself. Attach the policy through registration settings or a
supplied destination's `LabelPolicy` to enforce it during delivery.

### Attach Scopes And Read Delivered Context

Use scopes for labels and properties shared by an operation, and `LogAnnotated` for labels
specific to one entry. This event recipient accepts only entries whose complete effective
label set is nonempty and contained in its configured membership. All data here is synthetic;
the example creates no file. The scope and context assertions live in
[LoggerTests.cs](ProphetsWay.Logger.Test/LoggerTests.cs).

> **Illustrative** — not currently present in the repo.

```csharp
using System;
using System.Collections.Generic;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;

public static class ScopeExample
{
    public static void Run()
    {
        var internalUse = new SensitivityLabel("Internal");
        var personal = new SensitivityLabel("PersonalData");
        var policy = new DestinationLabelPolicy(
            LabelFilterMode.AllowOnly, new[] { internalUse, personal });
        var destination = new EventDestination(LogLevels.Trace);
        destination.LoggingEvent += ReadEntry;
        Logger.AddDestination(destination,
            new DestinationRegistrationSettings(true, LogLevels.Trace, policy));

        var properties = new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>("Step", "Receive"),
            new KeyValuePair<string, object>("Step", "Validate")
        };
        try
        {
            using (Logger.BeginScope(null, properties))
            {
                properties.Clear();
                using (Logger.BeginScope(new LogAnnotations(new[] { internalUse, internalUse })))
                {
                    Logger.LogAnnotated(new LogAnnotations(new[] { personal }),
                        LogLevels.InformationOnly, message: "Synthetic request.");
                }
            }

            Logger.SetDestinationSettings(destination, new DestinationRegistrationSettings(
                true, LogLevels.Trace,
                new DestinationLabelPolicy(LabelFilterMode.AllowOnly, new SensitivityLabel[0])));
            Logger.Info("Deliberately rejected without activating fallback.");
        }
        finally
        {
            Logger.RemoveDestination(destination);
        }
    }

    private static void ReadEntry(object sender, EventDestination.LoggerEventArgs entry)
    {
        var context = entry.Context;
        Console.WriteLine(entry.RawMessage);
        foreach (var label in context.Labels.EffectiveLabels)
            Console.WriteLine("Effective label: {0}", label.Identifier);
        foreach (var origin in context.Labels.Origins)
            Console.WriteLine("{0}: scope {1}, occurrence {2}", origin.Label.Identifier,
                origin.ScopeIndex.HasValue ? origin.ScopeIndex.Value.ToString() : "entry",
                origin.OccurrenceIndex);
        foreach (var frame in context.Scopes)
            foreach (var property in frame.Properties)
                Console.WriteLine("{0}: {1}", property.Key, property.Value);
    }
}
```

The unlabeled outer frame keeps both `Step` pairs despite `properties.Clear()`. The repeated
`Internal` label contributes separate origins but one effective identity. Origin scope indices
address `context.Labels.ScopeAnnotations`, so they do not count that unlabeled outer frame.
The final settings replacement keeps an enabled reject-all registration; it does not remove
the recipient or cause automatic output. The callback explicitly formats its synthetic property
values; Logger itself does not implicitly format arbitrary scope objects.

## Core Concepts

### Namespaces

The package and assembly are `ProphetsWay.Logger`; the utility namespace deliberately differs.

| Import | Use |
| --- | --- |
| `ProphetsWay.Utilities` | `Logger`, `LogLevels`, ordinary destination contracts/bases, label/policy types, `LogAnnotations`, scope/context types and `DestinationRegistrationSettings`. |
| `ProphetsWay.Utilities.LoggerDestinations` | `ConsoleDestination`, `FileDestination`, `EventDestination`, `GenericEventDestination<T>`, `TextBasedDestination`, `GenericTextBasedDestination<T>`. |
| `ProphetsWay.Utilities.Generics` | `ILoggingDestination<T>`, `IContextLoggingDestination<T>`, `BaseLoggingDestination<T>`, `ILoggerMetadata`, and metadata extension methods. |

### Severity Selection

These are the **current source** values in [LogLevels.cs](ProphetsWay.Logger/LogLevels.cs).
Each helper emits one exact bit; an inclusive destination setting accepts that severity
and every more severe one.

| Helper | Exact message bit | Inclusive destination mask | Accepted severities |
| --- | --- | --- | --- |
| `Trace` | `TraceOnly = 32` | `Trace = 63` | Trace, Debug, Information, Warning, Error, Critical. |
| `Debug` | `DebugOnly = 16` | `Debug = 31` | Debug, Information, Warning, Error, Critical. |
| `Info` | `InformationOnly = 8` | `Information = 15` | Information, Warning, Error, Critical. |
| `Warn` | `WarningOnly = 4` | `Warning = 7` | Warning, Error, Critical. |
| `Error` | `ErrorOnly = 2` | `Error = 3` | Error, Critical. |
| `Critical` | `Critical = 1` | `Critical = 1` | Critical only. |

`LogLevels` is an Int32 flags enum with these eleven names. There is no `CriticalOnly`,
`None`, or `All` alias. Use an exact bit as the destination mask for just that severity,
or combine bits with `|`. `Security` and `SecurityOnly` have been removed.

Destination masks accept **every integer from 0 through 63**, including unnamed combinations.
Zero is an active reject-all destination, not an absent registration or a reason to use fallback.
Raw/direct message masks accept **1 through 63**, never zero. Eligibility requires
`(messageLevel & destinationMask) == messageLevel`: message mask 9 passes destination 15
but fails destination 8. A valid mismatch returns normally without delivery.

The core's enum, integer and string constructors preserve equivalent accepted bits.
Strings use case-sensitive `Enum.TryParse<LogLevels>` grammar: decimal Int32 text or
comma-separated recognized names, with its surrounding/token whitespace handling.
Signs and leading zeroes follow that parser too: `" +009 "` is 9 and `"-0"` is zero.
`"0"`, `"9"`, `" +9 "`, and `"Critical, InformationOnly"` are valid; duplicate names combine
by OR. Empty/whitespace-only text, wrong-case names, removed/unknown names, empty comma
tokens, `"Critical|InformationOnly"`, `"Critical, 8"`, `"1, 8"`, `"0x9"`, overflow, negative
values and unknown bits are rejected. Invalid text no longer silently selects Information.
Prefer named masks; configure `LogLevels.Trace` explicitly when you need all six severities.

### Label Identity

[SensitivityLabel.cs](ProphetsWay.Logger/SensitivityLabel.cs) defines a sealed immutable
reference value. `Identifier` retains exactly what you supplied: 1 through 256 UTF-16 code
units, with no unit accepted by `Char.IsWhiteSpace` or `Char.IsControl` on the executing runtime.
There is no other syntax restriction, trimming, case folding, Unicode normalization, or registry.

Use `Equals` or collection value comparisons: separate labels with the same ordinal identifier
are equal; `PII` and `pii` are distinct. No value-equality `==` operator is supplied. Equal labels
have equal hashes, but hashes are neither unique nor a persistent or cryptographic identity.
See [SensitivityLabelTests.cs](ProphetsWay.Logger.Test/SensitivityLabelTests.cs).

### Annotation Occurrences

[LogAnnotations.cs](ProphetsWay.Logger/LogAnnotations.cs) provides a sealed immutable value
for one explicit label attachment. Construct it with
`LogAnnotations(IEnumerable<SensitivityLabel> labels)` and read its getter-only
`ReadOnlyCollection<SensitivityLabel> LabelOccurrences`. Construction copies the complete
finite sequence before returning: empty is valid, order and every repeated occurrence survive,
and later source additions, removals or replacements cannot change the captured value.

These are occurrences, not the deduplicated membership used by `DestinationLabelPolicy`.
Labels retain `SensitivityLabel`'s ordinal value equality; `LogAnnotations` does not define
annotation-value equality. Reusing a value does not combine attachments or merge their origins.
Construction alone attaches nothing. Pass it to `Logger.BeginScope` or `Logger.LogAnnotated`;
ordinary and typed convenience helpers inherit scopes without adding an entry annotation.
See [LogAnnotationsTests.cs](ProphetsWay.Logger.Test/LogAnnotationsTests.cs).

### Scopes And Captured Context

`Logger.BeginScope(annotations)` opens a label-only frame; the two-argument form also captures
an ordered sequence of `KeyValuePair<string, object>`. A null annotation means no attachment;
a non-null empty annotation is retained as an explicit empty attachment. Neither subtracts
outer labels. Opening a scope does not log anything or change registrations.

Read the completed `LogContext` from a permitted event or contextual recipient:

| Readback | Meaning |
| --- | --- |
| `context.EventTimestampUtc` | Getter-only `DateTimeOffset` with offset zero, captured once for this call before recipient callbacks. |
| `context.Scopes` | Every frame, outermost first, including inherited, empty and unlabeled frames. Each exposes `Annotations` and `Properties`. |
| `context.Labels.EntryAnnotations` | This entry's explicit attachment, or null. |
| `context.Labels.ScopeAnnotations` | The ordered non-null annotation projection of `Scopes`; empty attachments and repeated uses remain separate. |
| `context.Labels.Origins` | One record per occurrence, scope attachments first then entry; each has `Label`, `ScopeIndex` and `OccurrenceIndex`. |
| `context.Labels.EffectiveLabels` | The complete ordinal-identity union of all origins, with duplicates removed; ordering is unspecified. |

All selected contexts and supplied text records from one originating call share that UTC value,
including same-call internal forwarding. Retained contexts keep it after scope exit. Recursive
Logger calls and supplied-base public direct calls sample anew; distinct or increasing values
and cross-thread ordering are not promised. This is event time, not filename or output time.

`ScopeIndex` is zero-based into **ScopeAnnotations**, not **Scopes**; null identifies the entry.
`OccurrenceIndex` is zero-based within that attachment's `LabelOccurrences`. Empty attachments
occupy projection positions but contribute no origins. Inherited-only labels count as labeled.
Context/frame/origin values are library-created, with no public constructors or ambient-capture
factory; readback is data, not reusable delivery permission.

Properties are copied membership, not a dictionary or a deep clone. Pair order, duplicate keys,
null/empty/whitespace keys, default pairs and original values/references survive. Later source-list
changes and scope exit cannot alter captured membership; nested objects can still change.
Logger does not inspect getters, enumerate nested values, call their `ToString`, classify them
or strip capabilities already reachable through them. Retaining recipients own safe retention
and later cleanup. See [LogScopeFrame.cs](ProphetsWay.Logger/LogScopeFrame.cs) and
[LogContext.cs](ProphetsWay.Logger/LogContext.cs).

Use `using` blocks and dispose each handle only in the opening operation or its normal
continuation, including across `await`. Normally captured child work inherits active frames;
its own scope additions/cleanup do not change parent or sibling frames. A child can outlive
parent scope exit and retain the captured labels. Deliberately suppressing execution-context
flow prevents inheritance by newly queued work, without clearing the caller or earlier captures.
Explicitly passed objects still require your classification.

Cleanup removes the handle's frame when current, throws `InvalidOperationException` without
mutation when it is below another active frame, and is a no-op when locally absent, including
repeat cleanup after unrelated scopes open. Handles are non-transferable: do not delegate their
disposal to independent work. There is no creator-ID/fork workflow or promise to detect every
inherited-handle misuse. Supported inner scopes cannot remove inherited labels; deliberate or
accidental handle misuse is not sandboxed. Ending a scope disposes neither destinations nor
property values and does not revoke or erase captures. This is the accepted D024 discipline in
[docs/decision-log.md](docs/decision-log.md#d024---conventional-scope-handles-and-deliberate-flow-suppression).

### Policy Membership

`LabelFilterMode` is **not** a flags enum. Construct a policy with exactly one mode and a
non-null sequence of non-null labels. `Mode` is getter-only; `Labels` is an owned
`ReadOnlyCollection<SensitivityLabel>` containing one representative per configured identity,
even for `NoFilter`. Later changes to the source collection cannot change the policy.

| Mode | Result for valid effective labels |
| --- | --- |
| `NoFilter = 0` | Always true, including unlabeled input; still validates input. |
| `Exclude = 1` | True only when no effective identity is configured. Any intersection denies. |
| `AllowOnly = 2` | True only for nonempty effective membership wholly contained in configured membership. |

Empty Exclude permits every valid input; empty AllowOnly denies every valid input. Duplicates
and ordering do not change matching. Unknown valid identities participate normally.
When calling `Allows` yourself, supply all effective labels, including inherited ones: the
predicate alone does not collect scopes or origins. Logger supplies the full captured union
when enforcing registration and supplied-destination policies.

### Registration Settings

`DestinationRegistrationSettings(bool enabled, LogLevels reportingLevel, DestinationLabelPolicy labelPolicy)`
requires all three arguments and exposes getter-only `Enabled`, `ReportingLevel` and `LabelPolicy`.
Pass it to `Logger.AddDestination(destination, settings)` or its exact-`T` overload. The original
one-argument add means enabled, registration mask `Trace` (63), and empty `NoFilter`: no added
restriction, not an override of the recipient's own severity mask.

`Logger.SetDestinationSettings(destination, settings)` and its generic form replace all settings
atomically on an existing registration, preserving its insertion position. They are not upserts.
Existing captures keep their old settings; later captures, including recursive logging, see
published replacements. Sharing a settings value across routes does not couple future replacements.

An enabled registration suppresses fallback even when its mask is zero or its policy rejects
everything. A disabled registration retains its position and duplicate identity but is neither
attempted nor counted for fallback suppression. Disabling the last enabled recipient can therefore
reactivate existing fallback behavior; disabling and rejecting are not interchangeable.

Supplied destinations also expose `LabelPolicy`, initially empty `NoFilter`, through
`LoggingDestinationCore`. Replacing it atomically changes that destination's own restriction,
including direct calls, not its registration settings. Delivery must pass the registration mask,
recipient severity permission, registration policy and supplied policy. Logger captures supplied
policies before callbacks too; an earlier callback cannot change a later recipient's checks for
the current call. No global transaction across separate destination objects is promised.

## API Reference

The tables summarize the current declarations, not proposed APIs.

| Member | Current use |
| --- | --- |
| `SensitivityLabel(string identifier)` | Create an immutable identity; read `Identifier`, compare with `Equals`, use `GetHashCode` for collections. |
| `LogAnnotations(IEnumerable<SensitivityLabel> labels)` | Copy one attachment's ordered label occurrences, including repeats; empty input is valid. |
| `LogAnnotations.LabelOccurrences` | Getter-only `ReadOnlyCollection<SensitivityLabel>`; captured occurrence count and order stay fixed. |
| `DestinationLabelPolicy(LabelFilterMode mode, IEnumerable<SensitivityLabel> labels)` | Required mode and configured membership; no default constructor. |
| `DestinationLabelPolicy.Mode` / `Labels` | Read the selected mode and copied, unique, read-only membership. |
| `DestinationLabelPolicy.Allows(IEnumerable<SensitivityLabel> effectiveLabels)` | Synchronous Boolean membership decision; no output operation. |
| `Logger.AddDestination` / `RemoveDestination` / `ClearDestinations` | Manage plain registrations; generic overloads manage registrations keyed by `T`. |
| `Logger.ConfigureAutomaticFileHostDirectory(string directory)` | Set only the automatic file's primary directory before initial establishment starts; no filesystem I/O or recipient registration. |
| `ILoggerMetadata` / `MetadataExtensions` | Optional empty marker and the six metadata extension helpers below. |

### Native Scope And Delivery Members

These producer methods belong to `Logger`; generic `T` is unconstrained. The settings and
context types are in `ProphetsWay.Utilities`.

| Member | Current use |
| --- | --- |
| `BeginScope(LogAnnotations annotations)` | Return a `LogScopeHandle`; null opens an unlabeled frame with empty properties. |
| `BeginScope(LogAnnotations annotations, IEnumerable<KeyValuePair<string, object>> properties)` | Capture the complete property sequence before opening the frame. |
| `LogAnnotated(LogAnnotations annotations, LogLevels level, string message = null, Exception ex = null)` | Ordinary raw entry with optional explicit annotations and mask 1-63. |
| `LogAnnotated<T>(LogAnnotations annotations, LogLevels level, T metadata, string message = null, Exception ex = null)` | Exact-`T` raw entry; metadata remains separate from annotations. |
| `AddDestination(ILoggingDestination newDest, DestinationRegistrationSettings settings)` | Register an ordinary borrowed recipient with explicit settings. |
| `AddDestination<T>(ILoggingDestination<T> newDest, DestinationRegistrationSettings settings)` | Register only on the declared metadata route. |
| `SetDestinationSettings(ILoggingDestination destination, DestinationRegistrationSettings settings)` | Replace an existing ordinary registration's complete settings. |
| `SetDestinationSettings<T>(ILoggingDestination<T> destination, DestinationRegistrationSettings settings)` | Replace settings only on that exact-`T` route. |
| `LoggingDestinationCore.LabelPolicy` | Get/replace the supplied destination's additional immutable policy, including direct delivery. |
| `IContextLoggingDestination.LogWithContext(LogContext context, LogLevels level, string message = null, Exception ex = null)` | Optional ordinary full-context receiver. |
| `Generics.IContextLoggingDestination<T>.LogWithContext(LogContext context, LogLevels level, T metadata, string message = null, Exception ex = null)` | Optional exact-`T` full-context receiver. |
| `Logger.DispatchFailed` / `LogDispatchException.Report` | Observe bounded original-failure facts; see the distinct return/throw policies below. |

`LogAnnotated` uses raw rules: null message/exception and null annotations are valid, even at
ErrorOnly or Critical. It is not a convenience helper with their required arguments. Null/default
metadata is valid, and even `T = LogAnnotations` is only metadata, never an implicit label source.
Use an explicit generic invocation when inference would not select the route you intend.

### All Eighteen Helper Forms

All helpers return `void`. Ordinary and typed methods belong to `ProphetsWay.Utilities.Logger`;
typed `T` is unconstrained. Extensions belong to `ProphetsWay.Utilities.Generics.MetadataExtensions`
and each has only `where T : ILoggerMetadata`. Parameter order and optional defaults are:

| Ordinary Logger method | Typed Logger method | Metadata extension declaration |
| --- | --- | --- |
| `Trace(string message)` | `Trace<T>(string message, T metadata)` | `Trace<T>(this T metadata, string message)` |
| `Debug(string message)` | `Debug<T>(string message, T metadata)` | `Debug<T>(this T metadata, string message)` |
| `Info(string message)` | `Info<T>(string message, T metadata)` | `Info<T>(this T metadata, string message)` |
| `Warn(string message, Exception ex = null)` | `Warn<T>(string message, T metadata, Exception ex = null)` | `Warn<T>(this T metadata, string message, Exception ex = null)` |
| `Error(Exception ex, string message = null)` | `Error<T>(Exception ex, T metadata, string message = null)` | `Error<T>(this T metadata, Exception ex, string message = null)` |
| `Critical(Exception ex, string message)` | `Critical<T>(Exception ex, T metadata, string message)` | `Critical<T>(this T metadata, Exception ex, string message)` |

Trace/Debug/Info/Warn require a non-null message. Warn's exception may be omitted or null.
Error requires a non-null exception; its context message may be omitted or null.
Critical requires both exception and message. Empty and whitespace-only messages are valid
and are not trimmed or replaced. Required-null checks run before routing, eligibility checks,
rendering or fallback effects, even when every destination would reject the entry.

Metadata is a required argument, **not a required non-null value**: null and `default(T)` are
valid. Extension receivers may also be null or value-type marker implementations. Destinations
must handle the metadata values they accept; Logger does not clone or transform them.

### Destinations

| Destination or extension point | Current use |
| --- | --- |
| `ConsoleDestination` | Text output; reporting level defaults to `Debug`, excluding Trace. |
| `FileDestination` | Required filename; defaults: `Debug` (excluding Trace), `resetFile: false` (append), `EncodingOptions.UTF8`; no added BOM. |
| `EventDestination` | Required reporting level; subscribe to `LoggingEvent`. |
| `GenericEventDestination<T>` | Typed callback; event arguments also expose `Metadata`. |
| `ILoggingDestination` / `ILoggingDestination<T>` | `Log` receives severity, optional message/exception, and metadata for the generic form; `IDestination` supplies `ValidateMessageLevel`. |
| `BaseLoggingDestination` / `BaseLoggingDestination<T>` | Pass a reporting level to the base constructor and override protected `LogCore`; public `Log` and `LogWithContext` are nonvirtual guards. |
| `LoggingDestinationCore` | Shared severity/exception handling and the protected value-formatting hook. |
| `TextBasedDestination` / `GenericTextBasedDestination<T>` | Ordinary/exact-`T` text bases; render captured context, then call your protected `PrintLogEntry(string message)` override once with no terminator. The typed base adds metadata. |
| `LoggingDestinationCore.FormatValue(object value)` | Protected virtual `string` hook for metadata/property values; return unescaped text or null, or delegate to the closed scalar default. |
| `LogContext.EventTimestampUtc` | Immutable call-time UTC readback; distinct from event arguments' legacy local `Timestamp`. |

For a reusable application-specific destination, including a database destination, implement
your own storage behavior behind the appropriate base. The new ordinary hook is
`protected override void LogCore(LogContext context, LogLevels level, string message, Exception ex)`;
the generic hook is
`protected override void LogCore(LogContext context, LogLevels level, T metadata, string message, Exception ex)`.
Neither hook declares optional arguments. This replaces the old public `Log` override and
is a source and binary break for subclasses: rebuild and move the output body to the matching
hook, using its supplied context. Existing direct callers retain the public `Log` signatures
and optional null message/exception defaults. Independent implementations of the old destination
interfaces need not adopt a context interface.
No database context, schema, or `WriteLogRecord` API is provided by this library.

The core and these bases/event destinations accept `LogLevels reportingLevel`,
`string strReportingLevel`, or `int intReportingLevel` constructors. Console/file constructors
forward the same mask rules. `ValidateMessageLevel(LogLevels messageLevel)` queries eligibility
without dispatching or changing the captured mask. There is no public raw `Logger.Log` method;
`Logger.LogAnnotated` is the raw producer API, while destination `Log` methods are direct entrypoints.

## Common Scenarios

### Keep Detailed And Warning Logs

Choose paths whose contents and access you control. This example **writes files** beneath
local application data and explicitly appends. With the explicit `Trace` mask, `Debug.log`
receives all six severities; `Warnings.log` receives Warning/Error/Critical.

> **Illustrative** — not currently present in the repo.

```csharp
using System;
using System.IO;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;

public static class Program
{
    public static void Main()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProphetsWay.Logger.Example");
        var detailed = new FileDestination(Path.Combine(directory, "Debug.log"),
            LogLevels.Trace, resetFile: false);
        var warnings = new FileDestination(Path.Combine(directory, "Warnings.log"),
            LogLevels.Warning, resetFile: false);
        Logger.AddDestination(detailed);
        Logger.AddDestination(warnings);
        try
        {
            Logger.Trace("Trace detail.");
            Logger.Debug("Detailed diagnostic.");
            Logger.Warn("Check configuration.");
            Logger.Critical(new InvalidOperationException("Example failure."), "Cannot continue.");
        }
        finally
        {
            Logger.RemoveDestination(detailed);
            Logger.RemoveDestination(warnings);
        }
    }
}
```

For Debug/Information only, choose `LogLevels.DebugOnly | LogLevels.InformationOnly`.
All three existing `FileDestination` overloads now default to `resetFile: false`; the example
spells out that append choice. Explicit `resetFile: true` deletes only the selected existing
file during construction, not on each write. Use it only for an intentional reset. Construction
prepares missing parent directories but does not create a missing log file; eligible output
creates it. Constructor preparation is separate from per-entry severity and label selection.

The fourth parameter, `encoder`, accepts `FileDestination.EncodingOptions.ASCII`,
`BigEndianUnicode`, `Unicode`, `UTF8` (the constructor default), or `UTF32`.
`default(FileDestination.EncodingOptions)` is ASCII, not the optional UTF8 default.
Each write appends the selected BCL encoding's bytes for the completed native record followed
by exactly one `Environment.NewLine`. It adds no leading newline, extra blank line or encoding
preamble/BOM. Encoding replacement follows the selected BCL encoding; ASCII does not promise
arbitrary Unicode round-tripping.

Appending leaves all existing bytes untouched, including any existing BOM. It does not inspect
or transcode old content, detect binary or incompatible encoding, or insert a corrective separator
after an unterminated prefix. You choose compatible existing content and encoding.
See [Explicit File Output](#explicit-file-output) for path and failure boundaries. The behavior
is exercised in [FileDestinationTests.cs](ProphetsWay.Logger.Test/FileDestinationTests.cs),
which remains deliberately separate from the default local check below.

### Receive Messages In A Callback

Subscribe before registering the destination. Callbacks run synchronously on the logging
thread; marshal to your UI thread in your own handler when needed. Event arguments expose
`Message`, `RawMessage`, `Exception`, `LogLevel`, `Timestamp`, and `Context`.

> **Illustrative** — not currently present in the repo.

```csharp
using System;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;

public static class Program
{
    public static void Main()
    {
        var destination = new EventDestination("Critical, InformationOnly");
        destination.LoggingEvent += (sender, entry) =>
            Console.WriteLine($"{entry.LogLevel}: {entry.RawMessage}");
        Logger.AddDestination(destination);
        try
        {
            Logger.Info("Ready.");
            destination.Log(LogLevels.Critical | LogLevels.InformationOnly, "Composite.");
        }
        finally
        {
            Logger.RemoveDestination(destination);
        }
    }
}
```

The first callback prints `InformationOnly: Ready.` The direct call also passes: its full
mask 9 is present in the destination's mask 9. It would be rejected by `InformationOnly = 8`.
The callback receives that complete composite mask, not a single-bit reduction.
The registration/event pattern is also in
[EventDestinationTests.cs](ProphetsWay.Logger.Test/EventDestinationTests.cs).

Both event argument types expose non-null `Context` on actual library delivery, even with no
scopes or labels. Their existing public constructors leave `Context` null and perform no ambient
capture. Normal `LoggingEvent` multicast rules remain: a throwing handler stops that invocation
and counts as one output failure; other independent destinations are still attempted. This differs
from the individually contained subscribers of the safe `Logger.DispatchFailed` event.

### Carry Metadata And Use Extension Calls

Typed logging accepts your application object; it does not require `ILoggerMetadata`.
Implement that empty marker only when you want the convenience extension calls as well.
This example replaces a hypothetical database writer with an actual typed event destination.

> **Illustrative** — not currently present in the repo.

```csharp
using System;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.Generics;
using ProphetsWay.Utilities.LoggerDestinations;

public sealed class DbMetadata : ILoggerMetadata
{
    public int UserId { get; set; }
    public string EntryPointMethod { get; set; }
    public object Permissions { get; set; }
}

public static class Program
{
    public static void Main()
    {
        var destination = new GenericEventDestination<DbMetadata>(LogLevels.Trace);
        destination.LoggingEvent += (sender, entry) =>
            Console.WriteLine($"{entry.Metadata.UserId}: {entry.RawMessage}");
        Logger.AddDestination(destination);
        var metadata = new DbMetadata
        {
            UserId = 42, EntryPointMethod = nameof(Main), Permissions = "Synthetic example"
        };
        try
        {
            Logger.Trace("Routing detail.", metadata);
            Logger.Debug("Starting.", metadata);
            metadata.Info("Finished.");
            Logger.Error(new InvalidOperationException("Example failure."), metadata, "Stopped.");
            metadata.Critical(new InvalidOperationException("Critical failure."), "Cannot continue.");
        }
        finally
        {
            Logger.RemoveDestination(destination);
        }
    }
}
```

Expected output is `42: Routing detail.`, `42: Starting.`, `42: Finished.`, `42: Stopped.`,
then `42: Cannot continue.` Metadata remains available as an object; this callback chooses
what to print. See the real call patterns in
[GenericLoggerTests.cs](ProphetsWay.Logger.Test/GenericLoggerTests.cs) and
[MetadataLoggerTests.cs](ProphetsWay.Logger.Test/MetadataLoggerTests.cs).

Ordinary explicit registrations and every exact declared-`T` route are independent, including
interface types such as `T = ILoggingDestination`. Typed routing does not search base types,
implemented interfaces, or the metadata object's runtime subtype. If that exact type has no
enabled registered destination, its automatic recipient uses the native typed renderer and the
same session file as ordinary automatic output. It retains metadata, including null/default values,
with the original annotation, native scopes and UTC event time. Rendering still follows the
[closed scalar rules](#scalars-and-explicit-formatting), not arbitrary object serialization.
Typed calls never forward to ordinary registrations. Any enabled compatible explicit recipient
suppresses only its own route's automatic output, even when it rejects or fails; another route
can still use the shared file.

### Implement A Guarded Custom Destination

Move an old public `Log` override into protected `LogCore`. Keep formatting/output inside that
hook so the supplied guards run first, and use its context instead of recapturing ambient state.
This callback sink needs no files or external service; its added constructor belongs to the
example, not the library. The base requires a reporting level and has no parameterless constructor.

> **Illustrative** — not currently present in the repo.

```csharp
using System;
using ProphetsWay.Utilities;

public sealed class CallbackDestination : BaseLoggingDestination
{
    private readonly Action<LogContext, LogLevels, string, Exception> _write;

    public CallbackDestination(LogLevels reportingLevel,
        Action<LogContext, LogLevels, string, Exception> write)
        : base(reportingLevel)
    {
        if (write == null)
            throw new ArgumentNullException(nameof(write));
        _write = write;
    }

    protected override void LogCore(LogContext context, LogLevels level,
        string message, Exception ex)
    {
        _write(context, level, message, ex);
    }
}

public static class CustomDestinationExample
{
    public static void Run()
    {
        var personal = new SensitivityLabel("PersonalData");
        var destination = new CallbackDestination(LogLevels.Trace,
            (context, level, message, exception) =>
                Console.WriteLine("{0}: {1} ({2} labels)",
                    level, message, context.Labels.EffectiveLabels.Count));
        destination.LabelPolicy = new DestinationLabelPolicy(
            LabelFilterMode.Exclude, new[] { personal });
        Logger.AddDestination(destination);
        try
        {
            using (Logger.BeginScope(new LogAnnotations(new[] { personal })))
            {
                Logger.Info("Withheld before the callback.");
                destination.Log(LogLevels.InformationOnly, "Direct call also withheld.");
            }
            Logger.Info("Synthetic public message.");
        }
        finally
        {
            Logger.RemoveDestination(destination);
        }
    }
}
```

The generic base uses the same pattern, adding `T metadata` between `level` and `message`
in `LogCore`. Neither hook has optional parameters; the inherited public `Log` methods still
default `message` and `ex` to null. `MassageLogStatement` and `PrintLogEntry` overrides retain
their existing signatures. Hook or callback exceptions are output failures handled by the
invoking guarded boundary, not a request to retry or activate another output.

### Capture A Text Record In Memory

Override `PrintLogEntry` to receive a fully rendered ordinary record without a terminator.
This sink stores one string; it neither opens a file nor writes to the console. Explicit
registration avoids fallback, but any other registered recipients can still receive the call.
Use these in-memory examples with one synchronous producer and no unrelated recipients;
they are not concurrent storage implementations.

> **Illustrative** — not currently present in the repo.

```csharp
using System.Collections.Generic;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;

public sealed class RecordingTextDestination : TextBasedDestination
{
    public RecordingTextDestination() : base(LogLevels.Trace)
    {
    }

    public string Record { get; private set; }

    protected override void PrintLogEntry(string message)
    {
        Record = message;
    }
}

public static class TextRecordExample
{
    public static string Run()
    {
        var destination = new RecordingTextDestination();
        Logger.AddDestination(destination);
        try
        {
            var label = new SensitivityLabel("Internal");
            var properties = new[]
            {
                new KeyValuePair<string, object>("Step", "Receive"),
                new KeyValuePair<string, object>("Step", "Validate"),
                new KeyValuePair<string, object>(null, null)
            };
            using (Logger.BeginScope(new LogAnnotations(new[] { label, label }), properties))
            {
                Logger.LogAnnotated(new LogAnnotations(new SensitivityLabel[0]),
                    LogLevels.InformationOnly, message: "First line.\nSecond line.");
            }
            return destination.Record;
        }
        finally
        {
            Logger.RemoveDestination(destination);
        }
    }
}
```

On delivery, the returned string has one escaped message token, `entryLabels=[]`, and one
scope with `labels=["Internal","Internal"]` and
`properties=[("Step","Receive"),("Step","Validate"),(null,null)]`. No duplicate is flattened.
A null returned record means no print occurred; normal return alone is not proof of delivery.
The public example types above are application code, not additional library APIs.

### Extend Typed Value Formatting

Derive from `GenericTextBasedDestination<T>` to retain exact-`T` routing and render metadata.
This example explicitly reads two known application properties and delegates other values to
the default formatter. Return ordinary text: the base quotes and escapes it afterward, including
the newline and quotes in `Stage`. The same hook formats scope values.

> **Illustrative** — not currently present in the repo.

```csharp
using System.Collections.Generic;
using System.Globalization;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;

public sealed class OrderMetadata
{
    public int OrderId { get; set; }
    public string Stage { get; set; }
}

public sealed class OrderTextDestination : GenericTextBasedDestination<OrderMetadata>
{
    public OrderTextDestination() : base(LogLevels.Trace)
    {
    }

    public string Record { get; private set; }

    protected override string FormatValue(object value)
    {
        var order = value as OrderMetadata;
        if (order != null)
            return string.Format(CultureInfo.InvariantCulture,
                "Order {0}: {1}", order.OrderId, order.Stage);

        return base.FormatValue(value);
    }

    protected override void PrintLogEntry(string message)
    {
        Record = message;
    }
}

public static class FormatterExample
{
    public static string Run()
    {
        var destination = new OrderTextDestination();
        Logger.AddDestination<OrderMetadata>(destination);
        try
        {
            var metadata = new OrderMetadata { OrderId = 42, Stage = "Queued\n\"Review\"" };
            var properties = new[]
            {
                new KeyValuePair<string, object>("Attempt", 2),
                new KeyValuePair<string, object>("Unformatted", new object())
            };
            using (Logger.BeginScope(null, properties))
            {
                Logger.LogAnnotated<OrderMetadata>(null, LogLevels.InformationOnly,
                    metadata, message: "Synthetic order.");
            }
            return destination.Record;
        }
        finally
        {
            Logger.RemoveDestination<OrderMetadata>(destination);
        }
    }
}
```

The metadata token is `"Order 42: Queued\n\"Review\""`; the scope values are `"2"` and
`"[no formatter: Object]"`. Without the override, this metadata would be
`"[no formatter: OrderMetadata]"`, not an automatic property dump. These are expected tokens,
not captured program output. The sink has the same single-producer/registration assumptions
as the previous example. No `ILoggerMetadata` marker or formatter registration is needed.
See [TextRenderingTests.cs](ProphetsWay.Logger.Test/TextRenderingTests.cs) for the real
in-memory text, explicit formatter, rejection and escaping specifications.

## Behavior And Limitations

### Native Argument Errors

These errors apply to the core and its forwarding constructors, helpers, registrations,
and supplied direct `Log` implementations, as indicated. Valid rejection is not an error.

| Invalid input | Exception | `ParamName` |
| --- | --- | --- |
| Enum destination mask outside 0-63 | `ArgumentOutOfRangeException` | `reportingLevel` |
| Integer destination mask outside 0-63 | `ArgumentOutOfRangeException` | `intReportingLevel` |
| Null string destination mask | `ArgumentNullException` | `strReportingLevel` |
| Any other invalid string mask, including parsed negative/unknown bits or overflow | `ArgumentException` | `strReportingLevel` |
| Raw mask outside 1-63 passed to `ValidateMessageLevel` | `ArgumentOutOfRangeException` | `messageLevel` |
| Raw mask outside 1-63 passed to a supplied destination's `Log` | `ArgumentOutOfRangeException` | `level` |
| Null required helper message or exception | `ArgumentNullException` | `message` or `ex` |
| Null ordinary or typed `AddDestination` argument | `ArgumentNullException` | `newDest` |
| Same object reference added again on the same ordinary or exact-`T` route | `ArgumentException` | `newDest` |
| Null registration settings on add or replacement | `ArgumentNullException` | `settings` |
| Null/absent destination on settings replacement | `ArgumentNullException` / `ArgumentException` respectively | `destination` |
| Null policy in settings construction / supplied policy assignment | `ArgumentNullException` | `labelPolicy` / `value` respectively |
| Null property sequence for `BeginScope` | `ArgumentNullException` | `properties` |
| Invalid raw mask for `LogAnnotated` | `ArgumentOutOfRangeException` | `level` |
| Null/noncurrent context on a supplied `LogWithContext` call | `ArgumentNullException` / `ArgumentException` respectively | `context` |

No competing-error priority is promised when both Critical arguments are null. Exception
message text is not a configuration contract. Equivalent valid mask representations have
the same eligibility, but invalid strings deliberately use different errors from invalid enums/integers.

### Dispatch And Direct Calls

For enabled ordinary and exact-`T` registrations, every registration and supplied-destination
restriction must permit the full captured entry before handoff. Logger invokes a custom
`ValidateMessageLevel` callback at most once per attempted recipient and requires true; an
earlier rejection may skip it. No ordering among rejecting checks is promised. Label denial
withholds message, exception, metadata and the complete context, including scope properties,
before recipient-owned formatting, hooks, callbacks or export. Permitted recipients continue.

A recipient implementing the optional `IContextLoggingDestination` or exact-`T` counterpart
receives one `LogWithContext` handoff instead of the legacy `Log`, never both. Independent
implementations of the old interfaces remain valid and receive no new context parameter.
Supplied bases deliver once through Logger's internal captured path to `LogCore`, without
recapturing callback-mutated scopes or repeating policy checks/reporting. That path is internal,
not a consumer API or reusable permission token; public reentry always starts a new guarded attempt.

The supplied bases' public `Log` and `LogWithContext` are **nonvirtual guards**, inherited by
event/text/console/file destinations. Direct `Log` captures active frames with no entry annotation
and checks the destination's own mask and `LabelPolicy`, not registration settings. A valid
mismatch returns without massage, hook, event construction, text composition, output or fallback.
Invalid masks throw before recipient work, even without event subscribers.

Direct `LogWithContext` requires the same ordered scope openings currently active, including empty
and unlabeled frames. Equal labels/properties from a different opening are insufficient. Both
empty frame lists match; the supplied entry annotation may be non-null. Reuse while those openings
remain current still rechecks current destination configuration. Added, ended or replaced frames
make the context noncurrent and cause a local argument error before output. Possession or earlier
successful delivery is not authorization. Arbitrary independent direct implementations, hidden or
reimplemented members, and consumer methods calling their own hooks are not sandboxed.

An accepted supplied-base `LogWithContext` invocation delivers a newly stamped context with
that direct call's UTC time and the supplied immutable scope/label facts, including entry
annotations. It does not mutate the retained context or replace its facts with current labels.
Timestamp equality is not part of the current-opening check and grants no delivery permission.

Direct/raw content is distinct from helper preconditions: message and exception may each be
absent, even at Critical or ErrorOnly. Typed metadata may still be null/default. For accepted
callbacks, `RawMessage`, `Exception`, the complete `LogLevel` mask, and typed `Metadata` retain
their supplied values/references. `Message` contains the massaged text, not a replacement raw
payload; Error's omitted context remains null in `RawMessage`.

Without an exception, the base `MassageLogStatement` returns the original message, including
null, empty or whitespace. With an exception, it preserves context and includes exception and
nested-exception messages and available stack traces **at every valid mask**, including warnings
and composites. It does not serialize exception `Data`, redact content, or clone raw objects.
`Timestamp` remains each event carrier's construction-time local `DateTime.Now`, not a shared UTC
timestamp. Use the delivered `Context.EventTimestampUtc` for shared call time instead. Event
`Message` and `RawMessage` are not text-record tokens: message/exception text stays unescaped,
and metadata and scope values retain original references. Accepted raw objects are not sanitized
reporting objects; framing a neighboring text destination does not make a callback safe to export.

### Label And Policy Arguments

| Invalid input | Exception | `ParamName` |
| --- | --- | --- |
| Null identifier | `ArgumentNullException` | `identifier` |
| Empty, overlong, whitespace-containing or control-containing identifier | `ArgumentException` | `identifier` |
| Mode other than 0, 1, or 2, including combined value 3 | `ArgumentOutOfRangeException` | `mode` |
| Null configured/effective sequence | `ArgumentNullException` | `labels` / `effectiveLabels` respectively |
| Null element anywhere in a finite sequence | `ArgumentException` | `labels` / `effectiveLabels` respectively |

`NoFilter` and a decisive match do not bypass finite-input validation. Sequence-access failure
completes exceptionally, without a usable partial policy or Boolean result. Exact foreign
exception details and competing-error precedence are unspecified. Supplied sequence code can
have side effects; the policy does not sandbox it, undo those effects, or bound nonterminating inputs.

Keep inputs stable during capture. Repeated/concurrent evaluation of unchanged valid membership
preserves policy state and results, but concurrent producer mutation is unsupported. Readback order,
collection-reference identity, and representative label-reference identity are unspecified.
Mutation through readback collection interfaces throws `NotSupportedException`.

### Annotation Capture And Readback

Supply a finite, stable, non-null sequence of non-null labels to `LogAnnotations`.
Null `labels` throws `ArgumentNullException`; a null element anywhere throws `ArgumentException`.
Both name `labels` in `ParamName`; exception wording is not promised.

Sequence access includes enumerator acquisition, iteration, element access and disposal.
If any fails, construction produces no usable partial value. Foreign sequence errors are local
construction errors, not sanitized dispatch reports; their details, competing-error precedence
and access counts are unspecified. Caller code may have side effects: capture does not sandbox
or roll them back, support mutation during capture, or guarantee termination for unbounded input.

After successful construction, `LabelOccurrences` is non-null and supports concurrent reads.
Writes through its collection interfaces throw `NotSupportedException`; its `ICollection.SyncRoot`
does not expose occurrence storage. View and representative label-reference identity are unspecified.
These readback guarantees describe `LogAnnotations` specifically.

Scope-property capture follows the same complete-before-publication discipline, including
enumerator disposal. A null sequence or failed acquisition/iteration/current/disposal publishes
no scope or usable handle and emits no dispatch report. Opening cannot undo side effects in
caller sequence code, bound an infinite input or support concurrent producer mutation. Scope
cleanup errors are local errors too, not `LogDispatchException` or `DispatchFailed` notifications.

### Failure Reports And Return Policy

On explicit or automatic routes and supplied guarded direct calls, failure handling depends on the observed
boundary, not foreign exception text or type. Valid severity/label rejection is not a failure and
does not activate fallback. Independent recipients are attempted once in captured order, without
rollback or retries; another recipient succeeding does not cancel a mandatory failure.

| Original failure | Safe fact | Result after independent attempts and reporting |
| --- | --- | --- |
| Logger-invoked severity callback throws | `LogFailureStage.Eligibility = 1` | Throw `LogDispatchException`. |
| Selected legacy/contextual invocation, delivery hook, rendering or callback throws | `LogFailureStage.Output = 2` | Throw `LogDispatchException`. |
| Automatic initial establishment fails at both locations, remembered initial failure, or automatic output fails | One `LogFailureStage.Output` descriptor at the implicit slot after all captured explicit slots; no core-capture failure | Throw `LogDispatchException`; qualifying initial secondary success is normal success. |
| Opted-in label-policy evaluation unexpectedly fails | `LogFailureStage.LabelCheck = 3` | Withhold that recipient, report and return unless a mandatory failure also occurred. |
| Shared entry/context capture unexpectedly fails | `CoreCaptureFailureCount = 1`, no recipient descriptor | Withhold the incomplete entry from all affected recipients, report and return. |

No strict-mode selector is supplied by these APIs. A normal return proves neither delivery nor
successful capture/checks. Value/scope construction and argument errors remain local exceptions,
not fabricated dispatch failures. A mandatory failure still throws if its descriptor falls beyond
the retained limit; result selection is not inferred from the truncated descriptor list.

If neither initial automatic location is usable, Logger remembers a safe failure marker, not
the raw causes. Later default-dependent calls get fresh reports/correlations and exceptions without
new path or permission probes. `LogDispatchException.Message`, `ToString()` and best-effort stderr
give fixed guidance to make an appropriate default location writable or configure a compatible
destination, never the resolved path or payload. Fixing permissions alone does not retry this session:
use a compatible enabled explicit recipient to bypass failure **only on its route**, or start a fresh
loaded Logger session. Removing/disabling/clearing that recipient exposes the same remembered failure.

`LogFailureReport` contains a generated `CorrelationId`, at most eight immutable `Failures` in
encounter order, `OverflowCount` for further failed recipients, and `CoreCaptureFailureCount`
(zero or one). A core-only report has an empty recipient list and zero overflow. Total failures
are `(long)report.CoreCaptureFailureCount + report.Failures.Count + report.OverflowCount`.
Each descriptor's `RegistrationId` is a one-based position in this call's **full** capture,
including preceding disabled, rejected and successful slots. A supplied direct attempt uses 1;
zero is never a fabricated core recipient. These are not durable registration IDs.

Reports retain no entry, labels, origins, properties, destinations, delegates, raw causes or
control backlinks. Their owned membership remains unchanged across observers and later calls.
`LogDispatchException.Report` carries the same safe facts, with no raw inner cause. Its initial
diagnostic view uses fixed prose, allowed IDs/codes/counts, null `InnerException`/`HelpLink`, empty
`Data`, fixed `Source`, and null `StackTrace`. This does not erase CLR diagnostic state or sanitize
reflection, debugger access, serialization, inherited post-catch mutation or a whole object graph.

`Logger.DispatchFailed` captures subscribers after attempts, invokes each synchronously outside
registry locks, then attempts a safe stderr summary bounded to 512 UTF-16 units including its
terminator. Subscriber/writer failures are contained and add no original failures or escalation.
On the same managed-thread reporting stack, nested logging still runs with its own result policy
but emits no nested event/stderr report; other threads are independent. No timeout, cross-thread
cycle prevention, guaranteed observation or process-fatal containment is promised.

Native capture and policy checks use library-owned membership and sealed label/policy values,
with no ordinary deterministic consumer fault path for the unexpected capture/LabelCheck branches.
Those branches require code review, not a fabricated report presented as execution coverage.
Allocation/runtime faults may prevent reporting itself: **out-of-memory survival is not guaranteed**.
Consumer callbacks and raw payloads are not made safe by these bounded reporting facts.

Text rendering follows the same boundary: a throwing massage, `FormatValue` override or
composition step causes no `PrintLogEntry` call for that recipient. Independent recipients
continue, then an Output failure requires safe reporting and `LogDispatchException`. A print
failure may already have effects; there is no rollback or retry. Valid rejection reaches neither
formatter nor print, and a null formatter result is valid text absence, not a failure.
Neither rendered content, event time, original values nor the formatter's raw exception is
added to `LogFailureReport`. Intended log records are unbounded, potentially sensitive content;
they are not the bounded diagnostic channel. Custom inspection and retention remain your responsibility.

### Current Logging And Text Output

- With no enabled compatible explicit recipient on the captured ordinary or exact-`T` route,
    Logger selects one implicit automatic recipient without registering it or consulting another
    route. It uses `Trace` and no label filter, covering all six bits and valid composite levels.
    Ordinary and typed automatic records share one UTF-8 session file; typed metadata is retained.
    See [Automatic File Output](#automatic-file-output) for location, configuration and failure rules.
- Explicit registration uses reference identity, never recipient equality or hashing. Distinct
    equal objects remain distinct; the same instance may register on different compatible routes.
    Duplicate same-route adds leave membership unchanged. Removal targets only the exact reference;
    null/absent removal is a no-op, and clear affects only the selected route. After removal/clear,
    a successful re-add goes last.
- For a route with explicit recipients, mutations and captures are atomic: each call captures
    one complete insertion-ordered membership and its registration settings before callbacks.
    Native context and supplied policies are also captured before recipient callbacks, without
    promising a global transaction across registry and flow state. Registry locks span no consumer
    code. Callback add/remove/clear/settings changes, including self-removal, affect later captures
    only; an older capture can still call a removed recipient after mutation returns. Recursive
    logging is a new call with a fresh capture.
- Explicit destinations are borrowed: removal/clear never dispose them and do not drain calls.
    Stop producers and await synchronous calls before disposing your recipients. Membership capture
    does not serialize callers or shared recipients, impose cross-thread delivery order, or freeze
    custom recipient state, eligibility results, metadata or exception graphs.
- The failure rules above cover explicit and automatic dispatch and supplied guarded direct attempts,
    not arbitrary independent direct code or a guarantee that all I/O completes. Borrowed lifetime
    and retained-object responsibilities remain.

#### Automatic File Output

Ordinary and every exact-`T` automatic route share one session in the loaded static Logger state.
Separate processes, AppDomains or independently loaded assembly copies do not share that state.
An enabled compatible explicit recipient suppresses only its own route, including rejection or
failure. Removing or disabling the last one resumes the same automatic session, not a new file.

The normal primary directory is the host's `AppContext.BaseDirectory`, not the current working
directory, Logger's assembly location or the `dotnet` installation. To choose a different primary,
configure it during startup before any automatic establishment begins:

> **Illustrative** — not currently present in the repo.

`ProphetsWay.Utilities.Logger.ConfigureAutomaticFileHostDirectory("logs");`

Then, with no enabled ordinary recipient, `ProphetsWay.Utilities.Logger.Trace("Startup");`
uses automatic output. The relative `logs` directory is resolved once against the current directory
**at configuration time** using `DirectoryInfo.FullName`. Configuration creates no directory/file,
checks no permissions and registers no recipient. Later successful configuration calls replace the
override only before initialization; once establishment starts, even the same path is rejected with
`InvalidOperationException`. Null throws `ArgumentNullException`; empty or framework-rejected path
syntax throws `ArgumentException`. Other path-normalization errors remain ordinary local errors,
not sanitized dispatch diagnostics. Failed calls change nothing; invalid-input/state precedence is
unspecified. Null/empty are not reset requests.

Only initial primary establishment failure permits a secondary attempt: one `app-` directory
component followed by the full 64 lowercase SHA-256 hex digits of the normalized host-base key,
directly under `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`.
The key comes from the captured host base, **not the primary override**. Unavailable naming/root
inputs fail that location; Logger does not substitute CWD or another profile. This is host-base
association, not logical-application identity: equal keys share the directory, and physical aliases
need not share a key. It supplies no tenant isolation, ACL protection or confidentiality.

Filesystem establishment waits for a selected, fully rendered record. Initial allocation uses
`Default Log <UTC>-<Guid token>.log`, invariant UTC and exclusive `CreateNew`, leaving occupied
candidates untouched. Once selected, every later call appends or creates a missing file at that
**same pathname**, regardless of later CWD changes or route suppression/reactivation. Each record
is UTF-8 with one `Environment.NewLine` suffix, no added BOM and no leading blank line. Existing
content is neither inspected nor repaired. See the exact path rules in
[AutomaticFilePaths.cs](ProphetsWay.Logger/AutomaticFilePaths.cs) and session behavior in
[AutomaticFileSession.cs](ProphetsWay.Logger/AutomaticFileSession.cs).

File effects are serialized across automatic routes and handles close after each attempt; rendering,
callbacks, registration changes and reporting remain outside that coordination. Once a path is
selected, write/flush/close failures or later open failures cannot select the secondary, replay the
entry or allocate another session filename. A later call is a new attempt at the selected path.
There is no crash-durability or cross-process ordering guarantee, public session reset, disposal,
strict-mode selector or selected-path inspection API. Intended records are not redacted; choose
explicit destinations and filesystem access/retention controls for your deployment.

#### Explicit File Output

[FileDestination.cs](ProphetsWay.Logger/LoggerDestinations/FileDestination.cs) selects its full
pathname once at construction using the framework's `FileInfo` semantics. Relative paths use
that moment's `Environment.CurrentDirectory`; later directory changes do not redirect the instance.
If the file is deleted, a later permitted call can recreate it at that same path. Missing parent
directories are prepared at construction, not repaired on every output attempt. This fixes a
pathname, not a persistent physical-file identity or filesystem sandbox.

Required arguments are validated before deletion or directory creation. An undefined `encoder`
throws `ArgumentOutOfRangeException` naming `encoder` before those effects. Ordinary construction,
preparation and reset failures outside the documented argument mappings propagate as their original
framework exceptions. Their diagnostic contents are not promised sanitized or path-free; construction
produces neither a console echo nor a `Logger.DispatchFailed` notification.

After construction, encoding/open/write/flush/close failures through supplied public direct
entrypoints or Logger delivery are `LogFailureStage.Output` failures. A direct call safely reports
its one-recipient failure and throws `LogDispatchException`. Logger attempts independent eligible
recipients before safe reporting and the mandatory throw; reporter failures cannot suppress or
replace that result. The sink emits no second report. Validly rejected entries never reach rendering
or file output, but rejection does not undo earlier constructor preparation or explicit reset.

Physical writes are synchronous and serialized within one destination instance; each attempt
releases its acquired file handle before completion. Rendering can still run concurrently. There
is no cross-instance/process coordination, promised concurrent-call order, retry, replay, relocation,
global flush or crash-safe/exactly-once/durability guarantee. A failure may follow partial effects;
a later logging call is a new attempt at the same path, not a replay of the failed entry.

#### Record Layout

The supplied text bases render the captured UTC event time in invariant round-trip `O` format
(including `+00:00`), followed by the full severity's general spelling padded left to 12 characters.
The message is massaged once, then quoted and escaped. For `Logger.Debug("Hello World!")` without
scopes or entry annotations, the shape is below. The timestamp placeholder is not literal output
or a recorded execution:

```text
<UTC event time> ::    DebugOnly:  "Hello World!" | entryLabels=null | scopes=[]
```

`GenericTextBasedDestination<T>` adds the literal prefix `" | metadata="` (excluding the quotes)
immediately after the message token, before `entryLabels`, even for null/default metadata.
Ordinary text has no metadata field.
There is no configurable grammar or automatic discovery of arbitrary structured objects.

| Component | Representation |
| --- | --- |
| Absent text / empty text / literal `null` text | `null` / `""` / `"null"`; non-null values are always quoted. |
| Entry or scope annotation | `null` when absent; `[]` when present but empty; otherwise ordered quoted label identifiers, including duplicates. |
| Scopes | `[{labels=attachment,properties=[(key,value),...]},...]`, outermost first, including empty/unlabeled frames. |
| Properties | Ordered pairs, not a dictionary: duplicate keys, null/empty/whitespace keys and original value slots remain separate. Keys are string tokens; values use `FormatValue`. |

Arrays and pairs use commas without added spaces or trailing commas. Entry labels are separate
from scope labels. Text preserves label occurrences rather than exporting the deduplicated
`EffectiveLabels` set; frame positions in `scopes` are not the label-only `ScopeIndex` values.

Every content token is escaped once after formatting, including message and exception detail,
metadata, scope keys/values, label identifiers, unsupported markers and explicit formatter output:

| Input | Emitted text |
| --- | --- |
| Backslash / double quote | `\\` / `\"` |
| CR / LF / TAB | `\r` / `\n` / `\t` |
| Other `Char.IsControl` units and U+2028/U+2029 | `\uXXXX`, with four uppercase hexadecimal digits. |
| Other UTF-16 units | Preserved without normalization or truncation. |

An actual newline becomes `\n`; an existing backslash followed by `n` becomes `\\n`.
Delimiter-looking text stays inside its quoted token. This is reversible text escaping, not
redaction, confidentiality, lossless object serialization or proof of downstream encoding fidelity.

Each text base builds the whole record before one `PrintLogEntry` call. That string is non-null,
single-line and **has no terminator**; the sink owns termination. Console retains its `WriteLine`
handoff. `FileDestination` appends one `Environment.NewLine` suffix using the selected encoding,
as described above. Automatic output uses the same suffix with UTF-8; its separate lifecycle and
initial-only recovery rules are described under [Automatic File Output](#automatic-file-output).

#### Scalars And Explicit Formatting

The default `LoggingDestinationCore.FormatValue(object value)` supports only these values:

| Value | Unescaped scalar text |
| --- | --- |
| Null, string, char, Boolean | Null result, original string/character, or `True`/`False`. |
| SByte/Byte, Int16/UInt16, Int32/UInt32, Int64/UInt64, IntPtr/UIntPtr | Invariant `D`; pointer-sized integers render their numeric value, without dereferencing. |
| Single/Double; Decimal | Invariant `R`; invariant `G`, respectively. Floating special values remain supported. |
| Guid; DateTime/DateTimeOffset; TimeSpan | Invariant `D`; `O`; `c`, respectively. Payload times retain their kind/offset; only the event time is forced to UTC. |
| Enum | General `G` names/representable flag names, otherwise invariant underlying decimal; alias choice follows the BCL. |
| Anything else | `[no formatter: TypeName]`, using runtime `Type.Name`, not a full name or object string. |

Nullable boxing follows the underlying value, or null when empty. Runtime-specific BCL floating
spellings are not promised byte-identical across target frameworks. Dictionaries, enumerables and
annotation-shaped metadata remain values, not automatically traversed structures or label sources.
Default rendering calls no arbitrary getters, object `ToString`, `IFormattable`, equality, hashing
or nested enumeration. It does not deep-clone values.

Override `FormatValue` on your text destination to opt into application-owned inspection. Return
**unescaped** text, delegate unhandled values to `base.FormatValue(value)`, or return null for a
null text token. The renderer escapes every result as data; returning null is not a failure.
Successful text rendering calls the hook once per metadata/property-value occurrence after
eligibility. Duplicate slots are separate calls; call order across values is unspecified, and
results are not cached across recipients. Keys, labels and message/exception text bypass this hook.
Overrides may run concurrently and own their inspection, effects and retained objects.

The rules are implemented by the
[ordinary text base](ProphetsWay.Logger/LoggerDestinations/TextBasedDestination.cs),
[typed text base](ProphetsWay.Logger/LoggerDestinations/GenericTextBasedDestination.cs) and
[formatting hook](ProphetsWay.Logger/LoggingDestinationCore.cs), and specified in
[TextRenderingTests.cs](ProphetsWay.Logger.Test/TextRenderingTests.cs).

#### Earlier Text Layout

The old local-time, unquoted output looked like this; it is retained only as a migration reference,
not current output:

```text
3/15/2019 11:09:33 PM ::    DebugOnly:  Hello World!
```

The existing [example program](ProphetsWay.Logger.Example/Program.cs) constructs nested exceptions.
This historical illustrative error excerpt shows their messages in the earlier multiline layout.
Current text keeps the exception detail inside one escaped message token at every valid mask;
events retain unescaped massaged text. A constructed-but-unthrown exception does not acquire
a stack trace merely because its message mentions one:

```text
3/15/2019 11:25:41 PM ::    ErrorOnly:  Another generic message about an error occuring. (friendly message to show a UI maybe?)
This exception has an inner exception. (likely details to hide from a UI)

Inner Exception Message:
This is a specific Exception Message and will contain a stack trace.
```

### Implemented M3/M4-A And Remaining Boundaries

Native scopes, ordered origins/full properties, effective-label filtering, atomic registration
settings replacement, optional contextual receivers and guarded supplied destinations are implemented.
M4-A adds call-shared UTC time, invariant scalar/explicit value formatting, ordinary and exact-`T`
text bases, and quoted single-line rendering of the message and captured native context.
They extend the existing severity validation, explicit-route isolation, borrowed-recipient lifetime
and independent-attempt reporting rules. This source status is not full-v4 or release certification;
the permanent regression, compiled-example and parent final acceptance gates remain separate.

M4-B1 now implements append-by-default explicit files, construction-only reset, fixed selected paths
and physical record encoding/termination. M4-B2 now integrates the shared automatic session,
primary configuration, typed metadata retention, initial-only recovery, fixed-path reuse and
remembered initialization failure. Its final documentation-compilation and independent code/security
gates remain separate; this is not a claim that the whole B2 acceptance target is complete.
Both Microsoft logging bridges remain outside this native slice (M5). Explicit membership safety
is not a guarantee of all concurrency safety. Microsoft severity conversion/None handling is not
supplied by this native enum. The implemented failure behavior above is not delivery of every policy in
[docs/requirements.md](docs/requirements.md). Requirements readiness is not delivery.

A false policy result is an ordinary mismatch, not an output failure or instruction to activate
fallback. True means only membership permission, not successful delivery, correct classification,
or authorization of a recipient. The library does not automatically classify or redact payloads.
You own label mapping, sanitization, recipient access/authentication, storage, retention, and audit
obligations. Readable identifiers/configuration and consumer sequence exceptions can contain
sensitive information; do not treat them as safe to expose just because the predicate returned.

See the scoped [security review](docs/security/security-review.md),
[threat model](docs/security/threat-model.md), and
[data classification](docs/security/data-classification.md). Their scope is not a confidentiality,
compliance, sandboxing, whole-library security, or publication guarantee.
Any advisory-scan results in that review are dated evidence, not license, bundled-component,
SDK/runtime, or release clearance.

### Breaking Migration From Earlier Logger APIs

Recompile consumers, custom destinations, and code that compares or persists enum values.
Enum constants can be embedded in compiled callers; replacing a library binary does not translate
their old numeric meanings. This is unreleased major-version work, not a patch-safe drop-in update.

- Remove calls to ordinary, typed, and metadata-extension `Security`, plus `Security`/`SecurityOnly`
    configuration names. Choose the appropriate remaining severity for each call. There is no automatic
    security-event/concern replacement. Explicit application labels and policies are independent of
    severity, not an automatic translation of the removed Security level.
- Recalculate saved integer/numeric-string masks from the intended new bits: 1 is Critical, 2 is
    ErrorOnly, 4 is WarningOnly, 8 is InformationOnly, 16 is DebugOnly, and 32 is TraceOnly. Do not
    reuse old numeric configuration without reviewing its meaning. Recheck enum-name configuration too:
    inclusive Error accepts Error/Critical; Debug no longer means all severities. Select Trace for all six.
- Compare emitted Error entries to `ErrorOnly`, not the inclusive `Error` mask. For selection, require
    all requested bits; preserve composite masks rather than reducing them to one severity.
- Supply the formerly optional required extension arguments: messages for Debug/Info/Warn and an
    exception for Error. All three helper families now reject required nulls before dispatch, even if
    the route rejects everything. Critical requires exception and context; Error's context and Warn's
    exception remain optional. Empty/whitespace messages and null/default metadata remain valid.
- Correct malformed mask strings instead of relying on the former Information fallback. Zero is valid
    only as a destination mask; direct/raw zero is an error. Supplied direct destinations now reject
    ineligible messages before formatting/callback/output, so a direct call no longer bypasses selection.
- Deduplicate setup by object reference per route: a repeated add now throws `ArgumentException`
    with `ParamName` `newDest`, rather than adding another delivery. Retain the exact reference for
    removal; an equal but distinct object no longer removes it. Do not treat removal/clear as a drain
    or immediate revocation: already captured calls may still use that borrowed recipient.
- Rebuild subclasses and move public `Log` overrides to the matching protected `LogCore` signature
    shown above. The old virtual/abstract slot is gone: this is a binary as well as source break.
    Do not hide `Log` to bypass guards. Existing callers keep the public method defaults, and independent
    old-interface implementations need not adopt the optional context interface.
- Update report readers for `LabelCheck`, empty recipient lists on core-only failure, and
    core-inclusive totals. Notifications do not all imply an exception; severity/output failures do.
    Event consumers may read `Context` on delivery but must allow null on manually constructed args.
- Migrate text parsers from local culture-dependent, unquoted multiline output to the fixed UTC
    `O` prefix and quoted token grammar above. Parse boundaries before decoding escapes once;
    do not split blindly on delimiters that can occur inside quoted content. Preserve null versus
    empty/literal-null tokens, repeated labels/keys and full scope frames. Exception stacks now
    appear escaped within the message token, not as additional physical lines.
- Keep `MassageLogStatement` unframed and unescaped if you need the supplied text contract:
    text bases frame its result, while events retain it unescaped. Use `FormatValue` for explicit
    metadata/property rendering and `PrintLogEntry` for the completed unterminated record. Review
    existing subclasses for a coincidentally named `FormatValue` member; replacing `LogCore` can
    replace the supplied rendering behavior. These hooks do not certify custom output code.
- Do not reinterpret event `Timestamp` as UTC. Read `Context.EventTimestampUtc` on delivery;
    a public direct reuse samples anew without altering the retained context. Update text readers
    independently from raw/event consumers, whose original fields and local timestamp remain.
- Recompile callers to obtain `FileDestination`'s new omitted `resetFile: false` default on all
    three existing overloads. Previously compiled callers may still pass embedded `true` until
    rebuilt; replacing the library alone does not change those arguments. Explicit `true` still
    deletes the selected existing file during construction. No new reset method or factory is added.
- Update file readers for one suffix terminator per appended record and no newly added BOM;
    existing bytes and unterminated prefixes are not repaired. Reject undefined encodings during
    setup, and handle ordinary constructor errors locally without expecting console echo or safe
    dispatch diagnostics. Guarded output failures still report safely and must throw. These are
    unreleased v4 behavioral changes, not a patch-safe update.
- Review no-setup file assumptions: automatic output now uses the host base or a pre-initialization
    override, initial-only secondary recovery and one shared UTF-8 file with Trace coverage and typed
    metadata. It no longer forwards typed fallback to ordinary recipients. Clearing registrations
    cannot reset the selected path or remembered initial failure.

## Architecture And Design Decisions

| Choice | Trade-off |
| --- | --- |
| Static entry point with configurable destinations | Central setup and short calls, but process-wide state and current registration limitations. |
| Typed metadata separate from message text | Your destination can inspect application data; Logger does not supply a database schema or automatically persist that object. |
| Sealed, application-defined label instead of a closed taxonomy | No registry dependency and no invalid default value-type instance; consumers must agree on exact identities. |
| Immutable copied policy membership | Later caller edits cannot change configuration; changing policy means constructing another policy. |
| Standalone predicate plus native enforcement | Membership remains independently usable; Logger and supplied guards enforce whole-entry selection using the captured union. |
| Immutable membership, original nested objects | Stable scope/settings structure without arbitrary deep cloning, classification or sanitization; consumers own nested-object safety and retention. |
| Conventional non-transferable scope handles | Normal await/child inheritance and local cleanup without a creator-ID/fork protocol or universal misuse detector. |
| Nonvirtual entrypoints and protected output hooks | Selection precedes supplied output even on direct calls, at the cost of a major subclass override migration. |
| Closed scalar formatting plus explicit overrides | Predictable invariant text without arbitrary object inspection; your formatter owns any additional reads and effects. |
| Shared UTC and quoted native records | One call-time fact and preserved content boundaries, at the cost of a breaking text-parser migration; physical storage remains a separate responsibility. |

The accepted direction is recorded in [docs/decision-log.md](docs/decision-log.md).
M4-A implements native rendering/framing and M4-B1 implements the explicit-file sink behavior above;
M4-B2 integrates the automatic session. Final integration gates and the M5 bridges remain separate.

## Building And Testing Locally

Use Windows with a .NET 10 SDK and .NET Framework 4.8 available for the Framework test leg.
Run from the repository root. Build [Logger.sln](Logger.sln) first; it includes the library,
tests, and example. The build restores dependencies if needed. Stop if it fails before testing.

```powershell
dotnet build .\Logger.sln -c Debug
```

The [test project](ProphetsWay.Logger.Test/ProphetsWay.Logger.Test.csproj) uses
`xunit.v3.mtp-off` 4.0.1 through VSTest (`xunit.runner.visualstudio` 4.0.0 and
`Microsoft.NET.Test.Sdk` 18.10.1); both Microsoft Testing Platform runner switches are disabled.
New policy tests use Shouldly 4.3.0. Existing xUnit assertions and FluentAssertions 5.10.3 remain;
this is not a completed assertion migration. Moq 4.20.72 supports destination mocks, and
`coverlet.collector` 6.0.4 supplies coverage.

Use this **focused selection**, sequentially on both targets, with coverage:

```powershell
$filter = @(
    'FullyQualifiedName~ProphetsWay.Logger.Test.SensitivityLabelTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.DestinationLabelPolicyTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.BasicTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.GenericBasicTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.AdvancedTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.GenericAdvancedTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.LoggerTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.GenericLoggerTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.MetadataLoggerTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.ILoggingDestinationTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.LogAnnotationsTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.EventDestinationTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.DestinationRegistrationSettingsTests.'
    'FullyQualifiedName~ProphetsWay.Logger.Test.TextRenderingTests.'
) -join '|'

dotnet test .\ProphetsWay.Logger.Test\ProphetsWay.Logger.Test.csproj -c Debug -f net48 --no-build --no-restore --filter $filter --collect "XPlat Code Coverage"
dotnet test .\ProphetsWay.Logger.Test\ProphetsWay.Logger.Test.csproj -c Debug -f net10.0 --no-build --no-restore --filter $filter --collect "XPlat Code Coverage"
```

This selection includes native scope/context/settings, guarded-destination and text-rendering
specifications, not just the earlier severity and standalone-policy tests. Its discovered count
may change as regressions are added. Every C# example above requires independent compile-only
checking against both library assets; do not execute the programs as part of that check.
This README update does not claim that check, the parent final gate, or release approval passed.

The following figures are **historical selections**, not totals for the current source or rendering checks.

The recorded M2-B Windows check on **2026-09-18 EDT** passed **320/320 cases on each of
`net48` and `net10.0`**, with zero failures/skips: **20 new registration cases** plus the
**original 300 preserved cases** per target. Library and example builds also passed; the
example was not executed. This is focused explicit-registration evidence, not full-suite or
full-v4 certification, fallback/file qualification, independent review approval, or release clearance.

The earlier native Windows check on **2026-09-17 EDT** passed **300 cases per target**,
with zero failures/skips and unchanged same-target case identities. The recorded coverage
exercises all seven changed implementation files plus label/policy code; the net48 test copy
matches the built netstandard2.0 library, and net10.0 matches its dedicated library.
FileDestination, ConsoleDestination and ConsoleWrapper have zero covered lines in this selection.
See the portable [native assessment](docs/security/security-review.md#execution-attribution-and-integrity).
This is a focused result, not the full suite, file/console execution, package-consumer qualification,
or Mac/Linux validation.

The earlier label-policy check on the same date recorded **110 cases per target**: 67 policy
cases plus 43 preserved cases. Its different selection included ConsoleDestinationTests;
the [historical account](docs/security/security-review.md#recorded-execution-and-integrity)
is retained separately, not presented as the native check above.

Do **not** substitute an unfiltered `dotnet test` as the default developer check.
[FileDestinationTests.cs](ProphetsWay.Logger.Test/FileDestinationTests.cs) now uses isolated synthetic
files, but its path/failure checks temporarily change process state; run that class in a separate
reviewed test host, not in the default selection above. Some tests in
[DestinationManagmentTests.cs](ProphetsWay.Logger.Test/DestinationManagmentTests.cs) still trigger
automatic file output and need separately reviewed isolation. The example program
constructs a relative file destination too: build it, do not run it by default.
The existing pipeline is not evidence that this exact focused selection ran in CI.

## Contributing

Keep changes focused and add xUnit/Shouldly coverage for new behavior. Preserve the intentional
utility namespace and consult [AGENTS.md](AGENTS.md) before changing public contracts or build files.
Use synthetic data; file fixtures must not alter pre-existing files. The test project's singular
name is still the actual path, not an instruction to rename it during unrelated work.

### Versioning And Authors

We use [SemVer](http://semver.org/) for versioning. For the versions available, see
the [tags on this repository](https://github.com/ProphetManX/ProphetsWay.Logger/tags).
The owner controls version changes; this README does not authorize a bump or a release.

- **G. Gordon Nasseri** - *Initial work* - [ProphetManX](https://github.com/ProphetManX)

See also the list of [contributors](https://github.com/ProphetManX/ProphetsWay.Logger/graphs/contributors)
who participated in this project.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for unreleased changes and the existing release history.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
