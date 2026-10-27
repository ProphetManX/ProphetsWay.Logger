using System;

namespace ProphetsWay.Utilities
{
	/// <summary>
	/// Represents an immutable, application-defined sensitivity identity.
	/// </summary>
	/// <remarks>
	/// Identifiers are opaque: no registry, taxonomy, URI interpretation or logger is required.
	/// Construction and comparison do not trim, fold case or normalize Unicode.
	/// Reads, comparisons, hashing and collection use cannot change a label's identity.
	/// </remarks>
	public sealed class SensitivityLabel : IEquatable<SensitivityLabel>
	{
		/// <summary>
		/// Creates a label from an application-defined identifier.
		/// </summary>
		/// <param name="identifier">The exact identifier to retain without rewriting.</param>
		/// <exception cref="ArgumentNullException"><paramref name="identifier"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// The non-null identifier is empty, exceeds 256 UTF-16 code units, or contains
		/// a whitespace or control code unit.
		/// </exception>
		/// <remarks>
		/// Validity requires String.Length from 1 through 256 inclusive and, for every char,
		/// both Char.IsWhiteSpace and Char.IsControl to return false on the executing runtime.
		/// There are no additional syntax restrictions. Argument errors name identifier;
		/// supplied content is never copied or encoded into their messages or data.
		/// </remarks>
		public SensitivityLabel(string identifier)
		{
			if (identifier == null)
			{
				throw new ArgumentNullException(nameof(identifier));
			}

			if (identifier.Length < 1 || identifier.Length > 256)
			{
				throw new ArgumentException("Identifier must contain 1 through 256 UTF-16 code units.", nameof(identifier));
			}

			foreach (var character in identifier)
			{
				if (char.IsWhiteSpace(character) || char.IsControl(character))
				{
					throw new ArgumentException("Identifier must not contain whitespace or control characters.", nameof(identifier));
				}
			}

			Identifier = identifier;
		}

		/// <summary>
		/// Gets the identifier supplied at construction.
		/// </summary>
		/// <value>The original, non-null UTF-16 sequence, unchanged.</value>
		public string Identifier { get; }

		/// <summary>
		/// Determines whether another label has the same ordinal identity.
		/// </summary>
		/// <param name="other">The label to compare; null is permitted.</param>
		/// <returns>True for a non-null label with the same identifier; otherwise false.</returns>
		/// <remarks>
		/// Equality is case-sensitive and culture-independent, not reference equality.
		/// Comparison preserves both operands and is reflexive, symmetric and transitive.
		/// </remarks>
		public bool Equals(SensitivityLabel other)
		{
			return other != null && string.Equals(Identifier, other.Identifier, StringComparison.Ordinal);
		}

		/// <summary>
		/// Determines whether an object is a label with the same ordinal identity.
		/// </summary>
		/// <param name="obj">The object to compare; null and unrelated types are permitted.</param>
		/// <returns>The typed equality result for labels; false for null or unrelated objects.</returns>
		public override bool Equals(object obj)
		{
			return Equals(obj as SensitivityLabel);
		}

		/// <summary>
		/// Gets a hash code consistent with label value equality.
		/// </summary>
		/// <returns>A hash code equal to the hash of every equal label.</returns>
		/// <remarks>
		/// Repeated calls remain stable for the instance; unequal labels may collide.
		/// No fixed numeric value, algorithm or cross-process persistence is promised.
		/// </remarks>
		public override int GetHashCode()
		{
			return StringComparer.Ordinal.GetHashCode(Identifier);
		}
	}
}
