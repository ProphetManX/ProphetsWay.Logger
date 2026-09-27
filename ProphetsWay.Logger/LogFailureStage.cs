namespace ProphetsWay.Utilities
{
	/// <summary>Names the recipient boundary at which an original failure was observed.</summary>
	/// <remarks>B13: non-flags codes, not inferred causes. Core capture has a separate count.</remarks>
	public enum LogFailureStage
	{
		/// <summary>The Logger-invoked severity callback threw before payload handoff.</summary>
		Eligibility = 1,
		/// <summary>The selected raw/contextual recipient invocation or supplied delivery hook threw.</summary>
		Output = 2,
		/// <summary>An opted-in captured recipient label-policy evaluation threw before payload handoff.</summary>
		LabelCheck = 3
	}
}
