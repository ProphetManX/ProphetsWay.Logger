using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using ProphetsWay.Utilities;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	public class DestinationLabelPolicyTests
	{
		[Fact]
		public void ShouldExposeExactlyTheThreeReviewedNonFlagsModes()
		{
			var modeType = typeof(LabelFilterMode);
			modeType.IsPublic.ShouldBeTrue();
			modeType.IsEnum.ShouldBeTrue();
			modeType.Namespace.ShouldBe("ProphetsWay.Utilities");
			modeType.Assembly.ShouldBe(typeof(SensitivityLabel).Assembly);
			Enum.GetUnderlyingType(modeType).ShouldBe(typeof(int));
			modeType.IsDefined(typeof(FlagsAttribute), false).ShouldBeFalse();
			Enum.GetNames(modeType).ShouldBe(new[] { "NoFilter", "Exclude", "AllowOnly" });
			Enum.GetValues(modeType).Cast<LabelFilterMode>().Select(mode => (int)mode)
				.ShouldBe(new[] { 0, 1, 2 });
			((int)LabelFilterMode.NoFilter).ShouldBe(0);
			((int)LabelFilterMode.Exclude).ShouldBe(1);
			((int)LabelFilterMode.AllowOnly).ShouldBe(2);
		}

		[Fact]
		public void ShouldExposeTheExactReviewedPolicySurfaceInTheRealLibrary()
		{
			var contract = new PolicyContract();
			contract.PolicyType.IsPublic.ShouldBeTrue();
			contract.PolicyType.IsClass.ShouldBeTrue();
			contract.PolicyType.IsSealed.ShouldBeTrue();
			contract.PolicyType.IsAbstract.ShouldBeFalse();
			contract.PolicyType.ContainsGenericParameters.ShouldBeFalse();
			contract.PolicyType.BaseType.ShouldBe(typeof(object));
			contract.PolicyType.Assembly.GetName().Name.ShouldBe("ProphetsWay.Logger");
			contract.PolicyType.GetConstructors().ShouldHaveSingleItem();
			var parameters = contract.Constructor.GetParameters();
			parameters.Select(parameter => parameter.ParameterType)
				.ShouldBe(new[] { typeof(LabelFilterMode), typeof(IEnumerable<SensitivityLabel>) });
			parameters.Select(parameter => parameter.Name).ShouldBe(new[] { "mode", "labels" });
			parameters.Any(parameter => parameter.IsOptional).ShouldBeFalse();
			contract.ModeProperty.PropertyType.ShouldBe(typeof(LabelFilterMode));
			contract.LabelsProperty.PropertyType.ShouldBe(typeof(ReadOnlyCollection<SensitivityLabel>));
			foreach (var property in new[] { contract.ModeProperty, contract.LabelsProperty })
			{
				property.GetIndexParameters().ShouldBeEmpty();
				property.GetGetMethod().ShouldNotBeNull();
				property.GetSetMethod().ShouldBeNull();
			}
			contract.AllowsMethod.ReturnType.ShouldBe(typeof(bool));
			contract.AllowsMethod.ContainsGenericParameters.ShouldBeFalse();
			var effectiveParameter = contract.AllowsMethod.GetParameters().ShouldHaveSingleItem();
			effectiveParameter.ParameterType.ShouldBe(typeof(IEnumerable<SensitivityLabel>));
			effectiveParameter.Name.ShouldBe("effectiveLabels");
			effectiveParameter.IsOptional.ShouldBeFalse();
			var publicMembers = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
			var allowedMethods = new[]
			{
				contract.ModeProperty.GetGetMethod(), contract.LabelsProperty.GetGetMethod(), contract.AllowsMethod
			};
			var methods = contract.PolicyType.GetMethods(publicMembers);
			methods.Length.ShouldBe(allowedMethods.Length);
			foreach (var method in methods)
			{
				allowedMethods.ShouldContain(method);
			}
			contract.PolicyType.GetProperties(publicMembers).Length.ShouldBe(2);
			contract.PolicyType.GetFields(publicMembers).ShouldBeEmpty();
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter, new string[] { }, new string[] { }, true)]
		[InlineData(LabelFilterMode.NoFilter, new string[] { }, new[] { "unknown" }, true)]
		[InlineData(LabelFilterMode.NoFilter, new[] { "A" }, new string[] { }, true)]
		[InlineData(LabelFilterMode.NoFilter, new[] { "A" }, new[] { "A", "unknown" }, true)]
		[InlineData(LabelFilterMode.Exclude, new[] { "A" }, new[] { "A", "B" }, false)]
		[InlineData(LabelFilterMode.Exclude, new[] { "A" }, new[] { "B" }, true)]
		[InlineData(LabelFilterMode.Exclude, new[] { "A" }, new string[] { }, true)]
		[InlineData(LabelFilterMode.Exclude, new string[] { }, new[] { "unknown" }, true)]
		[InlineData(LabelFilterMode.Exclude, new string[] { }, new string[] { }, true)]
		[InlineData(LabelFilterMode.AllowOnly, new[] { "A", "B" }, new[] { "A" }, true)]
		[InlineData(LabelFilterMode.AllowOnly, new[] { "A", "B" }, new[] { "A", "B" }, true)]
		[InlineData(LabelFilterMode.AllowOnly, new[] { "A", "B" }, new[] { "A", "C" }, false)]
		[InlineData(LabelFilterMode.AllowOnly, new[] { "A", "B" }, new string[] { }, false)]
		[InlineData(LabelFilterMode.AllowOnly, new string[] { }, new[] { "unknown" }, false)]
		[InlineData(LabelFilterMode.AllowOnly, new string[] { }, new string[] { }, false)]
		public void ShouldApplyTheReviewedMembershipRule(LabelFilterMode mode, string[] configuredIdentifiers,
			string[] effectiveIdentifiers, bool expected)
		{
			var contract = new PolicyContract();
			var policy = contract.Create(mode, CreateLabels(configuredIdentifiers));
			contract.Allows(policy, CreateLabels(effectiveIdentifiers)).ShouldBe(expected);
			contract.ReadMode(policy).ShouldBe(mode);
		}

		[Theory]
		[InlineData(-1)]
		[InlineData(3)]
		[InlineData(4)]
		[InlineData(int.MinValue)]
		[InlineData(int.MaxValue)]
		public void ShouldRejectUndeclaredAndCombinedModesWithTheExactNamedError(int encodedMode)
		{
			var contract = new PolicyContract();
			ShouldRejectArgument<ArgumentOutOfRangeException>(
				() => contract.Create((LabelFilterMode)encodedMode, CreateLabels("A")), "mode");
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter)]
		[InlineData(LabelFilterMode.Exclude)]
		[InlineData(LabelFilterMode.AllowOnly)]
		public void ShouldRejectNullConfigurationInEveryMode(LabelFilterMode mode)
		{
			var contract = new PolicyContract();
			ShouldRejectArgument<ArgumentNullException>(() => contract.Create(mode, null), "labels");
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter)]
		[InlineData(LabelFilterMode.Exclude)]
		[InlineData(LabelFilterMode.AllowOnly)]
		public void ShouldRejectNullEffectiveInputEvenForEmptyConfiguration(LabelFilterMode mode)
		{
			var contract = new PolicyContract();
			foreach (var configured in new[] { CreateLabels(), CreateLabels("A") })
			{
				var policy = contract.Create(mode, configured);
				ShouldRejectArgument<ArgumentNullException>(() => contract.Allows(policy, null), "effectiveLabels");
			}
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter)]
		[InlineData(LabelFilterMode.Exclude)]
		[InlineData(LabelFilterMode.AllowOnly)]
		public void ShouldRejectNullConfigurationElementsAtEveryPosition(LabelFilterMode mode)
		{
			var contract = new PolicyContract();
			var valid = new SensitivityLabel("A");
			var invalidInputs = new[]
			{
				new SensitivityLabel[] { null }, new[] { null, valid },
				new[] { valid, null, valid }, new[] { valid, valid, null }
			};
			foreach (var labels in invalidInputs)
			{
				ShouldRejectArgument<ArgumentException>(() => contract.Create(mode, labels), "labels");
			}
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter)]
		[InlineData(LabelFilterMode.Exclude)]
		[InlineData(LabelFilterMode.AllowOnly)]
		public void ShouldRejectEveryNullEffectiveElementDespiteDecisivePrefixes(LabelFilterMode mode)
		{
			var contract = new PolicyContract();
			var matching = new SensitivityLabel("A");
			var unknown = new SensitivityLabel("unknown");
			var invalidInputs = new[]
			{
				new SensitivityLabel[] { null }, new[] { null, matching },
				new[] { matching, null, unknown }, new[] { matching, unknown, null },
				new[] { unknown, matching, null }
			};
			foreach (var configured in new[] { CreateLabels(), CreateLabels("A") })
			{
				var policy = contract.Create(mode, configured);
				foreach (var labels in invalidInputs)
				{
					ShouldRejectArgument<ArgumentException>(() => contract.Allows(policy, labels), "effectiveLabels");
				}
				ShouldHaveMembership(contract.ReadLabels(policy), configured.Select(label => label.Identifier).ToArray());
				contract.ReadMode(policy).ShouldBe(mode);
				contract.Allows(policy, CreateLabels("A")).ShouldBe(
					mode == LabelFilterMode.NoFilter || (mode == LabelFilterMode.Exclude ? configured.Length == 0 : configured.Length != 0));
			}
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter)]
		[InlineData(LabelFilterMode.Exclude)]
		[InlineData(LabelFilterMode.AllowOnly)]
		public void ShouldUseUnknownValueIdentitiesWithoutDuplicateOrOrderSensitivity(LabelFilterMode mode)
		{
			var contract = new PolicyContract();
			var policy = contract.Create(mode, CreateLabels("app:unregistered", "B", "app:unregistered"));
			ShouldHaveMembership(contract.ReadLabels(policy), "app:unregistered", "B");
			foreach (var labels in new[]
			{
				CreateLabels("app:unregistered"), CreateLabels("B", "app:unregistered", "app:unregistered"),
				CreateLabels("app:unregistered", "B")
			})
			{
				contract.Allows(policy, labels).ShouldBe(mode != LabelFilterMode.Exclude);
			}
			foreach (var labels in new[] { CreateLabels("B", "other:unknown"), CreateLabels("other:unknown", "B", "B") })
			{
				contract.Allows(policy, labels).ShouldBe(mode == LabelFilterMode.NoFilter);
			}
			contract.Allows(policy, CreateLabels("other:unknown")).ShouldBe(mode != LabelFilterMode.AllowOnly);
			ShouldHaveMembership(contract.ReadLabels(policy), "app:unregistered", "B");
		}

		[Theory]
		[InlineData("PII", "pii")]
		[InlineData("\u00E9", "e\u0301")]
		[InlineData("ab", "a\u00ADb")]
		[InlineData("I", "\u0131")]
		public void ShouldMatchOrdinalValuesWithoutCaseFoldingOrUnicodeNormalization(string identifier, string distinctIdentifier)
		{
			var contract = new PolicyContract();
			foreach (var mode in new[] { LabelFilterMode.NoFilter, LabelFilterMode.Exclude, LabelFilterMode.AllowOnly })
			{
				var policy = contract.Create(mode, CreateLabels(identifier));
				contract.Allows(policy, CreateLabels(new string(identifier.ToCharArray()))).ShouldBe(mode != LabelFilterMode.Exclude);
				contract.Allows(policy, CreateLabels(distinctIdentifier)).ShouldBe(mode != LabelFilterMode.AllowOnly);
				ShouldHaveMembership(contract.ReadLabels(policy), identifier);
				var pair = contract.Create(mode, CreateLabels(identifier, distinctIdentifier, identifier));
				ShouldHaveMembership(contract.ReadLabels(pair), identifier, distinctIdentifier);
				contract.Allows(pair, CreateLabels(distinctIdentifier, identifier)).ShouldBe(mode != LabelFilterMode.Exclude);
			}
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter)]
		[InlineData(LabelFilterMode.Exclude)]
		[InlineData(LabelFilterMode.AllowOnly)]
		public void ShouldCopyConfigurationBeforeReturnAndRetainItAfterSourceAdditionsAndRemovals(LabelFilterMode mode)
		{
			var contract = new PolicyContract();
			var source = CreateLabels("B", "A", "A").ToList();
			var originalReferences = source.ToArray();
			var policy = contract.Create(mode, source);
			ShouldPreserveInput(source, originalReferences, "B", "A", "A");
			var firstRead = contract.ReadLabels(policy);
			ShouldHaveMembership(firstRead, "A", "B");
			source.Clear();
			source.Add(new SensitivityLabel("replacement"));
			ShouldHaveMembership(firstRead, "A", "B");
			ShouldHaveMembership(contract.ReadLabels(policy), "A", "B");
			contract.Allows(policy, CreateLabels("A")).ShouldBe(mode != LabelFilterMode.Exclude);
			contract.Allows(policy, CreateLabels("B")).ShouldBe(mode != LabelFilterMode.Exclude);
			contract.Allows(policy, CreateLabels("replacement")).ShouldBe(mode != LabelFilterMode.AllowOnly);
			contract.ReadMode(policy).ShouldBe(mode);
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter)]
		[InlineData(LabelFilterMode.Exclude)]
		[InlineData(LabelFilterMode.AllowOnly)]
		public void ShouldNotBorrowWritableArrayBackingStorage(LabelFilterMode mode)
		{
			var contract = new PolicyContract();
			var source = CreateLabels("A");
			var policy = contract.Create(mode, source);
			var firstRead = contract.ReadLabels(policy);
			source[0] = new SensitivityLabel("B");
			ShouldHaveMembership(firstRead, "A");
			ShouldHaveMembership(contract.ReadLabels(policy), "A");
			contract.Allows(policy, CreateLabels("A")).ShouldBe(mode != LabelFilterMode.Exclude);
			contract.Allows(policy, CreateLabels("B")).ShouldBe(mode != LabelFilterMode.AllowOnly);
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter)]
		[InlineData(LabelFilterMode.Exclude)]
		[InlineData(LabelFilterMode.AllowOnly)]
		public void ShouldCaptureFiniteIteratorMembershipBeforeAnyPolicyMemberIsRead(LabelFilterMode mode)
		{
			var contract = new PolicyContract();
			var source = CreateLabels("B", "A", "A").ToList();
			var policy = contract.Create(mode, Enumerate(source));
			source.Clear();
			source.Add(new SensitivityLabel("replacement"));
			ShouldHaveMembership(contract.ReadLabels(policy), "A", "B");
			contract.Allows(policy, Enumerate(CreateLabels("B", "A", "B"))).ShouldBe(mode != LabelFilterMode.Exclude);
			contract.Allows(policy, Enumerate(CreateLabels("replacement"))).ShouldBe(mode != LabelFilterMode.AllowOnly);
			contract.Allows(policy, Enumerate(CreateLabels())).ShouldBe(mode != LabelFilterMode.AllowOnly);
			contract.ReadMode(policy).ShouldBe(mode);
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter)]
		[InlineData(LabelFilterMode.Exclude)]
		[InlineData(LabelFilterMode.AllowOnly)]
		public void ShouldRejectReturnedCollectionMutationsWithoutChangingPolicy(LabelFilterMode mode)
		{
			var contract = new PolicyContract();
			var policy = contract.Create(mode, CreateLabels("A", "B"));
			var readback = contract.ReadLabels(policy);
			ShouldHaveMembership(readback, "A", "B");
			var generic = (IList<SensitivityLabel>)readback;
			var untyped = (IList)readback;
			var extra = new SensitivityLabel("extra");
			var mutations = new Action[]
			{
				() => generic.Add(extra), () => generic.Clear(), () => generic[0] = extra,
				() => generic.Insert(0, extra), () => generic.Remove(readback[0]), () => generic.RemoveAt(0),
				() => untyped.Add(extra), () => untyped.Clear(), () => untyped[0] = extra,
				() => untyped.Insert(0, extra), () => untyped.Remove(readback[0]), () => untyped.RemoveAt(0)
			};
			foreach (var mutation in mutations)
			{
				Should.Throw<NotSupportedException>(mutation);
				ShouldHaveMembership(readback, "A", "B");
				ShouldHaveMembership(contract.ReadLabels(policy), "A", "B");
				contract.ReadMode(policy).ShouldBe(mode);
				contract.Allows(policy, CreateLabels("A")).ShouldBe(mode != LabelFilterMode.Exclude);
				contract.Allows(policy, CreateLabels("extra")).ShouldBe(mode != LabelFilterMode.AllowOnly);
			}
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter)]
		[InlineData(LabelFilterMode.Exclude)]
		[InlineData(LabelFilterMode.AllowOnly)]
		public void ShouldPreserveCallerInputsAndPolicyAcrossRepeatedDifferentEvaluations(LabelFilterMode mode)
		{
			var contract = new PolicyContract();
			var source = CreateLabels("B", "A", "A").ToList();
			var sourceReferences = source.ToArray();
			var policy = contract.Create(mode, source);
			var effective = CreateLabels("B", "A", "B").ToList();
			var effectiveReferences = effective.ToArray();
			for (var repetition = 0; repetition < 3; repetition++)
			{
				contract.Allows(policy, effective).ShouldBe(mode != LabelFilterMode.Exclude);
				contract.Allows(policy, CreateLabels("unknown")).ShouldBe(mode != LabelFilterMode.AllowOnly);
				contract.Allows(policy, CreateLabels()).ShouldBe(mode != LabelFilterMode.AllowOnly);
				ShouldPreserveInput(source, sourceReferences, "B", "A", "A");
				ShouldPreserveInput(effective, effectiveReferences, "B", "A", "B");
				ShouldHaveMembership(contract.ReadLabels(policy), "A", "B");
				contract.ReadMode(policy).ShouldBe(mode);
			}
		}

		[Theory]
		[InlineData("GetEnumerator")]
		[InlineData("MoveNext")]
		[InlineData("Current")]
		[InlineData("Dispose")]
		public void ShouldFailConstructionWithoutReturningAPartialPolicyWhenSequenceAccessFails(string failurePoint)
		{
			var contract = new PolicyContract();
			foreach (var mode in new[] { LabelFilterMode.NoFilter, LabelFilterMode.Exclude, LabelFilterMode.AllowOnly })
			{
				object returned = null;
				var failure = Record.Exception(() =>
				{
					returned = contract.Create(mode, new FailingSequence(CreateLabels("A", "B"), failurePoint));
				});
				failure.ShouldNotBeNull("A failing configuration sequence must not produce a usable policy.");
				returned.ShouldBeNull();
			}
		}

		[Theory]
		[InlineData("GetEnumerator")]
		[InlineData("MoveNext")]
		[InlineData("Current")]
		[InlineData("Dispose")]
		public void ShouldFailEvaluationWithoutReturningAPrefixResultOrMutatingPolicy(string failurePoint)
		{
			var contract = new PolicyContract();
			foreach (var mode in new[] { LabelFilterMode.NoFilter, LabelFilterMode.Exclude, LabelFilterMode.AllowOnly })
			{
				foreach (var configured in new[] { CreateLabels(), CreateLabels("A") })
				{
					var policy = contract.Create(mode, configured);
					foreach (var prefix in new[] { "A", "unknown" })
					{
						bool? returned = null;
						var failure = Record.Exception(() =>
						{
							returned = contract.Allows(policy, new FailingSequence(CreateLabels(prefix, "B"), failurePoint));
						});
						failure.ShouldNotBeNull("A failing effective sequence must not produce a Boolean result.");
						returned.ShouldBeNull();
						ShouldHaveMembership(contract.ReadLabels(policy), configured.Select(label => label.Identifier).ToArray());
						contract.ReadMode(policy).ShouldBe(mode);
						contract.Allows(policy, CreateLabels("A")).ShouldBe(
							mode == LabelFilterMode.NoFilter || (mode == LabelFilterMode.Exclude ? configured.Length == 0 : configured.Length != 0));
						contract.Allows(policy, CreateLabels("unknown")).ShouldBe(mode != LabelFilterMode.AllowOnly);
					}
				}
			}
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter, new[] { true, true, true, true, true })]
		[InlineData(LabelFilterMode.Exclude, new[] { false, true, true, false, false })]
		[InlineData(LabelFilterMode.AllowOnly, new[] { true, false, false, false, true })]
		public async Task ShouldPreserveResultsAndMembershipDuringConcurrentStableReads(LabelFilterMode mode, bool[] expected)
		{
			var contract = new PolicyContract();
			var policy = contract.Create(mode, CreateLabels("A", "B"));
			var inputs = new[]
			{
				CreateLabels("A"), CreateLabels("unknown"), CreateLabels(),
				CreateLabels("A", "unknown"), CreateLabels("B", "A", "B")
			};
			var readers = Enumerable.Range(0, 8).Select(reader => Task.Run(() =>
			{
				var results = inputs.Select(labels => contract.Allows(policy, labels)).ToArray();
				contract.ReadMode(policy).ShouldBe(mode);
				ShouldHaveMembership(contract.ReadLabels(policy), "A", "B");
				return results;
			})).ToArray();
			foreach (var result in await Task.WhenAll(readers))
			{
				result.ShouldBe(expected);
			}
			ShouldHaveMembership(contract.ReadLabels(policy), "A", "B");
			contract.ReadMode(policy).ShouldBe(mode);
		}

		private static void ShouldRejectArgument<TException>(Action action, string parameterName) where TException : ArgumentException
		{
			var exception = Should.Throw<TException>(action);
			exception.GetType().ShouldBe(typeof(TException));
			exception.ParamName.ShouldBe(parameterName);
		}

		private static void ShouldHaveMembership(ReadOnlyCollection<SensitivityLabel> labels, params string[] identifiers)
		{
			labels.ShouldNotBeNull();
			labels.Count.ShouldBe(identifiers.Length);
			foreach (var label in labels)
			{
				label.ShouldNotBeNull();
			}
			labels.Select(label => label.Identifier).OrderBy(identifier => identifier, StringComparer.Ordinal)
				.SequenceEqual(identifiers.OrderBy(identifier => identifier, StringComparer.Ordinal), StringComparer.Ordinal)
				.ShouldBeTrue("Readback must contain exactly the captured ordinal identities, with no duplicate representatives.");
		}

		private static void ShouldPreserveInput(IList<SensitivityLabel> actual, SensitivityLabel[] references, params string[] identifiers)
		{
			actual.Count.ShouldBe(references.Length);
			for (var index = 0; index < references.Length; index++)
			{
				ReferenceEquals(actual[index], references[index]).ShouldBeTrue();
			}
			actual.Select(label => label.Identifier).SequenceEqual(identifiers, StringComparer.Ordinal).ShouldBeTrue();
		}

		private static SensitivityLabel[] CreateLabels(params string[] identifiers)
		{
			return identifiers.Select(identifier => new SensitivityLabel(identifier)).ToArray();
		}

		private static IEnumerable<SensitivityLabel> Enumerate(IEnumerable<SensitivityLabel> labels)
		{
			foreach (var label in labels)
			{
				yield return label;
			}
		}

		private sealed class FailingSequence : IEnumerable<SensitivityLabel>
		{
			private readonly SensitivityLabel[] _labels;
			private readonly string _failurePoint;

			public FailingSequence(SensitivityLabel[] labels, string failurePoint)
			{
				_labels = labels;
				_failurePoint = failurePoint;
			}

			public IEnumerator<SensitivityLabel> GetEnumerator()
			{
				if (_failurePoint == "GetEnumerator")
				{
					throw new InvalidOperationException("Synthetic sequence access failure.");
				}
				return new FailingEnumerator(_labels, _failurePoint);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			private sealed class FailingEnumerator : IEnumerator<SensitivityLabel>
			{
				private readonly SensitivityLabel[] _labels;
				private readonly string _failurePoint;
				private int _position = -1;

				public FailingEnumerator(SensitivityLabel[] labels, string failurePoint)
				{
					_labels = labels;
					_failurePoint = failurePoint;
				}

				public SensitivityLabel Current
				{
					get
					{
						if (_failurePoint == "Current" && _position == 1)
						{
							throw new InvalidOperationException("Synthetic sequence access failure.");
						}
						return _labels[_position];
					}
				}

				object IEnumerator.Current => Current;

				public bool MoveNext()
				{
					_position++;
					if (_failurePoint == "MoveNext" && _position == 1)
					{
						throw new InvalidOperationException("Synthetic sequence access failure.");
					}
					return _position < _labels.Length;
				}

				public void Dispose()
				{
					if (_failurePoint == "Dispose")
					{
						throw new InvalidOperationException("Synthetic sequence access failure.");
					}
				}

				public void Reset()
				{
					throw new NotSupportedException();
				}
			}
		}

		private sealed class PolicyContract
		{
			public Type PolicyType { get; }
			public ConstructorInfo Constructor { get; }
			public PropertyInfo ModeProperty { get; }
			public PropertyInfo LabelsProperty { get; }
			public MethodInfo AllowsMethod { get; }

			public PolicyContract()
			{
				PolicyType = typeof(SensitivityLabel).Assembly.GetType("ProphetsWay.Utilities.DestinationLabelPolicy", false, false);
				PolicyType.ShouldNotBeNull("Missing public contract: ProphetsWay.Utilities.DestinationLabelPolicy in the real referenced ProphetsWay.Logger assembly.");
				Constructor = PolicyType.GetConstructor(new[] { typeof(LabelFilterMode), typeof(IEnumerable<SensitivityLabel>) });
				Constructor.ShouldNotBeNull("Missing public DestinationLabelPolicy(LabelFilterMode mode, IEnumerable<SensitivityLabel> labels) constructor.");
				ModeProperty = PolicyType.GetProperty("Mode", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				ModeProperty.ShouldNotBeNull("Missing public Mode property.");
				LabelsProperty = PolicyType.GetProperty("Labels", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				LabelsProperty.ShouldNotBeNull("Missing public Labels property.");
				AllowsMethod = PolicyType.GetMethod("Allows", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
					null, new[] { typeof(IEnumerable<SensitivityLabel>) }, null);
				AllowsMethod.ShouldNotBeNull("Missing public Allows(IEnumerable<SensitivityLabel> effectiveLabels) method.");
			}

			public object Create(LabelFilterMode mode, IEnumerable<SensitivityLabel> labels)
			{
				return Invoke(() => Constructor.Invoke(new object[] { mode, labels }));
			}

			public LabelFilterMode ReadMode(object policy)
			{
				return (LabelFilterMode)Invoke(() => ModeProperty.GetValue(policy, null));
			}

			public ReadOnlyCollection<SensitivityLabel> ReadLabels(object policy)
			{
				return (ReadOnlyCollection<SensitivityLabel>)Invoke(() => LabelsProperty.GetValue(policy, null));
			}

			public bool Allows(object policy, IEnumerable<SensitivityLabel> labels)
			{
				return (bool)Invoke(() => AllowsMethod.Invoke(policy, new object[] { labels }));
			}

			private static object Invoke(Func<object> member)
			{
				try
				{
					return member();
				}
				catch (TargetInvocationException exception)
				{
					ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
					throw;
				}
			}
		}

		[Theory]
		[InlineData(LabelFilterMode.NoFilter, "Replace", true)]
		[InlineData(LabelFilterMode.Exclude, "Replace", false)]
		[InlineData(LabelFilterMode.AllowOnly, "Replace", true)]
		[InlineData(LabelFilterMode.AllowOnly, "ReplaceWithNull", false)]
		[InlineData(LabelFilterMode.Exclude, "Duplicate", true)]
		[InlineData(LabelFilterMode.NoFilter, "Add", false)]
		[InlineData(LabelFilterMode.AllowOnly, "AddNull", true)]
		[InlineData(LabelFilterMode.Exclude, "Remove", true)]
		[InlineData(LabelFilterMode.NoFilter, "RemoveAt", false)]
		[InlineData(LabelFilterMode.AllowOnly, "Clear", false)]
		public void ShouldPreserveOwnedPolicyMembershipThroughSyncRootMutationAttempts(
			LabelFilterMode mode, string operation, bool useGeneric)
		{
			var contract = new PolicyContract();
			var policy = contract.Create(mode, CreateLabels("B", "A", "A"));
			var retainedView = contract.ReadLabels(policy);
			var independentInputs = new[]
			{
				CreateLabels("A"), CreateLabels("B"), CreateLabels("replacement"), CreateLabels(),
				CreateLabels("A", "replacement"), CreateLabels("B", "A", "A"), CreateLabels("a")
			};
			var expectedResults = mode == LabelFilterMode.NoFilter
				? new[] { true, true, true, true, true, true, true }
				: mode == LabelFilterMode.Exclude
					? new[] { false, false, true, true, false, false, true }
					: new[] { true, true, false, false, false, true, false };
			Action verifyPolicyUnchanged = () =>
			{
				contract.ReadMode(policy).ShouldBe(mode);
				independentInputs.Select(labels => contract.Allows(policy, labels)).ToArray().ShouldBe(expectedResults);
				ShouldHaveMembership(retainedView, "A", "B");
				ShouldHaveMembership(contract.ReadLabels(policy), "A", "B");
			};
			verifyPolicyUnchanged();
			var syncRoot = ((ICollection)retainedView).SyncRoot;
			var replacement = new SensitivityLabel("replacement");
			var duplicate = new SensitivityLabel("A");
			var mutations = new List<Action>();
			if (useGeneric)
			{
				if (syncRoot is IList<SensitivityLabel> genericRoot)
				{
					switch (operation)
					{
						case "Replace":
							if (genericRoot.Count > 0) mutations.Add(() => genericRoot[0] = replacement);
							break;
						case "Duplicate":
							if (genericRoot.Count > 0) mutations.Add(() => genericRoot[0] = duplicate);
							if (genericRoot.Count > 1) mutations.Add(() => genericRoot[1] = duplicate);
							break;
					}
				}
				if (syncRoot is ICollection<SensitivityLabel> genericCollection)
				{
					switch (operation)
					{
						case "AddNull":
							mutations.Add(() => genericCollection.Add(null));
							break;
						case "Remove":
							mutations.Add(() => genericCollection.Remove(new SensitivityLabel("A")));
							break;
					}
				}
			}
			else if (syncRoot is IList untypedRoot)
			{
				switch (operation)
				{
					case "Replace":
						if (untypedRoot.Count > 0) mutations.Add(() => untypedRoot[0] = replacement);
						break;
					case "ReplaceWithNull":
						if (untypedRoot.Count > 0) mutations.Add(() => untypedRoot[0] = null);
						break;
					case "Add":
						mutations.Add(() => untypedRoot.Add(replacement));
						break;
					case "RemoveAt":
						if (untypedRoot.Count > 0) mutations.Add(() => untypedRoot.RemoveAt(0));
						break;
					case "Clear":
						mutations.Add(() => untypedRoot.Clear());
						break;
				}
			}
			foreach (var mutation in mutations)
			{
				Record.Exception(mutation);
				verifyPolicyUnchanged();
			}
			verifyPolicyUnchanged();
		}
	}
}