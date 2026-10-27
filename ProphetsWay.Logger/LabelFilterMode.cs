namespace ProphetsWay.Utilities
{
	/// <summary>Selects one destination label-membership rule.</summary>
	/// <remarks>Not a flags enum. Exclude and AllowOnly cannot be combined.</remarks>
	public enum LabelFilterMode
	{
		/// <summary>Adds no label restriction.</summary>
		NoFilter = 0,

		/// <summary>Denies any intersection with configured label membership.</summary>
		Exclude = 1,

		/// <summary>Permits only nonempty effective membership wholly contained in configured membership.</summary>
		AllowOnly = 2
	}
}
