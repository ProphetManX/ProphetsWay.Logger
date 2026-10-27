namespace ProphetsWay.Utilities
{
	/// <summary>Describes one failed recipient without retaining its object or raw cause.</summary>
	/// <remarks>B13/B14/B17: sealed, immutable, library-created, with no public construction.
	/// One observed stage per failed recipient, never per inner cause; core failures are not descriptors.</remarks>
	public sealed class LogFailureDescriptor
	{
		private readonly int _registrationId;
		private readonly LogFailureStage _stage;

		internal LogFailureDescriptor(int registrationId, LogFailureStage stage)
		{
			_registrationId = registrationId;
			_stage = stage;
		}

		/// <summary>Locates the recipient within this call's full captured membership.</summary>
		/// <value>A positive one-based position, including preceding disabled, rejected and successful slots.</value>
		/// <remarks>B14: meaningful only with CorrelationId; not a stable ID, hash or lookup key.
		/// The sole supplied-direct recipient occupies 1. Zero never denotes core capture.</remarks>
		public int RegistrationId
		{
			get
			{
				return _registrationId;
			}
		}

		/// <summary>Gets the observed failed boundary.</summary>
		/// <value>Eligibility, Output or LabelCheck.</value>
		/// <remarks>B13: never derive this value from foreign exception types, messages or nested reports.</remarks>
		public LogFailureStage Stage
		{
			get
			{
				return _stage;
			}
		}
	}
}
