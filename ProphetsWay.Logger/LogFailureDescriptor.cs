namespace ProphetsWay.Utilities
{
	/// <summary>Describes one failed captured recipient without retaining that recipient or its cause.</summary>
	/// <remarks>B08-B12: sealed, immutable and library-created; no public construction or mutation.</remarks>
	public sealed class LogFailureDescriptor
	{
		private readonly int _registrationId;
		private readonly LogFailureStage _stage;

		internal LogFailureDescriptor(int registrationId, LogFailureStage stage)
		{
			_registrationId = registrationId;
			_stage = stage;
		}

		/// <summary>Identifies the recipient within the failed call's captured membership.</summary>
		/// <value>The positive, one-based position in the full capture, not in the failure list.</value>
		/// <remarks>B09: meaningful only with the report's correlation ID; no cross-call identity.</remarks>
		public int RegistrationId
		{
			get
			{
				return _registrationId;
			}
		}

		/// <summary>Identifies which recipient callback threw.</summary>
		/// <value>Eligibility or Output, selected by the observed boundary, never exception content.</value>
		/// <remarks>B05-B06: one stage per failed recipient; no inner-cause decomposition.</remarks>
		public LogFailureStage Stage
		{
			get
			{
				return _stage;
			}
		}
	}
}
