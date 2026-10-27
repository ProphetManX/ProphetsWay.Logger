using System;
using System.Globalization;
using System.Text;

namespace ProphetsWay.Utilities
{
	internal static class LogTextRenderer
	{
		internal static string FormatValue(object value)
		{
			switch (value)
			{
				case null:
					return null;
				case string text:
					return text;
				case char character:
					return character.ToString();
				case bool boolean:
					return boolean ? bool.TrueString : bool.FalseString;
				case sbyte signedByte:
					return signedByte.ToString("D", CultureInfo.InvariantCulture);
				case byte unsignedByte:
					return unsignedByte.ToString("D", CultureInfo.InvariantCulture);
				case short signedShort:
					return signedShort.ToString("D", CultureInfo.InvariantCulture);
				case ushort unsignedShort:
					return unsignedShort.ToString("D", CultureInfo.InvariantCulture);
				case int signedInteger:
					return signedInteger.ToString("D", CultureInfo.InvariantCulture);
				case uint unsignedInteger:
					return unsignedInteger.ToString("D", CultureInfo.InvariantCulture);
				case long signedLong:
					return signedLong.ToString("D", CultureInfo.InvariantCulture);
				case ulong unsignedLong:
					return unsignedLong.ToString("D", CultureInfo.InvariantCulture);
				case IntPtr signedPointer:
					return signedPointer.ToInt64().ToString("D", CultureInfo.InvariantCulture);
				case UIntPtr unsignedPointer:
					return unsignedPointer.ToUInt64().ToString("D", CultureInfo.InvariantCulture);
				case float single:
					return single.ToString("R", CultureInfo.InvariantCulture);
				case double number:
					return number.ToString("R", CultureInfo.InvariantCulture);
				case decimal decimalNumber:
					return decimalNumber.ToString("G", CultureInfo.InvariantCulture);
				case Guid identifier:
					return identifier.ToString("D", CultureInfo.InvariantCulture);
				case DateTime dateTime:
					return dateTime.ToString("O", CultureInfo.InvariantCulture);
				case DateTimeOffset dateTimeOffset:
					return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
				case TimeSpan duration:
					return duration.ToString("c", CultureInfo.InvariantCulture);
				case Enum enumeration:
					return FormatEnum(enumeration);
				default:
					return "[no formatter: " + value.GetType().Name + "]";
			}
		}

		private static string FormatEnum(Enum value)
		{
			var type = value.GetType();
			if (Enum.GetName(type, value) != null)
				return value.ToString("G");

			var underlyingType = Enum.GetUnderlyingType(type);
			if (type.IsDefined(typeof(FlagsAttribute), false))
			{
				var remaining = GetEnumBits(value, underlyingType);
				var constants = Enum.GetValues(type);
				for (var index = constants.Length - 1; index >= 0 && remaining != 0; index--)
				{
					var bits = GetEnumBits(constants.GetValue(index), underlyingType);
					if (bits != 0 && (remaining & bits) == bits)
						remaining &= ~bits;
				}

				if (remaining == 0)
					return value.ToString("G");
			}

			return FormatValue(Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture));
		}

		private static ulong GetEnumBits(object value, Type underlyingType)
		{
			return underlyingType == typeof(ulong)
				? Convert.ToUInt64(value, CultureInfo.InvariantCulture)
				: unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture));
		}

		internal static string Render(LogContext context, LogLevels level, string message, bool hasMetadata, object metadata, Func<object, string> formatValue)
		{
			var record = new StringBuilder();
			record.Append(context.EventTimestampUtc.ToString("O", CultureInfo.InvariantCulture));
			record.Append(" :: ");
			record.Append(level.ToString("G").PadLeft(12));
			record.Append(":  ");
			AppendToken(record, message);
			if (hasMetadata)
			{
				record.Append(" | metadata=");
				AppendToken(record, formatValue(metadata));
			}

			record.Append(" | entryLabels=");
			AppendAttachment(record, context.Labels.EntryAnnotations);
			record.Append(" | scopes=[");
			for (var frameIndex = 0; frameIndex < context.Scopes.Count; frameIndex++)
			{
				if (frameIndex != 0)
					record.Append(',');

				var frame = context.Scopes[frameIndex];
				record.Append("{labels=");
				AppendAttachment(record, frame.Annotations);
				record.Append(",properties=[");
				for (var propertyIndex = 0; propertyIndex < frame.Properties.Count; propertyIndex++)
				{
					if (propertyIndex != 0)
						record.Append(',');

					var property = frame.Properties[propertyIndex];
					record.Append('(');
					AppendToken(record, property.Key);
					record.Append(',');
					AppendToken(record, formatValue(property.Value));
					record.Append(')');
				}
				record.Append("]}");
			}
			record.Append(']');
			return record.ToString();
		}

		private static void AppendAttachment(StringBuilder record, LogAnnotations annotations)
		{
			if (annotations == null)
			{
				record.Append("null");
				return;
			}

			record.Append('[');
			for (var index = 0; index < annotations.LabelOccurrences.Count; index++)
			{
				if (index != 0)
					record.Append(',');

				AppendToken(record, annotations.LabelOccurrences[index].Identifier);
			}
			record.Append(']');
		}

		private static void AppendToken(StringBuilder record, string text)
		{
			if (text == null)
			{
				record.Append("null");
				return;
			}

			record.Append('"');
			foreach (var character in text)
			{
				switch (character)
				{
					case '\\':
						record.Append("\\\\");
						break;
					case '"':
						record.Append("\\\"");
						break;
					case '\r':
						record.Append("\\r");
						break;
					case '\n':
						record.Append("\\n");
						break;
					case '\t':
						record.Append("\\t");
						break;
					default:
						if (char.IsControl(character) || character == '\u2028' || character == '\u2029')
						{
							record.Append("\\u");
							record.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
						}
						else
						{
							record.Append(character);
						}
						break;
				}
			}
			record.Append('"');
		}
	}
}