﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace System.Unicode
{
	public static class UnicodeInfo
	{
		public const string DefaultBlock = "No_Block";

		private static readonly Version unicodeVersion;
		private static readonly UnicodeCharacterData[] unicodeCharacterData;
		private static readonly UnihanCharacterData[] unihanCharacterData;
		private static readonly UnicodeBlock[] blocks;
		private static readonly int maxContiguousIndex;

		static UnicodeInfo()
		{
			using (var stream = new DeflateStream(typeof(UnicodeInfo).GetTypeInfo().Assembly.GetManifestResourceStream("ucd.dat"), CompressionMode.Decompress, false))
			{
				ReadFromStream(stream, out unicodeVersion, out unicodeCharacterData, out unihanCharacterData, out blocks, out maxContiguousIndex);
			}
		}

		internal static void ReadFromStream(Stream stream, out Version unicodeVersion, out UnicodeCharacterData[] unicodeCharacterData, out UnihanCharacterData[] unihanCharacterData, out UnicodeBlock[] blocks, out int maxContiguousIndex)
		{
			using (var reader = new BinaryReader(stream, Encoding.UTF8))
			{
				int i;

				if (reader.ReadByte() != 'U'
					| reader.ReadByte() != 'C'
					| reader.ReadByte() != 'D')
					throw new InvalidDataException();

				byte formatVersion = reader.ReadByte();

				if (formatVersion != 1) throw new InvalidDataException();

				var fileUnicodeVersion = new Version(reader.ReadUInt16(), reader.ReadByte());

				var unicodeCharacterDataEntries = new UnicodeCharacterData[ReadCodePoint(reader)];
				byte[] nameBuffer = new byte[128];
				int mci = 0;

				for (i = 0; i < unicodeCharacterDataEntries.Length; ++i)
				{
					if ((unicodeCharacterDataEntries[i] = ReadUnicodeCharacterDataEntry(reader, nameBuffer)).CodePointRange.Contains(i)) mci = i;
					else
					{
						++i;
						break;
					}
				}

				maxContiguousIndex = mci;

				for (; i < unicodeCharacterDataEntries.Length; ++i)
				{
					unicodeCharacterDataEntries[i] = ReadUnicodeCharacterDataEntry(reader, nameBuffer);
				}

				var blockEntries = new UnicodeBlock[reader.ReadByte()];

				for (i = 0; i < blockEntries.Length; ++i)
				{
					blockEntries[i] = ReadBlockEntry(reader);
				}

				var unihanCharacterDataEntries = new UnihanCharacterData[ReadCodePoint(reader)];

				for (i = 0; i < unihanCharacterDataEntries.Length; ++i)
				{
					unihanCharacterDataEntries[i] = ReadUnihanCharacterDataEntry(reader);
				}

				unicodeVersion = fileUnicodeVersion;
				unicodeCharacterData = unicodeCharacterDataEntries;
				unihanCharacterData = unihanCharacterDataEntries;
				blocks = blockEntries;
			}
		}

		private static UnicodeCharacterData ReadUnicodeCharacterDataEntry(BinaryReader reader, byte[] nameBuffer)
		{
			var fields = (UcdFields)reader.ReadUInt16();

			var codePointRange = (fields & UcdFields.CodePointRange) != 0 ? new UnicodeCharacterRange(ReadCodePoint(reader), ReadCodePoint(reader)) : new UnicodeCharacterRange(ReadCodePoint(reader));

			string name = null;
			UnicodeNameAlias[] nameAliases = UnicodeNameAlias.EmptyArray;

			// Read all the official names of the character.
			if ((fields & UcdFields.Name) != 0)
			{
				int length = reader.ReadByte();
				byte @case = (byte)(length & 0xC0);

				if (@case < 0x80)   // Handles the case where only the name is present.
				{
					length = (length & 0x7F) + 1;
					if (reader.Read(nameBuffer, 0, length) != length) throw new EndOfStreamException();

					name = Encoding.UTF8.GetString(nameBuffer, 0, length);
				}
				else
				{
					nameAliases = new UnicodeNameAlias[(length & 0x3F) + 1];

					if ((@case & 0x40) != 0)
					{
						length = reader.ReadByte() + 1;
						if (length > 128) throw new InvalidDataException("Did not expect names longer than 128 bytes.");
						if (reader.Read(nameBuffer, 0, length) != length) throw new EndOfStreamException();
						name = Encoding.UTF8.GetString(nameBuffer, 0, length);
					}

					for (int i = 0; i < nameAliases.Length; ++i)
					{
						nameAliases[i] = new UnicodeNameAlias(reader.ReadString(), (UnicodeNameAliasKind)(reader.ReadByte()));
					}
				}
			}

			var category = (fields & UcdFields.Category) != 0 ? (UnicodeCategory)reader.ReadByte() : UnicodeCategory.OtherNotAssigned;
			var canonicalCombiningClass = (fields & UcdFields.CanonicalCombiningClass) != 0 ? (CanonicalCombiningClass)reader.ReadByte() : CanonicalCombiningClass.NotReordered;
			var bidirectionalClass = (fields & UcdFields.BidirectionalClass) != 0 ? (BidirectionalClass)reader.ReadByte() : 0;
			CompatibilityFormattingTag decompositionType = (fields & UcdFields.DecompositionMapping) != 0 ? (CompatibilityFormattingTag)reader.ReadByte() : CompatibilityFormattingTag.Canonical;
			string decompositionMapping = (fields & UcdFields.DecompositionMapping) != 0 ? reader.ReadString() : null;
			var numericType = (UnicodeNumericType)((int)(fields & UcdFields.NumericNumeric) >> 6);
			UnicodeRationalNumber numericValue = numericType != UnicodeNumericType.None ?
				new UnicodeRationalNumber(reader.ReadInt64(), reader.ReadByte()) :
				default(UnicodeRationalNumber);
			string oldName = (fields & UcdFields.OldName) != 0 ? reader.ReadString() : null;
			string simpleUpperCaseMapping = (fields & UcdFields.SimpleUpperCaseMapping) != 0 ? reader.ReadString() : null;
			string simpleLowerCaseMapping = (fields & UcdFields.SimpleLowerCaseMapping) != 0 ? reader.ReadString() : null;
			string simpleTitleCaseMapping = (fields & UcdFields.SimpleTitleCaseMapping) != 0 ? reader.ReadString() : null;
			ContributoryProperties contributoryProperties = (fields & UcdFields.ContributoryProperties) != 0 ? (ContributoryProperties)reader.ReadInt32() : 0;
			CoreProperties coreProperties = (fields & UcdFields.CoreProperties) != 0 ? (CoreProperties)ReadInt24(reader) : 0;
			int[] crossReferences = (fields & UcdFields.CrossRerefences) != 0 ? new int[reader.ReadByte() + 1] : null;

			if (crossReferences != null)
			{
				for (int i = 0; i < crossReferences.Length; ++i)
					crossReferences[i] = ReadCodePoint(reader);
			}

			return new UnicodeCharacterData
			(
				codePointRange,
				name,
				nameAliases,
				category,
				canonicalCombiningClass,
				bidirectionalClass,
				decompositionType,
				decompositionMapping,
				numericType,
				numericValue,
				(fields & UcdFields.BidirectionalMirrored) != 0,
				oldName,
				simpleUpperCaseMapping,
				simpleLowerCaseMapping,
				simpleTitleCaseMapping,
				contributoryProperties,
				coreProperties,
				crossReferences
			);
		}

		private static UnihanCharacterData ReadUnihanCharacterDataEntry(BinaryReader reader)
		{
			var fields = (UnihanFields)reader.ReadUInt16();

			int codePoint = UnihanCharacterData.UnpackCodePoint(ReadCodePoint(reader));

			var numericType = (UnihanNumericType)((int)(fields & UnihanFields.OtherNumeric));
			long numericValue = numericType != UnihanNumericType.None ?
				reader.ReadInt64() :
				0;

			string definition = (fields & UnihanFields.Definition) != 0 ? reader.ReadString() : null;
			string mandarinReading = (fields & UnihanFields.MandarinReading) != 0 ? reader.ReadString() : null;
			string cantoneseReading = (fields & UnihanFields.CantoneseReading) != 0 ? reader.ReadString() : null;
			string japaneseKunReading = (fields & UnihanFields.JapaneseKunReading) != 0 ? reader.ReadString() : null;
			string japaneseOnReading = (fields & UnihanFields.JapaneseOnReading) != 0 ? reader.ReadString() : null;
			string koreanReading = (fields & UnihanFields.KoreanReading) != 0 ? reader.ReadString() : null;
			string hangulReading = (fields & UnihanFields.HangulReading) != 0 ? reader.ReadString() : null;
			string vietnameseReading = (fields & UnihanFields.VietnameseReading) != 0 ? reader.ReadString() : null;
			string simplifiedVariant = (fields & UnihanFields.SimplifiedVariant) != 0 ? reader.ReadString() : null;
			string traditionalVariant = (fields & UnihanFields.TraditionalVariant) != 0 ? reader.ReadString() : null;

			return new UnihanCharacterData
			(
				codePoint,
				numericType,
				numericValue,
				definition,
				mandarinReading,
				cantoneseReading,
				japaneseKunReading,
				japaneseOnReading,
				koreanReading,
				hangulReading,
				vietnameseReading,
				simplifiedVariant,
				traditionalVariant
			);
		}

		private static UnicodeBlock ReadBlockEntry(BinaryReader reader)
		{
			return new UnicodeBlock(new UnicodeCharacterRange(ReadCodePoint(reader), ReadCodePoint(reader)), reader.ReadString());
		}

		private static int ReadInt24(BinaryReader reader)
		{
			return reader.ReadByte() | ((reader.ReadByte() | (reader.ReadByte() << 8)) << 8);
		}

#if DEBUG
		internal static int ReadCodePoint(BinaryReader reader)
#else
		private static int ReadCodePoint(BinaryReader reader)
#endif
		{
			byte b = reader.ReadByte();

			if (b < 0xA0) return b;
			else if (b < 0xC0)
			{
				return 0xA0 + (((b & 0x1F) << 8) | reader.ReadByte());
			}
			else if (b < 0xE0)
			{
				return 0x20A0 + (((b & 0x1F) << 8) | reader.ReadByte());
			}
			else
			{
				return 0x40A0 + (((((b & 0x1F) << 8) | reader.ReadByte()) << 8) | reader.ReadByte());
			}
		}

		public static Version UnicodeVersion { get { return unicodeVersion; } }

		private static UnicodeCharacterData FindUnicodeCodePoint(int codePoint)
		{
			// For the first code points (this includes all of ASCII, and quite a bit more), the index in the table will be the code point itself.
			if (codePoint <= maxContiguousIndex)
			{
				return unicodeCharacterData[codePoint];
			}
			else
			{
				// For other code points, we will use a classic binary search with adjusted search indexes.
				return BinarySearchUnicodeCodePoint(codePoint);
			}
		}

		private static UnicodeCharacterData BinarySearchUnicodeCodePoint(int codePoint)
		{
			// NB: Due to the strictly ordered nature of the table, we know that a code point can never happen after the index which is the code point itself.
			// This will greatly reduce the range to scan for characters close to maxContiguousIndex, and will have a lesser impact on other characters.
			int minIndex = maxContiguousIndex + 1;
			int maxIndex = codePoint < unicodeCharacterData.Length ? codePoint - 1 : unicodeCharacterData.Length - 1;

			do
			{
				int index = (minIndex + maxIndex) >> 1;

				int Δ = unicodeCharacterData[index].CodePointRange.CompareCodePoint(codePoint);

				if (Δ == 0) return unicodeCharacterData[index];
				else if (Δ < 0) maxIndex = index - 1;
				else minIndex = index + 1;
			} while (minIndex <= maxIndex);

			return null;
		}

		private static UnihanCharacterData FindUnihanCodePoint(int codePoint)
		{
			int minIndex;
			int maxIndex;

			if (unihanCharacterData.Length == 0 || codePoint < unihanCharacterData[minIndex = 0].CodePoint || codePoint > unihanCharacterData[maxIndex = unicodeCharacterData.Length - 1].CodePoint)
			{
				return null;
			}

			do
			{
				int index = (minIndex + maxIndex) >> 1;

				int Δ = codePoint - unihanCharacterData[index].CodePoint;

				if (Δ == 0) return unihanCharacterData[index];
				else if (Δ < 0) maxIndex = index - 1;
				else minIndex = index + 1;
			} while (minIndex <= maxIndex);

			return null;
		}

		private static int FindBlockIndex(int codePoint)
		{
			int minIndex = 0;
			int maxIndex = blocks.Length - 1;

			do
			{
				int index = (minIndex + maxIndex) >> 1;

				int Δ = blocks[index].CodePointRange.CompareCodePoint(codePoint);

				if (Δ == 0) return index;
				else if (Δ < 0) maxIndex = index - 1;
				else minIndex = index + 1;
			} while (minIndex <= maxIndex);

			return -1;
		}

		public static string GetBlockName(int codePoint)
		{
			int i = FindBlockIndex(codePoint);

			return i >= 0 ? blocks[i].Name : DefaultBlock;
		}

		public static UnicodeCharInfo GetCharInfo(int codePoint)
		{
			return new UnicodeCharInfo(codePoint, FindUnicodeCodePoint(codePoint), FindUnihanCodePoint(codePoint), GetBlockName(codePoint));
		}

		public static UnicodeCategory GetCategory(int codePoint)
		{
			var charData = FindUnicodeCodePoint(codePoint);

			return charData != null ? charData.Category : UnicodeCategory.OtherNotAssigned;
		}

		public static string GetDisplayText(UnicodeCharInfo charInfo)
		{
			if (charInfo.CodePoint <= 0x0020) return ((char)(0x2400 + charInfo.CodePoint)).ToString();
			else if (charInfo.Category == UnicodeCategory.NonSpacingMark) return "\u25CC" + char.ConvertFromUtf32(charInfo.CodePoint);
			else return char.ConvertFromUtf32(charInfo.CodePoint);
		}

		public static string GetDisplayText(int codePoint)
		{
			if (codePoint <= 0x0020) return ((char)(0x2400 + codePoint)).ToString();
			else if (GetCategory(codePoint) == UnicodeCategory.NonSpacingMark) return "\u25CC" + char.ConvertFromUtf32(codePoint);
			else return char.ConvertFromUtf32(codePoint);
		}

		public static string GetName(int codePoint)
		{
			if (HangulInfo.IsHangul(codePoint)) return HangulInfo.GetHangulName((char)codePoint);
			else return GetName(codePoint, FindUnicodeCodePoint(codePoint));
		}

		internal static string GetName(int codePoint, UnicodeCharacterData characterData)
		{
			if (characterData.CodePointRange.IsSingleCodePoint) return characterData.Name;
			else if (HangulInfo.IsHangul(codePoint)) return HangulInfo.GetHangulName((char)codePoint);
			else if (characterData.Name != null) return characterData.Name + "-" + codePoint.ToString("X4");
			else return null;
		}

		public static UnicodeBlock[] GetBlocks()
		{
			return (UnicodeBlock[])blocks.Clone();
		}
	}
}
