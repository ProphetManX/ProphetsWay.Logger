namespace ProphetsWay.Utilities
{
	/// <summary>Identifies native message severity bits and destination masks.</summary>
	/// <remarks>
	/// Helpers emit one exact bit. Raw messages may combine known bits but cannot be zero.
	/// Destination masks may combine known bits, including zero to reject every message.
	/// Eligibility requires every message bit, not merely an overlapping bit.
	/// </remarks>
	[System.Flags]
	public enum LogLevels : int
	{
		/// <summary>Exact Critical bit; destination accepts Critical only.</summary>
		Critical = 1,
		/// <summary>Exact Error bit; destination accepts Error only.</summary>
		ErrorOnly = 2,
		/// <summary>Exact Warning bit; destination accepts Warning only.</summary>
		WarningOnly = 4,
		/// <summary>Exact Information bit; destination accepts Information only.</summary>
		InformationOnly = 8,
		/// <summary>Exact Debug bit; destination accepts Debug only.</summary>
		DebugOnly = 16,
		/// <summary>Exact Trace bit; destination accepts Trace only.</summary>
		TraceOnly = 32,
		/// <summary>Destination accepts Critical and Error bits.</summary>
		Error = 3,
		/// <summary>Destination accepts Critical, Error and Warning bits.</summary>
		Warning = 7,
		/// <summary>Destination accepts Critical through Information bits.</summary>
		Information = 15,
		/// <summary>Destination accepts Critical through Debug bits.</summary>
		Debug = 31,
		/// <summary>Destination accepts all six severity bits.</summary>
		Trace = 63
	}
}