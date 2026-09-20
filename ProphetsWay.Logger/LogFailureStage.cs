namespace ProphetsWay.Utilities
{
	/// <summary>Identifies the recipient callback boundary that failed.</summary>
	/// <remarks>B05-B06/B10: non-flags codes; Logger emits only these values, not inferred causes.</remarks>
	public enum LogFailureStage
	{
		/// <summary>The Logger-invoked severity eligibility callback threw before payload handoff.</summary>
		Eligibility = 1,
		/// <summary>The eligible recipient's Log invocation threw, including work inside that invocation.</summary>
		Output = 2
	}
}
