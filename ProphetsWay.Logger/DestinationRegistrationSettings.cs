using System;

namespace ProphetsWay.Utilities
{
	/// <summary>Defines one immutable publication of a registration's delivery restrictions.</summary>
	/// <remarks>A23/A25-A31/A38: sealed value, separate from the borrowed destination.
	/// Construction publishes nothing. Enabled, ReportingLevel and LabelPolicy are replaced
	/// together, never mutated in place. Reusing the value on several routes does not couple
	/// later replacements. The supplied sealed policy already owns immutable membership;
	/// neither its source enumerable nor writable aliases become registration storage.
	/// Concurrent reads are supported. Destination-owned state and direct-call configuration
	/// are not frozen, replaced or made thread-safe by this value.</remarks>
	public sealed class DestinationRegistrationSettings
	{
		private readonly bool _enabled;
		private readonly LogLevels _reportingLevel;
		private readonly DestinationLabelPolicy _labelPolicy;

		/// <summary>Creates a complete registration-settings value without registering a recipient.</summary>
		/// <param name="enabled">True for active membership; false retains the registration disabled.</param>
		/// <param name="reportingLevel">A known-bit destination mask from 0 through 63; zero rejects all.</param>
		/// <param name="labelPolicy">Required immutable policy; use NoFilter for no added label restriction.</param>
		/// <exception cref="ArgumentNullException">labelPolicy is null; ParamName is "labelPolicy".</exception>
		/// <exception cref="ArgumentOutOfRangeException">reportingLevel is negative or has unknown bits;
		/// ParamName is "reportingLevel".</exception>
		/// <remarks>A25/A26: every argument is explicit; no additional default mode is chosen.
		/// Unnamed combinations of known bits and both enabled values are valid. No caller
		/// sequence or destination callback is evaluated. Preserve the supplied immutable
		/// policy's mode/membership; representative object identity is unspecified. No validation
		/// precedence is promised when more than one argument is invalid. Argument errors use
		/// fixed contract vocabulary without copying policy labels or other supplied content.</remarks>
		public DestinationRegistrationSettings(bool enabled, LogLevels reportingLevel, DestinationLabelPolicy labelPolicy)
		{
			if ((reportingLevel & ~LogLevels.Trace) != 0)
			{
				throw new ArgumentOutOfRangeException(nameof(reportingLevel));
			}

			if (labelPolicy == null)
			{
				throw new ArgumentNullException(nameof(labelPolicy));
			}

			_enabled = enabled;
			_reportingLevel = reportingLevel;
			_labelPolicy = labelPolicy;
		}

		/// <summary>Gets whether this registration participates in subsequent captured routes.</summary>
		/// <value>The unchanged enabled value supplied at construction.</value>
		/// <remarks>A29: false excludes this registration from delivery and fallback suppression,
		/// but does not remove it, change insertion position or dispose its destination.
		/// True remains active even when its mask or label policy rejects every entry.</remarks>
		public bool Enabled
		{
			get
			{
				return _enabled;
			}
		}

		/// <summary>Gets the registration-level severity restriction.</summary>
		/// <value>The unchanged known-bit mask from 0 through 63.</value>
		/// <remarks>A26/A30: the mask must contain every requested message bit, not merely
		/// overlap it. It restricts registration delivery in addition to the destination's
		/// own ValidateMessageLevel result; replacing it does not overwrite recipient state.</remarks>
		public LogLevels ReportingLevel
		{
			get
			{
				return _reportingLevel;
			}
		}

		/// <summary>Gets the registration-level whole-entry label restriction.</summary>
		/// <value>A non-null immutable policy with the supplied mode and captured membership.</value>
		/// <remarks>A25/A31: evaluate against the completed effective-label union, including
		/// inherited-only membership. NoFilter adds no label restriction. A false result is
		/// a non-failure, keeps enabled membership active and never authorizes fallback.</remarks>
		public DestinationLabelPolicy LabelPolicy
		{
			get
			{
				return _labelPolicy;
			}
		}
	}
}