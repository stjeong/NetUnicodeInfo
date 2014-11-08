﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace System.Unicode.Builder
{
	internal class UnicodeDataProcessor
	{
		public const string UnicodeDataFileName = "UnicodeData.txt";
		public const string PropListFileName = "PropList.txt";
		public const string BlocksFileName = "Blocks.txt";

		private static string ParseSimpleCaseMapping(string mapping)
		{
			if (string.IsNullOrEmpty(mapping)) return null;

			return char.ConvertFromUtf32(int.Parse(mapping, NumberStyles.HexNumber));
		}

		private static string NullIfEmpty(string s)
		{
			return string.IsNullOrEmpty(s) ? null : s;
		}

		public static async Task<UnicodeInfoBuilder> BuildDataAsync(IUcdSource ucdSource)
		{
			var builder = new UnicodeInfoBuilder(new Version(7, 0));

			await ProcessUnicodeDataFile(ucdSource, builder).ConfigureAwait(false);
			await ProcessPropListFile(ucdSource, builder).ConfigureAwait(false);
			await ProcessBlocksFile(ucdSource, builder).ConfigureAwait(false);

			return builder;
		}

		private static async Task ProcessUnicodeDataFile(IUcdSource ucdSource, UnicodeInfoBuilder builder)
		{
			using (var reader = new UnicodeDataFileReader(await ucdSource.OpenDataFileAsync(UnicodeDataFileName).ConfigureAwait(false)))
			{
				int rangeStartCodePoint = -1;

				while (reader.MoveToNextLine())
				{
					var codePoint = new UnicodeCharacterRange(int.Parse(reader.ReadField(), NumberStyles.HexNumber));

					string name = reader.ReadField();

					if (!string.IsNullOrEmpty(name) && name[0] == '<' && name[name.Length - 1] == '>')
					{
						if (name.EndsWith(", First>", StringComparison.OrdinalIgnoreCase))
						{
							if (rangeStartCodePoint >= 0) throw new InvalidDataException("Invalid range data in UnicodeData.txt.");

							rangeStartCodePoint = codePoint.FirstCodePoint;

							continue;
						}
						else if (name.EndsWith(", Last>", StringComparison.OrdinalIgnoreCase))
						{
							if (rangeStartCodePoint < 0) throw new InvalidDataException("Invalid range data in UnicodeData.txt.");

							codePoint = new UnicodeCharacterRange(rangeStartCodePoint, codePoint.LastCodePoint);

							name = name.Substring(1, name.Length - 8).ToUpperInvariant();   // Upper-case the name in order to respect unicode naming scheme. (Spec says all names are uppercase ASCII)

							rangeStartCodePoint = -1;
						}
						else if (name == "<control>")	// Ignore the name of the property for these code points, as it should really be empty by the spec.
						{
							// For control characters, we can derive a character label in of the form <control-NNNN>, which is not the character name.
							name = null;
						}
						else
						{
							throw new InvalidDataException("Unexpected code point name tag: " + name + ".");
						}
					}
					else if (rangeStartCodePoint >= 0)
					{
						throw new InvalidDataException("Invalid range data in UnicodeData.txt.");
					}

					// NB: Fields 10 and 11 are deemed obsolete. Field 11 should always be empty, and will be ignored here.
					var characterData = new UnicodeCharacterDataBuilder(codePoint)
					{
						Name = NullIfEmpty(name),
						Category = UnicodeCategoryInfo.FromShortName(reader.ReadField()).Category,
						CanonicalCombiningClass = (CanonicalCombiningClass)byte.Parse(reader.ReadField()),
					};

					BidirectionalClass bidirectionalClass;
					if (EnumHelper<BidirectionalClass>.TryGetNamedValue(reader.ReadField(), out bidirectionalClass))
					{
						characterData.BidirectionalClass = bidirectionalClass;
					}
					else
					{
						throw new InvalidDataException(string.Format("Missing Bidi_Class property for code point(s) {0}.", codePoint));
					}

					characterData.CharacterDecompositionMapping = CharacterDecompositionMapping.Parse(NullIfEmpty(reader.ReadField()));

					string numericDecimalField = NullIfEmpty(reader.ReadField());
					string numericDigitField = NullIfEmpty(reader.ReadField());
					string numericNumericField = NullIfEmpty(reader.ReadField());

					characterData.BidirectionalMirrored = reader.ReadField() == "Y";
					characterData.OldName = NullIfEmpty(reader.ReadField());
					reader.SkipField();
					characterData.SimpleUpperCaseMapping = ParseSimpleCaseMapping(reader.ReadField());
					characterData.SimpleLowerCaseMapping = ParseSimpleCaseMapping(reader.ReadField());
					characterData.SimpleTitleCaseMapping = ParseSimpleCaseMapping(reader.ReadField());

					// Handle Numeric_Type & Numeric_Value:
					// If field 6 is set, fields 7 and 8 should have the same value, and Numeric_Type is Decimal.
					// If field 6 is not set but field 7 is set, field 8 should be set and have the same value. Then, the type is Digit.
					// If field 6 and 7 are not set, but field 8 is set, then Numeric_Type is Numeric.
					if (numericNumericField != null)
					{
						characterData.NumericValue = UnicodeRationalNumber.Parse(numericNumericField);

						if (numericDigitField != null)
						{
							if (numericDigitField != numericNumericField)
							{
								throw new InvalidDataException("Invalid value for field 7 of code point " + characterData.CodePointRange.ToString() + ".");
							}

							if (numericDecimalField != null)
							{
								if (numericDecimalField != numericDigitField)
								{
									throw new InvalidDataException("Invalid value for field 6 of code point " + characterData.CodePointRange.ToString() + ".");
								}
								characterData.NumericType = UnicodeNumericType.Decimal;
							}
							else
							{
								characterData.NumericType = UnicodeNumericType.Digit;
							}
						}
						else
						{
							characterData.NumericType = UnicodeNumericType.Numeric;
						}
					}

					builder.Insert(characterData);
				}
			}
		}

		private static async Task ProcessPropListFile(IUcdSource ucdSource, UnicodeInfoBuilder builder)
		{
			using (var reader = new UnicodeDataFileReader(await ucdSource.OpenDataFileAsync(PropListFileName).ConfigureAwait(false)))
			{
				while (reader.MoveToNextLine())
				{
					ContributoryProperties property;

					var range = UnicodeCharacterRange.Parse(reader.ReadField().TrimEnd());
					if (EnumHelper<ContributoryProperties>.TryGetNamedValue(reader.ReadField().Trim(), out property))
					{
						builder.SetProperties(property, range);
					}
				}
			}
		}

		private static async Task ProcessBlocksFile(IUcdSource ucdSource, UnicodeInfoBuilder builder)
		{
			using (var reader = new UnicodeDataFileReader(await ucdSource.OpenDataFileAsync(BlocksFileName).ConfigureAwait(false)))
			{
				while (reader.MoveToNextLine())
				{
					builder.AddBlockEntry(new UnicodeBlock(UnicodeCharacterRange.Parse(reader.ReadField()), reader.ReadField().Trim()));
				}
			}
		}
	}
}
