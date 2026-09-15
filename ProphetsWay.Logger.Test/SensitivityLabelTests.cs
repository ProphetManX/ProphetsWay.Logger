using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using ProphetsWay.Utilities;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	public class SensitivityLabelTests
	{
		[Fact]
		public void ShouldExposeTheReviewedImmutableReferenceValueContract()
		{
			var contract = new LabelContract();
			var labelType = contract.LabelType;
			Assert.True(labelType.IsPublic && labelType.IsClass && labelType.IsSealed);
			Assert.False(labelType.IsAbstract);
			Assert.False(labelType.ContainsGenericParameters);
			Assert.Equal("ProphetsWay.Logger", labelType.Assembly.GetName().Name);
			Assert.Single(labelType.GetConstructors());
			var identifierParameter = Assert.Single(contract.Constructor.GetParameters());
			Assert.Equal(typeof(string), identifierParameter.ParameterType);
			Assert.Equal("identifier", identifierParameter.Name);
			Assert.False(identifierParameter.IsOptional);
			Assert.Equal(typeof(string), contract.IdentifierProperty.PropertyType);
			Assert.Empty(contract.IdentifierProperty.GetIndexParameters());
			Assert.NotNull(contract.IdentifierProperty.GetGetMethod());
			Assert.Null(contract.IdentifierProperty.GetSetMethod());
			Assert.Equal(contract.EquatableType, Assert.Single(labelType.GetInterfaces()));
			Assert.Equal("other", Assert.Single(contract.TypedEquality.GetParameters()).Name);
			Assert.Equal("obj", Assert.Single(contract.ObjectEquality.GetParameters()).Name);
			Assert.Equal(typeof(bool), contract.TypedEquality.ReturnType);
			Assert.Equal(typeof(bool), contract.ObjectEquality.ReturnType);
			Assert.Equal(typeof(int), contract.HashMethod.ReturnType);
			Assert.Equal(typeof(object), contract.ObjectEquality.GetBaseDefinition().DeclaringType);
			Assert.Equal(typeof(object), contract.HashMethod.GetBaseDefinition().DeclaringType);
			var allowedMethods = new[]
			{
				contract.IdentifierProperty.GetGetMethod(), contract.TypedEquality,
				contract.ObjectEquality, contract.HashMethod
			};
			Assert.All(labelType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly),
				method => Assert.Contains(method, allowedMethods));
			Assert.Empty(labelType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));
		}

		[Theory]
		[InlineData(1)]
		[InlineData(256)]
		public void ShouldAcceptAsciiLengthBoundariesWithoutChangingTheIdentifier(int length)
		{
			var contract = new LabelContract();
			var identifier = new string('a', length);
			var label = contract.Create(identifier);
			Assert.Equal(identifier, contract.ReadIdentifier(label));
		}

		[Fact]
		public void ShouldRejectNullWithTheExactNamedArgumentError()
		{
			var contract = new LabelContract();
			var exception = Assert.Throws<ArgumentNullException>(() => contract.Create(null));
			Assert.Equal("identifier", exception.ParamName);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(257)]
		public void ShouldRejectLengthsOutsideTheInclusiveLimit(int length)
		{
			var contract = new LabelContract();
			var exception = Assert.Throws<ArgumentException>(() => contract.Create(new string('a', length)));
			Assert.Equal("identifier", exception.ParamName);
		}

		[Fact]
		public void ShouldCountSupplementaryCharactersAsTwoUtf16Units()
		{
			var contract = new LabelContract();
			var atLimit = string.Concat(Enumerable.Repeat("\uD83D\uDE00", 128));
			var label = contract.Create(atLimit);
			Assert.Equal(atLimit, contract.ReadIdentifier(label));
			var overLimit = string.Concat(Enumerable.Repeat("\uD83D\uDE00", 129));
			var exception = Assert.Throws<ArgumentException>(() => contract.Create(overLimit));
			Assert.Equal("identifier", exception.ParamName);
		}

		[Theory]
		[InlineData("team:alpha/v1")]
		[InlineData("<not-a-uri>#?%|\\")]
		[InlineData("\u00E9")]
		[InlineData("e\u0301")]
		public void ShouldRetainOpaqueIdentifiersWithoutRegistrationOrNormalization(string identifier)
		{
			var contract = new LabelContract();
			var label = contract.Create(identifier);
			Assert.Equal(identifier, contract.ReadIdentifier(label));
		}

		[Theory]
		[InlineData(0x00AD)]
		[InlineData(0x200B)]
		[InlineData(0xD800)]
		[InlineData(0xDC00)]
		[InlineData(0xFFFF)]
		public void ShouldAcceptNonWhitespaceNonControlUnitsWithoutExtraUnicodeRestrictions(int codeUnit)
		{
			var contract = new LabelContract();
			var identifier = new string((char)codeUnit, 1);
			var label = contract.Create(identifier);
			Assert.Equal(identifier, contract.ReadIdentifier(label));
		}

		[Theory]
		[InlineData(0x0020)]
		[InlineData(0x0009)]
		[InlineData(0x000D)]
		[InlineData(0x000A)]
		[InlineData(0x0000)]
		[InlineData(0x007F)]
		[InlineData(0x0085)]
		[InlineData(0x009F)]
		[InlineData(0x00A0)]
		[InlineData(0x2003)]
		[InlineData(0x2028)]
		[InlineData(0x2029)]
		[InlineData(0x3000)]
		public void ShouldRejectEmbeddedWhitespaceAndControlUnits(int codeUnit)
		{
			var contract = new LabelContract();
			var identifier = "left" + (char)codeUnit + "right";
			var exception = Assert.Throws<ArgumentException>(() => contract.Create(identifier));
			Assert.Equal("identifier", exception.ParamName);
		}

		[Theory]
		[InlineData(" label")]
		[InlineData("label ")]
		public void ShouldRejectEdgeWhitespaceInsteadOfTrimmingIt(string identifier)
		{
			var contract = new LabelContract();
			var exception = Assert.Throws<ArgumentException>(() => contract.Create(identifier));
			Assert.Equal("identifier", exception.ParamName);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldExcludeRejectedContentFromArgumentDiagnostics(bool exceedsLengthLimit)
		{
			var contract = new LabelContract();
			const string canary = "LabelInput7f19";
			var identifier = exceedsLengthLimit ? canary + new string('x', 256) : canary + "\nEnd8a42";
			var exception = Assert.Throws<ArgumentException>(() => contract.Create(identifier));
			Assert.Equal("identifier", exception.ParamName);
			var forbiddenContent = new[]
			{
				canary, identifier, Uri.EscapeDataString(identifier),
				Convert.ToBase64String(Encoding.UTF8.GetBytes(identifier)),
				Convert.ToBase64String(Encoding.Unicode.GetBytes(identifier))
			};
			AssertNoIdentifierContent(exception.Message, forbiddenContent);
			foreach (DictionaryEntry entry in exception.Data)
			{
				AssertNoIdentifierContent(entry.Key, forbiddenContent);
				AssertNoIdentifierContent(entry.Value, forbiddenContent);
			}
		}

		[Fact]
		public void ShouldGiveSeparateEqualLabelsReflexiveSymmetricAndTransitiveEquality()
		{
			var contract = new LabelContract();
			var labels = Enumerable.Range(0, 3)
				.Select(index => contract.Create(new string("team:alpha/v1".ToCharArray()))).ToArray();
			foreach (var left in labels)
			{
				foreach (var right in labels)
				{
					Assert.True(contract.TypedEquals(left, right));
					Assert.True(contract.EquatableEquals(left, right));
					Assert.True(left.Equals(right));
					Assert.Equal(left.GetHashCode(), right.GetHashCode());
				}
				Assert.Equal("team:alpha/v1", contract.ReadIdentifier(left));
			}
		}

		[Theory]
		[InlineData("PII", "pii")]
		[InlineData("\u00E9", "e\u0301")]
		[InlineData("ab", "a\u00ADb")]
		[InlineData("team:alpha/v1", "team:alpha/v2")]
		public void ShouldKeepOrdinallyDifferentIdentifiersUnequalInBothDirections(string firstIdentifier, string secondIdentifier)
		{
			var contract = new LabelContract();
			var first = contract.Create(firstIdentifier);
			var second = contract.Create(secondIdentifier);
			Assert.False(contract.TypedEquals(first, second));
			Assert.False(contract.TypedEquals(second, first));
			Assert.False(contract.EquatableEquals(first, second));
			Assert.False(contract.EquatableEquals(second, first));
			Assert.False(first.Equals(second));
			Assert.False(second.Equals(first));
			Assert.Equal(firstIdentifier, contract.ReadIdentifier(first));
			Assert.Equal(secondIdentifier, contract.ReadIdentifier(second));
		}

		[Fact]
		public void ShouldCompareUnequalToNullAndUnrelatedObjectsWithoutThrowing()
		{
			var contract = new LabelContract();
			var label = contract.Create("team:alpha/v1");
			Assert.False(contract.TypedEquals(label, null));
			Assert.False(contract.EquatableEquals(label, null));
			Assert.False(label.Equals(null));
			Assert.False(label.Equals("team:alpha/v1"));
			Assert.False(label.Equals(new object()));
			Assert.Equal("team:alpha/v1", contract.ReadIdentifier(label));
		}

		[Fact]
		public void ShouldPreserveIdentityAndHashAcrossOtherLabelsComparisonsAndCollectionUse()
		{
			var contract = new LabelContract();
			var first = contract.Create("team:alpha/v1");
			var originalHash = first.GetHashCode();
			var equal = contract.Create(new string("team:alpha/v1".ToCharArray()));
			var different = contract.Create("team:beta/v1");
			var entries = new Dictionary<object, string> { { first, "original" } };
			Assert.Equal("original", entries[equal]);
			entries[equal] = "updated";
			Assert.Single(entries);
			entries.Add(different, "separate");
			for (var repetition = 0; repetition < 3; repetition++)
			{
				Assert.Equal("updated", entries[first]);
				Assert.Equal("updated", entries[equal]);
				Assert.Equal("separate", entries[different]);
				Assert.Equal("team:alpha/v1", contract.ReadIdentifier(first));
				Assert.Equal("team:alpha/v1", contract.ReadIdentifier(equal));
				Assert.Equal("team:beta/v1", contract.ReadIdentifier(different));
				Assert.True(contract.TypedEquals(first, equal));
				Assert.True(contract.EquatableEquals(first, equal));
				Assert.True(first.Equals(equal));
				Assert.False(contract.TypedEquals(first, different));
				Assert.False(first.Equals(different));
				Assert.Equal(originalHash, first.GetHashCode());
				Assert.Equal(originalHash, equal.GetHashCode());
			}
		}

		private static void AssertNoIdentifierContent(object diagnostic, IEnumerable<string> forbiddenContent)
		{
			var text = diagnostic is char[] ? new string((char[])diagnostic)
				: diagnostic is byte[] ? Encoding.UTF8.GetString((byte[])diagnostic) + Encoding.Unicode.GetString((byte[])diagnostic)
				: Convert.ToString(diagnostic, CultureInfo.InvariantCulture) ?? string.Empty;
			foreach (var content in forbiddenContent)
			{
				Assert.True(text.IndexOf(content, StringComparison.OrdinalIgnoreCase) < 0,
					"Argument diagnostics must not expose supplied identifier content.");
			}
		}

		private sealed class LabelContract
		{
			public Type LabelType { get; }
			public Type EquatableType { get; }
			public ConstructorInfo Constructor { get; }
			public PropertyInfo IdentifierProperty { get; }
			public MethodInfo TypedEquality { get; }
			public MethodInfo ObjectEquality { get; }
			public MethodInfo HashMethod { get; }

			public LabelContract()
			{
				LabelType = typeof(ILoggingDestination).Assembly.GetType("ProphetsWay.Utilities.SensitivityLabel", false, false);
				Assert.True(LabelType != null,
					"Missing public contract: ProphetsWay.Utilities.SensitivityLabel in the real referenced ProphetsWay.Logger assembly.");
				Constructor = LabelType.GetConstructor(new[] { typeof(string) });
				Assert.True(Constructor != null, "Missing public SensitivityLabel(string identifier) constructor.");
				IdentifierProperty = LabelType.GetProperty("Identifier", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				Assert.True(IdentifierProperty != null, "Missing public Identifier property.");
				TypedEquality = RequireMethod("Equals", LabelType);
				ObjectEquality = RequireMethod("Equals", typeof(object));
				HashMethod = RequireMethod("GetHashCode");
				EquatableType = typeof(IEquatable<>).MakeGenericType(LabelType);
				Assert.True(EquatableType.IsAssignableFrom(LabelType), "SensitivityLabel must implement IEquatable<SensitivityLabel>.");
			}

			public object Create(string identifier)
			{
				try
				{
					return Constructor.Invoke(new object[] { identifier });
				}
				catch (TargetInvocationException exception)
				{
					ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
					throw;
				}
			}

			public string ReadIdentifier(object label)
			{
				return Assert.IsType<string>(IdentifierProperty.GetValue(label, null));
			}

			public bool TypedEquals(object left, object right)
			{
				return Assert.IsType<bool>(TypedEquality.Invoke(left, new[] { right }));
			}

			public bool EquatableEquals(object left, object right)
			{
				return Assert.IsType<bool>(EquatableType.GetMethod("Equals").Invoke(left, new[] { right }));
			}

			private MethodInfo RequireMethod(string name, params Type[] parameterTypes)
			{
				var method = LabelType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
					null, parameterTypes, null);
				Assert.True(method != null, "Missing declared public instance member: " + name + ".");
				Assert.Equal(parameterTypes, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
				return method;
			}
		}
	}
}
