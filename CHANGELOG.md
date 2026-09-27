# Unreleased

## Support and tooling

The library target set is now `netstandard2.0;net10.0`. This removes dedicated assets for the
older .NET Framework, .NET Standard 1.x, .NET Core, .NET 5 and .NET 6 targets, including the former
`net48` library asset. Consumers that can use the `netstandard2.0` asset remain supported; consumers
that require one of the removed assets must move to a compatible `netstandard2.0` consumer or to
`net10.0`. This target change is breaking and requires a major-version decision before publication.

The test project now exercises `net48` and `net10.0` with the refreshed xUnit v3 tooling. This changes
the verification matrix, not the Logger runtime behavior.

## Native severity and destination validation

The native Logger severity model now has six exact bits: `Critical = 1`, `ErrorOnly = 2`,
`WarningOnly = 4`, `InformationOnly = 8`, `DebugOnly = 16`, and `TraceOnly = 32`. Inclusive
destination masks are `Error = 3`, `Warning = 7`, `Information = 15`, `Debug = 31`, and
`Trace = 63`; a destination accepts a composite message only when it contains every requested bit.
The three public `Security` helper forms have been removed. The Logger's internal `Log` helpers remain
private, while `Log` on destination interfaces remains public for direct destination calls. There is
no compatibility replacement for the removed `Security` names, so callers must select the new severity
helper that matches their intent.

The ordinary, metadata-typed, and metadata extension helper families now expose the six severity
operations with exact-bit delivery and explicit argument requirements: `Trace`, `Debug`, `Info`, and
`Warn` require a message; `Error` requires an exception and permits a missing message; `Critical`
requires both. Existing callers should update removed helpers and any calls that relied on omitted
arguments or on the former cumulative severity values.

Destination masks supplied as enum values, integers, or recognized case-sensitive strings are now
validated strictly. Values from 0 through 63 are representable, including unnamed combinations such
as `9`; zero is a valid active reject-all destination mask. The new validation rejects invalid, negative,
unsupported, malformed, or null inputs with argument errors; it does not imply that all such invalid
forms previously shared one `Information` fallback. Destination eligibility continues to require every
requested message bit, while the new raw and direct destination guards require a nonzero mask from 1
through 63 and validate before callbacks or output. Valid calls preserve the original composite mask,
message, metadata, and exception. Exception details are included for valid warnings and composites as
well as errors, including when the raw message is null; no synthetic message or exception is fabricated.

This is breaking for consumers that compile against the removed `Security` members or depend on the
old enum numbers, permissive helper arguments, or inputs now rejected by strict validation. The current
tree is an unreleased v4-direction change; this entry does not claim that the full v4 integration,
automatic Trace/file behavior, Microsoft bridge, framing, or output-failure policy is complete.

## Sensitivity labels

A new immutable `SensitivityLabel` reference type is available in `ProphetsWay.Utilities`. Create one
with an application-defined identifier, for example:

```csharp
var label = new SensitivityLabel("internal");
```

Labels retain their exact non-null identifier, compare by ordinal value, and provide a matching hash
code. Identifiers must contain 1 through 256 UTF-16 code units and must not contain whitespace or
control characters. Invalid input is rejected without copying the supplied identifier into argument
diagnostics.

## Pure destination-label policy

The new `LabelFilterMode` enum (`NoFilter`, `Exclude`, and `AllowOnly`) and `DestinationLabelPolicy`
class provide immutable, defensive-copying membership evaluation. `Exclude` rejects any intersecting
label; `AllowOnly` requires nonempty effective membership wholly contained in the configured labels;
`NoFilter` permits valid effective input. Duplicate identities and ordering do not affect the result,
and null labels or undeclared modes are rejected with argument errors.

This remains a standalone predicate API for callers that want to evaluate a policy directly; it does
not render or redact content, select fallback destinations, or provide confidentiality or security
guarantees. M3 also uses the immutable policy as an additional whole-entry gate through
`DestinationRegistrationSettings` and the destination's intrinsic `LabelPolicy`: both configured
and intrinsic restrictions must permit the complete effective label union before payload delivery.
A label denial is a deliberate non-failure and does not select fallback; enabled membership still
suppresses fallback on that route.

The pure destination-label policy now protects its configured membership readback from ordinary collection-interface mutation attempts, including the synchronization-root path that previously exposed its owned backing list on the modern runtime. Constructed policies retain the same deduplicated, ordinal membership, and `Mode`, `Allows`, and the public API remain unchanged; consumers do not need to change their calls.

## Explicit destination registration

The existing ordinary and metadata-typed registration methods now maintain separate, exact routes. A
typed registration is selected by its declared `T`, not by runtime metadata assignability, and a
destination-contract type does not alias the ordinary route. The same compatible destination reference
may be registered on distinct routes, but adding that same reference twice to one route now throws
`ArgumentException` with `ParamName` `newDest`. Removal is reference-only, so an equal but distinct
destination remains registered, survivor order is preserved, and remove/clear affect only their selected
route. These are breaking behavior changes for callers that relied on duplicate registration, equality-
based removal, or ordinary/typed route sharing; no method signature or public type was added, and callers
should register each intended route explicitly and retain the reference they intend to remove.

Registration and dispatch now publish complete ordered membership captures before user eligibility or
callbacks run. Mutations from those callbacks affect later captures, recursive dispatch captures again,
and an older capture may still invoke a removed or cleared destination after the mutation returns.
Registration does not invoke recipients, and remove/clear never dispose borrowed destinations; hosts
must quiesce producers and await synchronous calls before disposing them. This corrects the prior live-
enumeration and ownership hazards without promising a drain, immediate revocation, or automatic fallback.

## Log annotation occurrences

The new `LogAnnotations` value captures an explicit `IEnumerable<SensitivityLabel>` through its
constructor and exposes the successful capture through the read-only `LabelOccurrences` property.
The constructor copies the supplied occurrences in order, preserves duplicates, accepts an empty
sequence, and rejects null input or null elements. A failed or unsupported sequence capture does not
publish a usable partial value; callers remain responsible for providing a finite, stable sequence.
The returned collection is read-only. `BeginScope` can attach one to a native scope and
`LogAnnotated` can attach one to an entry; dispatch captures the enclosing scopes and entry
annotation together before recipient callbacks, while the policy predicate still uses deduplicated
ordinal membership rather than occurrence order. An annotation remains data, not authorization,
classification, formatting, redaction, or a security guarantee.

## Configured dispatch failure reporting

Configured ordinary and declared-typed routes now capture the complete route, settings, scopes and
effective labels before attempting each enabled, independently eligible recipient. Registration
severity, destination severity and both applicable label policies must permit the entry; a contextual
recipient receives one contextual handoff and a legacy recipient receives one raw handoff. A deliberate
Boolean label denial withholds the entry from that recipient without recording a failure; a
`LabelCheck` exception is recorded and returns through the safe report boundary. Neither kind of label
outcome stops later captured recipients, while an eligibility or output callback failure also does not
stop later captured recipients.
After applicable attempts, the call attempts bounded notification through `Logger.DispatchFailed` when
subscribers are present and same-thread recursive reporting is not already suppressed, along with the
ordinary stderr summary, then throws `LogDispatchException` for mandatory eligibility/output failures.
A capture or check-only failure therefore permits the whole call to return safely only when no mandatory
eligibility/output failure also occurred, so mixed outcomes remain represented by `LogDispatchException`.
The report exposes a correlation ID, encounter-ordered failure descriptors with each recipient's
one-based captured position and `LabelCheck`, `Eligibility` or `Output` stage, an overflow count when
more failures occurred than the retained descriptor limit, and a separate `CoreCaptureFailureCount`
for a shared capture failure. Subscriber failures are contained, and the safe exception/report boundary
does not retain or expose the original callback causes.

Consumers that previously handled only the first raw recipient exception should instead catch
`LogDispatchException` and inspect its `Report`, and may subscribe to `Logger.DispatchFailed` when they
need the bounded notification. This contract applies to configured routes; unconfigured fallback
behavior remains separately qualified, and it does not claim automatic fallback, retries, rollback,
or full exception-graph sanitization.

The configured-route failure behavior corrects the prior single-failure propagation boundary without
claiming that the full v4 integration, automatic Trace/file behavior, Microsoft bridge, framing, or
output-failure policy is complete.

## Native scopes and contextual destinations

The Logger now captures native scopes and entry annotations into an immutable `LogContext`. Use
`BeginScope` for ordered scope properties and labels, and `LogAnnotated` for entry labels; captured
scope and label membership is complete at capture time, while nested property values remain the
caller-owned objects. Scope handles are flow-local and conventional: dispose them in order, and do
not treat a retained `LogContext` as authorization to bypass current destination checks.

Destination registration now has immutable per-registration `Enabled`, severity-mask, and label-policy
settings. `SetDestinationSettings` publishes a complete replacement atomically, so a callback sees
either the old settings or the new settings rather than a partially updated combination. The ordinary
and exact-declared-metadata-type routes remain separate. Existing independent destination interfaces
remain valid; contextual interfaces are optional extensions for destinations that need the captured
context.

Supplied destinations and base classes now expose guarded direct `Log` calls and optional
`LogWithContext` calls. Custom destinations deriving from `BaseLoggingDestination` or
`BaseLoggingDestination<T>` must move their implementation to the protected `LogCore` override;
the public logging entrypoints are guarded and are no longer the subclass override point. This is a
source and binary compatibility break for custom destinations that overrode the former public logging
member. Built-in event destinations expose the selected context through their event arguments, while
text and event delivery preserve the original payload, metadata, exception and existing rendering
behavior.

Failure results now distinguish a failed native capture from a destination `LabelCheck`, `Eligibility`,
or `Output` failure. Shared capture and label-check failures report safely and return without handing
the incomplete or rejected entry to that recipient; eligibility and output failures remain mandatory
`LogDispatchException` results after independent attempts. `CoreCaptureFailureCount` records the core
capture count without inventing a recipient failure, and failure-report membership no longer exposes a
writable `SyncRoot` alias through collection interfaces. Consumers reading `LogDispatchException.Report`
can therefore distinguish these safe result paths without receiving callback causes or payload data.

This M3 work is unreleased and does not by itself claim publication or release readiness. The custom
destination override change has major-version implications; the new scope, annotation, contextual
delivery, settings, and failure-report members are additive individually, but they do not make that
overall compatibility decision a minor or patch change. The accepted local validation is green on the
focused `net48` and `net10.0` matrix, with the library/example builds and compile-only README checks
also passing; it is not unfiltered real-file fixture certification. The permanent report-membership
regression and parent final gate are complete within that accepted local boundary; they are not pending release work.

# v3.0.1
### Build target for Net 6.0
Library now targets .Net 6.0
Updated repository to build using the new https://github.com/ProphetManX/prophets-pipelines framework

# v3.0.0
### Converted build/release pipelines into YML source files
Project's build and release pipleines are now written in YML files that are checked into the source code.
Removed build targets for unsupported versions of .Net frameworks (removed netcoreapp1.0, netcoreapp1.1, netcoreapp2.0, netcoreapp2.2, netcoreapp3.0.)
Removed UTF7 as a target filetype encoding for FileDestinations as this is obsolete, should be using UTF8.

# v2.2.0
### Build target for Net 5.0
Library now targets .Net 5.0

# v2.1.0
### Configurable LogLevels
You can now instanciate the Logger Destinations using the string or integer value of a given LogLevel.

Library now targets .NetFramework 4.8, .NetStandard 2.1 .NetCore 3.1


# v2.0.0
### LogLevel Overhaul and Specific LogLevel Targeting for Destinations
Modified the actual values of the LogLevels enum values, setting them in Binary so it's easier to understand how 
the logic works in the comparison functionality.  With these changes I have also created a subset of "xxxOnly" levels.
These new levels can be used to set your destination to only register log messages of that specific log level.
Now if you want to have all your ```Security``` logs written to a destination that your security officer will see, 
without cluttering the log with any ```Error``` or ```Warning``` messages that might have also occurred.  

Unfortunately the changes coming with this version will break any custom Destinations and code that ever used 
the ```Log``` or ```Log<T>``` methods.  My assumption is that this project has low adoption at this point
and any use of the pre-defined destinations aren't affected by these changes.  

Now all Log messages must be triggered via the "Shortcut" methods, and instead of triggering the original LogLevels
they now trigger the "Only" level.  This is the big change required to get Specific Level Targeting to work properly.


# v1.3.0
### Metadata Extensions
With the addition of Generics, I wanted to make an easier way for a user to log statements around their context/metadata
object.  Instead of requiring ```Logger.Info("information message", customMetadataObj);```, now you can modify your custom
object to implement the interface ```ILoggerMetadata```.  With this small change, logging becomes as easy as 
```customMetadataObj.Info("information message");```.

Cleaned up architecture of base logging destination classes.  I didn't like how the generic base inherited the regular base, and thus 
the generic base had both log functions implemented (```Log``` and ```Log<T>```, even tho only one was accessible). 
Created new base class for Destinations where the log level check logic, and the message massager can be shared across 
both base destinations.  


# v1.2.0
### Now with Generics!
Added the ability to log a custom object to a destination.  Currently only ```GenericEventDestination<T>``` was
created for testing purposes, however the point of a generic Logger is so you can put your custom metadata into 
your logs however/where you choose.  Immediate examples are having a custom database Destination, and you would want
to pass context of where/who/what is calling the log statement.  All of that context/metadata can be put into
a custom object class and call ```Logger.Info("information message", customMetadataObj);```, and then your custom
destination will parse out all the properties of your object, and log them accordingly.

There is no need to include `Message` or `Exception` in your custom object, as they are still required
parameters for using Logger and will be passed via the BaseLoggingDestination.


# v1.1.0
### Project Refactor
Changed a lot of properties on the .csproj file and updated the build/release pipelines for a more seamless setup.


# v1.0.0
### Initial proper release.  
Contains functionality to log messages and exceptions.  See the README.MD for general information.
