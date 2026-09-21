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
	public class LogAnnotationsTests
	{
		[Fact]
		public void ShouldExposeTheExactReviewedImmutableAnnotationSurfaceInTheRealLibrary()
		{
			var contract = new AnnotationContract();
			contract.AnnotationType.IsPublic.ShouldBeTrue();
			contract.AnnotationType.IsClass.ShouldBeTrue();
			contract.AnnotationType.IsSealed.ShouldBeTrue();
			contract.AnnotationType.IsAbstract.ShouldBeFalse();
			contract.AnnotationType.ContainsGenericParameters.ShouldBeFalse();
			contract.AnnotationType.BaseType.ShouldBe(typeof(object));
			contract.AnnotationType.Assembly.GetName().Name.ShouldBe("ProphetsWay.Logger");
			contract.AnnotationType.GetInterfaces().ShouldBeEmpty();
			contract.AnnotationType.GetConstructors().ShouldHaveSingleItem();
			var parameter = contract.Constructor.GetParameters().ShouldHaveSingleItem();
			parameter.ParameterType.ShouldBe(typeof(IEnumerable<SensitivityLabel>));
			parameter.Name.ShouldBe("labels");
			parameter.IsOptional.ShouldBeFalse();
			contract.OccurrencesProperty.PropertyType.ShouldBe(typeof(ReadOnlyCollection<SensitivityLabel>));
			contract.OccurrencesProperty.GetIndexParameters().ShouldBeEmpty();
			contract.OccurrencesProperty.GetGetMethod().ShouldNotBeNull();
			contract.OccurrencesProperty.GetSetMethod().ShouldBeNull();
			var publicMembers = contract.AnnotationType.GetMembers(
				BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
			publicMembers.Length.ShouldBe(3);
			publicMembers.ShouldContain(contract.Constructor);
			publicMembers.ShouldContain(contract.OccurrencesProperty);
			publicMembers.ShouldContain(contract.OccurrencesProperty.GetGetMethod());
		}

		[Fact]
		public void ShouldExposeANonNullEmptyOccurrenceListForAnEmptyAttachment()
		{
			var contract = new AnnotationContract();
			var annotations = contract.Construct(Array.Empty<SensitivityLabel>());
			var occurrences = contract.ReadOccurrences(annotations);
			ShouldHaveOccurrences(occurrences);
			Should.Throw<NotSupportedException>(() => ((ICollection<SensitivityLabel>)occurrences).Add(new SensitivityLabel("extra")));
			Should.Throw<NotSupportedException>(() => ((IList)occurrences).Clear());
			ShouldHaveOccurrences(contract.ReadOccurrences(annotations));
		}

		[Fact]
		public void ShouldPreserveUnknownOrdinalValuesInOrderIncludingEveryRepeatedOccurrence()
		{
			var contract = new AnnotationContract();
			var repeated = new SensitivityLabel("unknown:Z/v1");
			var annotations = contract.Construct(new[]
			{
				repeated, new SensitivityLabel("A"), new SensitivityLabel("unknown:Z/v1"), repeated,
				new SensitivityLabel("a"), new SensitivityLabel("\u00E9"), new SensitivityLabel("e\u0301")
			});
			ShouldHaveOccurrences(contract.ReadOccurrences(annotations),
				"unknown:Z/v1", "A", "unknown:Z/v1", "unknown:Z/v1", "a", "\u00E9", "e\u0301");
		}

		[Fact]
		public void ShouldRejectANullSequenceWithTheNamedArgumentError()
		{
			var contract = new AnnotationContract();
			ShouldRejectLabels<ArgumentNullException>(() => contract.Construct(null));
		}

		[Theory]
		[InlineData(1, 0)]
		[InlineData(3, 0)]
		[InlineData(3, 1)]
		[InlineData(3, 2)]
		public void ShouldRejectNullOccurrencesAtEveryPositionWithTheNamedArgumentError(int length, int nullIndex)
		{
			var contract = new AnnotationContract();
			var source = Enumerable.Range(0, length).Select(index => new SensitivityLabel("valid:" + index)).ToArray();
			source[nullIndex] = null;
			ShouldRejectLabels<ArgumentException>(() => contract.Construct(source));
		}

		[Fact]
		public void ShouldRejectANullIteratorTailBeforeReturningAnyAttachment()
		{
			var contract = new AnnotationContract();
			var source = new[] { new SensitivityLabel("A"), new SensitivityLabel("A"), null };
			object returned = null;
			ShouldRejectLabels<ArgumentException>(() => returned = contract.Construct(Enumerate(source)));
			returned.ShouldBeNull();
		}

		[Fact]
		public void ShouldKeepSeparateCapturesIndependentOfLaterListEditsBeforeTheirFirstRead()
		{
			var contract = new AnnotationContract();
			var source = new List<SensitivityLabel>
			{
				new SensitivityLabel("B"), new SensitivityLabel("A"), new SensitivityLabel("B")
			};
			var first = contract.Construct(source);
			source[0] = new SensitivityLabel("replacement");
			source.RemoveAt(1);
			source.Add(new SensitivityLabel("later"));
			var second = contract.Construct(source);
			source.Clear();
			ShouldHaveOccurrences(contract.ReadOccurrences(first), "B", "A", "B");
			ShouldHaveOccurrences(contract.ReadOccurrences(second), "replacement", "B", "later");
		}

		[Fact]
		public void ShouldOwnArrayMembershipInsteadOfBorrowingWritableBacking()
		{
			var contract = new AnnotationContract();
			var source = new[] { new SensitivityLabel("B"), new SensitivityLabel("A"), new SensitivityLabel("B") };
			var annotations = contract.Construct(source);
			var retainedView = contract.ReadOccurrences(annotations);
			source[0] = new SensitivityLabel("replacement");
			source[2] = null;
			ShouldHaveOccurrences(retainedView, "B", "A", "B");
			ShouldHaveOccurrences(contract.ReadOccurrences(annotations), "B", "A", "B");
		}

		[Fact]
		public void ShouldCopyEvenAReadOnlyViewOfCallerOwnedMutableMembership()
		{
			var contract = new AnnotationContract();
			var source = new List<SensitivityLabel> { new SensitivityLabel("B"), new SensitivityLabel("A") };
			var annotations = contract.Construct(source.AsReadOnly());
			source.Clear();
			source.Add(new SensitivityLabel("replacement"));
			ShouldHaveOccurrences(contract.ReadOccurrences(annotations), "B", "A");
		}

		[Fact]
		public void ShouldFinishCopyingFiniteIteratorMembershipBeforeAnyOccurrenceIsRead()
		{
			var contract = new AnnotationContract();
			var source = new List<SensitivityLabel>
			{
				new SensitivityLabel("B"), new SensitivityLabel("A"), new SensitivityLabel("B")
			};
			var annotations = contract.Construct(Enumerate(source));
			source.Clear();
			source.Add(new SensitivityLabel("replacement"));
			ShouldHaveOccurrences(contract.ReadOccurrences(annotations), "B", "A", "B");
		}

		[Fact]
		public void ShouldRejectCollectionInterfaceWritesWithoutChangingAnyOccurrence()
		{
			var contract = new AnnotationContract();
			var annotations = contract.Construct(new[]
			{
				new SensitivityLabel("B"), new SensitivityLabel("A"), new SensitivityLabel("B")
			});
			var occurrences = contract.ReadOccurrences(annotations);
			ShouldHaveOccurrences(occurrences, "B", "A", "B");
			var generic = (IList<SensitivityLabel>)occurrences;
			var untyped = (IList)occurrences;
			var extra = new SensitivityLabel("extra");
			var mutations = new Action[]
			{
				() => generic.Add(extra), () => generic.Clear(), () => generic[0] = extra,
				() => generic.Insert(1, extra), () => generic.Remove(occurrences[0]), () => generic.RemoveAt(0),
				() => untyped.Add(extra), () => untyped.Clear(), () => untyped[0] = extra,
				() => untyped.Insert(1, extra), () => untyped.Remove(occurrences[0]), () => untyped.RemoveAt(0)
			};
			foreach (var mutation in mutations)
			{
				Should.Throw<NotSupportedException>(mutation);
				ShouldHaveOccurrences(occurrences, "B", "A", "B");
				ShouldHaveOccurrences(contract.ReadOccurrences(annotations), "B", "A", "B");
			}
		}

		[Theory]
		[InlineData("GetEnumerator")]
		[InlineData("MoveNext")]
		[InlineData("Current")]
		[InlineData("Dispose")]
		public void ShouldFailConstructionWithoutReturningAPrefixWhenFiniteSequenceAccessFails(string failurePoint)
		{
			var contract = new AnnotationContract();
			var source = new FailingSequence(new[]
			{
				new SensitivityLabel("A"), new SensitivityLabel("B"), new SensitivityLabel("A")
			}, failurePoint);
			object returned = null;
			var failure = Record.Exception(() =>
			{
				returned = contract.Construct(source);
			});
			failure.ShouldNotBeNull("Failed finite capture, including disposal, must not return a usable attachment.");
			returned.ShouldBeNull();
		}

		[Fact]
		public async Task ShouldPreserveOrderedOccurrencesDuringConcurrentStableReads()
		{
			var contract = new AnnotationContract();
			var annotations = contract.Construct(new[]
			{
				new SensitivityLabel("B"), new SensitivityLabel("A"), new SensitivityLabel("B")
			});
			var retainedView = contract.ReadOccurrences(annotations);
			var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			var readers = Enumerable.Range(0, 8).Select(reader => Task.Run(async () =>
			{
				await start.Task;
				for (var repetition = 0; repetition < 16; repetition++)
				{
					ShouldHaveOccurrences(retainedView, "B", "A", "B");
					ShouldHaveOccurrences(contract.ReadOccurrences(annotations), "B", "A", "B");
				}
			})).ToArray();
			start.SetResult(true);
			await Task.WhenAll(readers);
			ShouldHaveOccurrences(contract.ReadOccurrences(annotations), "B", "A", "B");
		}

		private static void ShouldRejectLabels<TException>(Action action) where TException : ArgumentException
		{
			var exception = Should.Throw<TException>(action);
			exception.GetType().ShouldBe(typeof(TException));
			exception.ParamName.ShouldBe("labels");
		}

		private static void ShouldHaveOccurrences(ReadOnlyCollection<SensitivityLabel> occurrences, params string[] identifiers)
		{
			occurrences.ShouldNotBeNull();
			occurrences.Count.ShouldBe(identifiers.Length);
			for (var index = 0; index < identifiers.Length; index++)
			{
				occurrences[index].ShouldNotBeNull();
				occurrences[index].ShouldBe(new SensitivityLabel(identifiers[index]));
			}
			occurrences.Select(label => label.Identifier).SequenceEqual(identifiers, StringComparer.Ordinal)
				.ShouldBeTrue("Readback must preserve exactly the ordered ordinal label occurrences, including repeats.");
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
						if (_failurePoint == "Current" && _position == _labels.Length - 1)
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
					if (_failurePoint == "MoveNext" && _position == _labels.Length)
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

		private sealed class AnnotationContract
		{
			public Type AnnotationType { get; }
			public ConstructorInfo Constructor { get; }
			public PropertyInfo OccurrencesProperty { get; }

			public AnnotationContract()
			{
				AnnotationType = typeof(SensitivityLabel).Assembly.GetType("ProphetsWay.Utilities.LogAnnotations", false, false);
				AnnotationType.ShouldNotBeNull("Missing public contract: ProphetsWay.Utilities.LogAnnotations in the real referenced ProphetsWay.Logger assembly.");
				Constructor = AnnotationType.GetConstructor(new[] { typeof(IEnumerable<SensitivityLabel>) });
				Constructor.ShouldNotBeNull("Missing public LogAnnotations(IEnumerable<SensitivityLabel> labels) constructor.");
				OccurrencesProperty = AnnotationType.GetProperty("LabelOccurrences", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				OccurrencesProperty.ShouldNotBeNull("Missing public LabelOccurrences property.");
			}

			public object Construct(IEnumerable<SensitivityLabel> labels)
			{
				return Invoke(() => Constructor.Invoke(new object[] { labels }));
			}

			public ReadOnlyCollection<SensitivityLabel> ReadOccurrences(object annotations)
			{
				return (ReadOnlyCollection<SensitivityLabel>)Invoke(() => OccurrencesProperty.GetValue(annotations, null));
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
		[InlineData("Replace")]
		[InlineData("ReplaceWithNull")]
		[InlineData("Add")]
		[InlineData("AddNull")]
		[InlineData("RemoveAt")]
		[InlineData("Clear")]
		public void ShouldPreserveOwnedOccurrencesThroughSyncRootMutationAttempts(string operation)
		{
			var contract = new AnnotationContract();
			var annotations = contract.Construct(new[]
			{
				new SensitivityLabel("B"), new SensitivityLabel("A"), new SensitivityLabel("B")
			});
			var occurrences = contract.ReadOccurrences(annotations);
			ShouldHaveOccurrences(occurrences, "B", "A", "B");
			var syncRoot = ((ICollection)occurrences).SyncRoot;
			var replacement = new SensitivityLabel("replacement");
			var mutations = new List<Action>();
			if (syncRoot is IList<SensitivityLabel> genericRoot)
			{
				switch (operation)
				{
					case "Replace":
						if (genericRoot.Count > 0) mutations.Add(() => genericRoot[0] = replacement);
						break;
					case "ReplaceWithNull":
						if (genericRoot.Count > 0) mutations.Add(() => genericRoot[0] = null);
						break;
					case "Add":
						mutations.Add(() => genericRoot.Add(replacement));
						break;
					case "AddNull":
						mutations.Add(() => genericRoot.Add(null));
						break;
					case "RemoveAt":
						if (genericRoot.Count > 0) mutations.Add(() => genericRoot.RemoveAt(0));
						break;
					case "Clear":
						mutations.Add(() => genericRoot.Clear());
						break;
				}
			}
			if (syncRoot is IList untypedRoot)
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
					case "AddNull":
						mutations.Add(() => untypedRoot.Add(null));
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
				ShouldHaveOccurrences(occurrences, "B", "A", "B");
				ShouldHaveOccurrences(contract.ReadOccurrences(annotations), "B", "A", "B");
			}
			ShouldHaveOccurrences(occurrences, "B", "A", "B");
			ShouldHaveOccurrences(contract.ReadOccurrences(annotations), "B", "A", "B");
		}
	}
}
