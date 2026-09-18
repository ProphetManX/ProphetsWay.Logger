# ProphetsWay.Logger

Route log messages to console, file, event, or custom destinations with per-destination severity selection.

> **Current tree: unreleased v4 work.** The version file still reads `3.0.1`.
> Six native severities, strict mask validation, and severity checks before supplied
> destination rendering are implemented. `SensitivityLabel`, `LabelFilterMode`, and
> `DestinationLabelPolicy` also exist in source, but the label policy remains a standalone
> predicate, not integrated into Logger registration or dispatch. These are not claims
> about the published NuGet package or completion of v4.

Build Status:  
[![Build Status](https://dev.azure.com/ProphetsWay/ProphetsWay%20GitHub%20Projects/_apis/build/status/ProphetManX.ProphetsWay.Logger?repoName=ProphetManX%2FProphetsWay.Logger&branchName=main)](https://dev.azure.com/ProphetsWay/ProphetsWay%20GitHub%20Projects/_build/latest?definitionId=25&repoName=ProphetManX%2FProphetsWay.Logger&branchName=main)

## Why Logger

When you need both a detailed log and a warning/error view, repeating each logging call
for each output adds unnecessary work. Configure destinations once at application startup;
plain Logger calls reach the registered destinations whose severity settings accept them.

- Send the same message to console, files, or callbacks with different reporting levels.
- Pass application metadata to typed destinations without putting it into message text.
- Evaluate application-defined label membership independently of logging in the current tree.

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
To use the native severity and label/policy examples now, reference the current
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

Each C# block below is a separate, self-contained console example, not a set of files to
combine. Examples use current declarations and test call patterns. Expected output describes
the source contract, not a recorded execution of these programs.

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

The messages use local timestamps and exact emitted levels, `TraceOnly` and `DebugOnly`.
The explicit `Trace` destination mask accepts all six severities; the constructor's
unchanged default `Debug` mask does not accept Trace.
`Logger.ClearDestinations()` clears plain registrations; `Logger.ClearDestinations<T>()`
clears registrations for that metadata type. Removing or clearing does not dispose your
destinations. Do not log after removing the last destination unless you intend fallback.

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
a later `Logger.Debug` call. There is currently no Logger overload that attaches this policy.

## Core Concepts

### Namespaces

The package and assembly are `ProphetsWay.Logger`; the utility namespace deliberately differs.

| Import | Use |
| --- | --- |
| `ProphetsWay.Utilities` | `Logger`, `LogLevels`, plain destination contracts/bases, and all three label/policy types. |
| `ProphetsWay.Utilities.LoggerDestinations` | `ConsoleDestination`, `FileDestination`, `EventDestination`, `GenericEventDestination<T>`, `TextBasedDestination`. |
| `ProphetsWay.Utilities.Generics` | `ILoggingDestination<T>`, `BaseLoggingDestination<T>`, `ILoggerMetadata`, and metadata extension methods. |

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
The caller must supply all effective labels, including inherited ones: the predicate does not
collect scopes or preserve origins. An inherited-only effective set is still labeled.

## API Reference

The tables summarize the current declarations, not proposed APIs.

| Member | Current use |
| --- | --- |
| `SensitivityLabel(string identifier)` | Create an immutable identity; read `Identifier`, compare with `Equals`, use `GetHashCode` for collections. |
| `DestinationLabelPolicy(LabelFilterMode mode, IEnumerable<SensitivityLabel> labels)` | Required mode and configured membership; no default constructor. |
| `DestinationLabelPolicy.Mode` / `Labels` | Read the selected mode and copied, unique, read-only membership. |
| `DestinationLabelPolicy.Allows(IEnumerable<SensitivityLabel> effectiveLabels)` | Synchronous Boolean membership decision; no output operation. |
| `Logger.AddDestination` / `RemoveDestination` / `ClearDestinations` | Manage plain registrations; generic overloads manage registrations keyed by `T`. |
| `ILoggerMetadata` / `MetadataExtensions` | Optional empty marker and the six metadata extension helpers below. |

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
| `FileDestination` | Required filename; defaults: `Debug` (excluding Trace), `resetFile: true`, `EncodingOptions.UTF8`. |
| `EventDestination` | Required reporting level; subscribe to `LoggingEvent`. |
| `GenericEventDestination<T>` | Typed callback; event arguments also expose `Metadata`. |
| `ILoggingDestination` / `ILoggingDestination<T>` | `Log` receives severity, optional message/exception, and metadata for the generic form; `IDestination` supplies `ValidateMessageLevel`. |
| `BaseLoggingDestination` / `BaseLoggingDestination<T>` | Pass a reporting level to the base constructor and override `Log`, not `WriteLogEntry`. |
| `LoggingDestinationCore` / `TextBasedDestination` | Shared severity/exception handling; the text base formats then calls your overridden `PrintLogEntry(string message)`. |

For a reusable application-specific destination, including a database destination, implement
your own storage behavior behind the appropriate base. The plain override is
`Log(LogLevels level, string message = null, Exception ex = null)`; the generic override is
`Log(LogLevels level, T metadata, string message = null, Exception ex = null)`.
No database context, schema, or `WriteLogRecord` API is provided by this library.

The core and these bases/event destinations accept `LogLevels reportingLevel`,
`string strReportingLevel`, or `int intReportingLevel` constructors. Console/file constructors
forward the same mask rules. `ValidateMessageLevel(LogLevels messageLevel)` queries eligibility
without dispatching or changing the captured mask. There is no public raw `Logger.Log` method;
the destination `Log` methods are the direct/raw entrypoints.

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
`resetFile: true` deletes an existing target during construction; use it only for an intentional
reset. The fourth parameter, `encoder`, accepts `FileDestination.EncodingOptions.ASCII`,
`BigEndianUnicode`, `Unicode`, `UTF8` (default), or `UTF32`.
The append/reset behavior is shown in [FileDestinationTests.cs](ProphetsWay.Logger.Test/FileDestinationTests.cs),
which is deliberately excluded from the default local check below.

### Receive Messages In A Callback

Subscribe before registering the destination. Callbacks run synchronously on the logging
thread; marshal to your UI thread in your own handler when needed. Event arguments expose
`Message`, `RawMessage`, `Exception`, `LogLevel`, and `Timestamp`.

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

Registration is keyed by the generic argument `T`, not the object's runtime subtype.
If that type has no registered destination, current typed logging falls back to plain logging
**without the metadata**. Register the typed destination to retain it; typed calls do not
automatically broadcast to the plain destinations when a typed registration exists.

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

No competing-error priority is promised when both Critical arguments are null. Exception
message text is not a configuration contract. Equivalent valid mask representations have
the same eligibility, but invalid strings deliberately use different errors from invalid enums/integers.

### Dispatch And Direct Calls

For registered plain and typed destinations, Logger calls each recipient's
`ValidateMessageLevel` before handing raw content to its `Log`; a false result withholds
that handoff. A present reject-all destination still suppresses fallback on that route.

`EventDestination.Log`, `GenericEventDestination<T>.Log`, and `TextBasedDestination.Log`
also enforce mask validity and all-bits eligibility when you call them **directly**.
Invalid masks throw before recipient work, even without event subscribers. A valid mismatch
returns without massage, event construction/callback, text composition or `PrintLogEntry`.
Console/file output inherits the text path. Arbitrary custom `Log` implementations or overrides
called directly are not sandboxed; inheriting an abstract destination base alone cannot guard
your override.

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
timestamp. Accepted raw exception/metadata references are not sanitized reporting objects.

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

### Current Logging And Text Output

- With no plain destination, Logger adds a timestamp-named relative file destination using local
    time and the current `FileDestination` defaults. Recreating a default can reset an existing
    same-name file. Its `Debug` mask excludes Trace: **no-setup Trace delivery is not provided**.
    Configure an explicit `Trace` destination for all six severities, and use `resetFile: false`
    for append behavior.
- Registration uses mutable global collections. Do not assume concurrent registration or callback
  add/remove/clear is safe; atomic route snapshots are unfinished work.
- Current dispatch can stop at a throwing destination. The planned independent-attempt and safe
    failure-reporting behavior is not implemented by the native severity changes or label policy.
- Console/file formatting uses local, culture-dependent timestamps and pads severity names to 12
  characters. It does not escape multiline/control input into one physical record or redact content.
- Supplied exceptions now retain their detail for warnings, errors, critical entries and every
    valid composite. The text layout is otherwise unchanged; exception detail can itself be multiline.

For example, `Logger.Debug("Hello World!")` has this text shape, with the actual host timestamp:

```text
3/15/2019 11:09:33 PM ::    DebugOnly:  Hello World!
```

The existing [example program](ProphetsWay.Logger.Example/Program.cs) constructs nested exceptions.
This retained illustrative error excerpt shows their messages; a constructed-but-unthrown
exception does not acquire a stack trace merely because its message mentions one:

```text
3/15/2019 11:25:41 PM ::    ErrorOnly:  Another generic message about an error occuring. (friendly message to show a UI maybe?)
This exception has an inner exception. (likely details to hide from a UI)

Inner Exception Message:
This is a specific Exception Message and will contain a stack trace.
```

### Unfinished Integration And Security Boundaries

Native severity/mask/helper validation and supplied-recipient **severity** withholding are implemented,
alongside the standalone labels and membership predicate. This completes neither the full dispatch
foundation (M2) nor v4. Label registration/filter integration and entry/scope origin collection remain
future work; a label policy does not yet withhold Logger payloads.

Independent route-key isolation, atomic snapshots/concurrency and lifetime work, record framing,
invariant rendering/shared UTC timestamps, safe failure reporting and independent recipient attempts,
fixed automatic-file recovery/reuse and remembered initialization failure, append-by-default explicit
files, and both Microsoft logging bridges remain unfinished. Microsoft severity conversion/None
handling is not supplied by this native enum. The new file/fallback/failure policies in
[docs/requirements.md](docs/requirements.md) are **future behavior**, not current runtime promises.
Requirements readiness is not delivery.

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
The native source assessment does not refresh the earlier dated advisory scan or provide new
license, bundled-component, SDK/runtime, or release clearance.

### Breaking Migration From Earlier Logger APIs

Recompile consumers, custom destinations, and code that compares or persists enum values.
Enum constants can be embedded in compiled callers; replacing a library binary does not translate
their old numeric meanings. This is unreleased major-version work, not a patch-safe drop-in update.

- Remove calls to ordinary, typed, and metadata-extension `Security`, plus `Security`/`SecurityOnly`
    configuration names. Choose the appropriate remaining severity for each call. There is no automatic
    security-event/concern replacement, and the standalone label policy is not a routing replacement.
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

## Architecture And Design Decisions

| Choice | Trade-off |
| --- | --- |
| Static entry point with configurable destinations | Central setup and short calls, but process-wide state and current registration limitations. |
| Typed metadata separate from message text | Your destination can inspect application data; Logger does not supply a database schema or automatically persist that object. |
| Sealed, application-defined label instead of a closed taxonomy | No registry dependency and no invalid default value-type instance; consumers must agree on exact identities. |
| Immutable copied policy membership | Later caller edits cannot change configuration; changing policy means constructing another policy. |
| Separate predicate and runtime integration | Membership is usable and testable without output, but does not yet enforce recipient withholding. |

The accepted direction is recorded in [docs/decision-log.md](docs/decision-log.md).
Its future integration rules must not be inferred from this standalone API's completion.

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
) -join '|'

dotnet test .\ProphetsWay.Logger.Test\ProphetsWay.Logger.Test.csproj -c Debug -f net48 --no-build --no-restore --filter $filter --collect "XPlat Code Coverage"
dotnet test .\ProphetsWay.Logger.Test\ProphetsWay.Logger.Test.csproj -c Debug -f net10.0 --no-build --no-restore --filter $filter --collect "XPlat Code Coverage"
```

The recorded native Windows check on **2026-09-17 EDT** passed **300 cases per target**,
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
[FileDestinationTests.cs](ProphetsWay.Logger.Test/FileDestinationTests.cs) deletes a fixed-name file,
and some tests in [DestinationManagmentTests.cs](ProphetsWay.Logger.Test/DestinationManagmentTests.cs)
trigger automatic file output. They need separately reviewed isolation. The example program
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
