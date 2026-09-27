using System;
using System.Collections.Generic;
using System.Linq;
using ProphetsWay.Utilities;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	public class DestinationRegistrationSettingsTests
	{
		[Fact]
		public void ShouldRequireExplicitImmutableRegistrationSettings()
		{
			var type = typeof(DestinationRegistrationSettings);
			type.IsSealed.ShouldBeTrue();
			var constructor = type.GetConstructors().ShouldHaveSingleItem();
			constructor.GetParameters().Select(parameter => parameter.ParameterType)
				.ShouldBe(new[] { typeof(bool), typeof(LogLevels), typeof(DestinationLabelPolicy) });
			constructor.GetParameters().Any(parameter => parameter.IsOptional).ShouldBeFalse();
			type.GetProperties().Select(property => property.Name).OrderBy(name => name)
				.ShouldBe(new[] { "Enabled", "LabelPolicy", "ReportingLevel" });
			foreach (var property in type.GetProperties())
			{
				property.GetGetMethod().ShouldNotBeNull();
				property.GetSetMethod().ShouldBeNull();
			}
		}

		[Theory]
		[InlineData(false, 0)]
		[InlineData(true, 0)]
		[InlineData(false, 9)]
		[InlineData(true, 9)]
		[InlineData(false, 63)]
		[InlineData(true, 63)]
		public void ShouldReadBackBothEnabledValuesAndKnownBitMasks(bool enabled, int mask)
		{
			var policy = new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]);
			var settings = new DestinationRegistrationSettings(enabled, (LogLevels)mask, policy);
			settings.Enabled.ShouldBe(enabled);
			settings.ReportingLevel.ShouldBe((LogLevels)mask);
			settings.LabelPolicy.Mode.ShouldBe(LabelFilterMode.NoFilter);
			settings.LabelPolicy.Labels.ShouldBeEmpty();
		}

		[Theory]
		[InlineData(-1)]
		[InlineData(int.MinValue)]
		[InlineData(64)]
		[InlineData(65)]
		[InlineData(int.MaxValue)]
		public void ShouldRejectUnknownOrNegativeRegistrationBits(int mask)
		{
			var policy = new DestinationLabelPolicy(LabelFilterMode.Exclude,
				new[] { new SensitivityLabel("settings-private-canary") });
			var failure = Should.Throw<ArgumentOutOfRangeException>(() =>
				new DestinationRegistrationSettings(true, (LogLevels)mask, policy));
			failure.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
			failure.ParamName.ShouldBe("reportingLevel");
			failure.Message.ShouldNotContain("settings-private-canary");
		}

		[Fact]
		public void ShouldRejectNullRegistrationPolicyWithItsParameterName()
		{
			var failure = Should.Throw<ArgumentNullException>(() =>
				new DestinationRegistrationSettings(true, LogLevels.Trace, null));
			failure.GetType().ShouldBe(typeof(ArgumentNullException));
			failure.ParamName.ShouldBe("labelPolicy");
		}

		[Fact]
		public void ShouldRetainPolicyMembershipAndIndependentSettingsValues()
		{
			var source = new List<SensitivityLabel> { new SensitivityLabel("A"), new SensitivityLabel("a") };
			var policy = new DestinationLabelPolicy(LabelFilterMode.AllowOnly, source);
			var first = new DestinationRegistrationSettings(true, (LogLevels)9, policy);
			var second = new DestinationRegistrationSettings(false, LogLevels.Trace, policy);
			source.Clear();
			source.Add(new SensitivityLabel("replacement"));
			foreach (var settings in new[] { first, second })
			{
				settings.LabelPolicy.Mode.ShouldBe(LabelFilterMode.AllowOnly);
				settings.LabelPolicy.Labels.Select(label => label.Identifier).OrderBy(name => name, StringComparer.Ordinal)
					.ShouldBe(new[] { "A", "a" });
				Should.Throw<NotSupportedException>(() => ((IList<SensitivityLabel>)settings.LabelPolicy.Labels).Clear());
				settings.LabelPolicy.Allows(new[] { new SensitivityLabel("A") }).ShouldBeTrue();
				settings.LabelPolicy.Allows(new[] { new SensitivityLabel("replacement") }).ShouldBeFalse();
			}
			first.Enabled.ShouldBeTrue();
			first.ReportingLevel.ShouldBe((LogLevels)9);
			second.Enabled.ShouldBeFalse();
			second.ReportingLevel.ShouldBe(LogLevels.Trace);
		}
	}
}