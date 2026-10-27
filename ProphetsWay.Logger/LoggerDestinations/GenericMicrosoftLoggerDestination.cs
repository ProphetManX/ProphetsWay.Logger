using System;
using Microsoft.Extensions.Logging;
using ProphetsWay.Utilities.Generics;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	/// <summary>Forwards permitted exact-T Logger entries to a supplied Microsoft logger or factory.</summary>
	/// <typeparam name="T">Unconstrained declared native metadata route type, not a label or runtime-type selector.</typeparam>
	/// <remarks>O34: the complete O01-O33 outbound contract on MicrosoftLoggerDestination
	/// applies, including O32-O33's controlled-call diagnostic membership and single
	/// completion, with the native generic BaseLoggingDestination of T entrypoints and
	/// O12's typed-state capture instead of the ordinary string-state projection.
	/// This class implements no ordinary route. Use the existing exact-T registration;
	/// ordinary routes and other declared types remain independent. For accepted inbound
	/// entries explicitly use T equal to LogBridgeEntry; runtime assignability does not
	/// select that route. A trusted captured inbound delivery preserves its original
	/// generic producer entry, not a nested LogBridgeEntry of LogBridgeEntry. A public
	/// direct call or fresh native call with carrier-shaped metadata instead remains
	/// new raw T input, never implicit archived-label or cycle authority. Registration
	/// and intrinsic eligibility, direct current-frame validation/new time, shared
	/// capture, borrowed lifetime, envelopes, scope cleanup and failure behavior are
	/// identical to the ordinary counterpart. No metadata marker, formatter option,
	/// new lifecycle member or automatic reverse registration is introduced.</remarks>
	/// <seealso cref="MicrosoftLoggerDestination"/>
	public sealed class GenericMicrosoftLoggerDestination<T> : BaseLoggingDestination<T>
	{
		private readonly ILoggerFactory _factory;
		private readonly ILogger _logger;
		private readonly string _defaultCategoryName;
		/// <summary>Creates an exact-T destination using available origin or fallback category.</summary>
		/// <param name="reportingLevel">Required known-bit destination mask, 0 through 63; zero is active reject-all.</param>
		/// <param name="factory">Required borrowed Microsoft logger factory.</param>
		/// <param name="defaultCategoryName">Required category for absent origin; defaults to "ProphetsWay.Logger". Empty and whitespace are valid.</param>
		/// <exception cref="ArgumentNullException">factory or defaultCategoryName is null; ParamName names the argument.</exception>
		/// <exception cref="ArgumentOutOfRangeException">reportingLevel is negative or contains unknown bits; ParamName is "reportingLevel".</exception>
		/// <remarks>O06/O08/O34: the ordinary factory constructor's validation,
		/// initial NoFilter policy, independent construction and absence of callbacks,
		/// registration, output and disposal apply unchanged. No invalid-argument
		/// priority is added. Constructor choices do not change after construction.</remarks>
		public GenericMicrosoftLoggerDestination(LogLevels reportingLevel, ILoggerFactory factory,
			string defaultCategoryName = "ProphetsWay.Logger") : base(reportingLevel)
		{
			if (factory == null)
				throw new ArgumentNullException(nameof(factory));
			if (defaultCategoryName == null)
				throw new ArgumentNullException(nameof(defaultCategoryName));

			_factory = factory;
			_defaultCategoryName = defaultCategoryName;
		}

		/// <summary>Creates an exact-T destination using one fixed Microsoft logger.</summary>
		/// <param name="reportingLevel">Required known-bit destination mask, 0 through 63; zero is active reject-all.</param>
		/// <param name="logger">Required borrowed fixed logger; no category is inferred or changed.</param>
		/// <exception cref="ArgumentNullException">logger is null; ParamName is "logger".</exception>
		/// <exception cref="ArgumentOutOfRangeException">reportingLevel is negative or contains unknown bits; ParamName is "reportingLevel".</exception>
		/// <remarks>O07/O09/O34: retain this exact logger and initial NoFilter policy.
		/// Validate before foreign operations, without competing-invalid priority.
		/// No registration, callback, output, scope change or disposal occurs here;
		/// each construction is an independent destination, not a registration helper.</remarks>
		public GenericMicrosoftLoggerDestination(LogLevels reportingLevel, ILogger logger) : base(reportingLevel)
		{
			if (logger == null)
				throw new ArgumentNullException(nameof(logger));

			_logger = logger;
		}

		/// <summary>Exports one admitted exact-T entry using the completed shared bridge facts.</summary>
		/// <param name="context">Non-null selected current native capture, separate from retained source context.</param>
		/// <param name="level">Validated permitted full native mask, 1 through 63.</param>
		/// <param name="metadata">Original T value/reference, including null or default(T); never runtime-rerouted.</param>
		/// <param name="message">Original optional native text; permitted absences remain unchanged.</param>
		/// <param name="ex">Original optional exception being logged.</param>
		/// <exception cref="Exception">An actual Microsoft callback or adaptation output fails; the enclosing guarded dispatch applies safe Output handling.</exception>
		/// <remarks>O12-O34: follow the ordinary hook's complete protocol, using the
		/// prepared original typed entry or specifically trusted controlled entry as
		/// appropriate. No per-recipient re-enumeration of producer membership, new
		/// producer invocation, implicit metadata classification, ambient recapture,
		/// second public native dispatch, borrowed disposal or local duplicate report.
		/// Shared-capture failures retain their separate preceding core boundary;
		/// temporary export callbacks and reverse cleanup use the Output boundary.
		/// The enclosing accumulator uses O32's block/slot association and O33's
		/// trusted-fact handling exactly as for the ordinary hook. Never preserve an
		/// unqualified route-local number in a multi-block completion, attribute a
		/// downstream failure to this exporter merely for forwarding it, or merge
		/// an independent recursive call's completed report.</remarks>
		protected override void LogCore(LogContext context, LogLevels level, T metadata,
			string message, Exception ex)
		{
			Logger.ExportMicrosoft(_factory, _logger, _defaultCategoryName,
				context, level, metadata, message, message, ex);
		}
	}
}
