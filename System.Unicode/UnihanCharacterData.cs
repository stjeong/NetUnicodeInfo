namespace System.Unicode
{
	internal readonly struct UnihanCharacterData
	{
		public readonly int CodePoint;
		public readonly UnihanNumericType NumericType;
		public readonly long NumericValue;
		public readonly UnicodeRadicalStrokeCountCollection UnicodeRadicalStrokeCounts;
		public readonly UnicodeDataString Definition;
		public readonly UnicodeDataString MandarinReading;
		public readonly UnicodeDataString CantoneseReading;
		public readonly UnicodeDataString JapaneseKunReading;
		public readonly UnicodeDataString JapaneseOnReading;
		public readonly UnicodeDataString KoreanReading;
		public readonly UnicodeDataString HangulReading;
		public readonly UnicodeDataString VietnameseReading;
		public readonly UnicodeDataString SimplifiedVariant;
		public readonly UnicodeDataString TraditionalVariant;

		internal UnihanCharacterData
		(
			int codePoint,
			UnihanNumericType numericType,
			long numericValue,
			UnicodeRadicalStrokeCountCollection unicodeRadicalStrokeCounts,
			UnicodeDataString definition,
			UnicodeDataString mandarinReading,
			UnicodeDataString cantoneseReading,
			UnicodeDataString japaneseKunReading,
			UnicodeDataString japaneseOnReading,
			UnicodeDataString koreanReading,
			UnicodeDataString hangulReading,
			UnicodeDataString vietnameseReading,
			UnicodeDataString simplifiedVariant,
			UnicodeDataString traditionalVariant
		)
		{
			CodePoint = codePoint;
			NumericType = numericType;
			NumericValue = numericValue;
			UnicodeRadicalStrokeCounts = unicodeRadicalStrokeCounts;
			Definition = definition;
			MandarinReading = mandarinReading;
			CantoneseReading = cantoneseReading;
			JapaneseKunReading = japaneseKunReading;
			JapaneseOnReading = japaneseOnReading;
			KoreanReading = koreanReading;
			HangulReading = hangulReading;
			VietnameseReading = vietnameseReading;
			SimplifiedVariant = simplifiedVariant;
			TraditionalVariant = traditionalVariant;
		}

#if BUILD_SYSTEM
		internal static int PackCodePoint(int codePoint)
		{
			if (codePoint >= 0x3400)
			{
				if (codePoint < 0x4E00) return codePoint + 0x1E00;
				else if (codePoint < 0xA000) return codePoint - 0x4E00;
				else if (codePoint >= 0xF900 && codePoint < 0xFB00) return codePoint + 0xFD00;
				else if (codePoint >= 0x20000)
				{
					if (codePoint < 0x2F800) return codePoint - 0x19400;
					else if (codePoint < 0x30000) return codePoint - 0x10000;
				}
			}

			throw new ArgumentOutOfRangeException(nameof(codePoint));
		}
#endif

		internal static int UnpackCodePoint(int packedCodePoint)
		{
			if (packedCodePoint >= 0)
			{
				if (packedCodePoint < 0x05200) return packedCodePoint + 0x4E00;
				else if (packedCodePoint < 0x06C00) return packedCodePoint - 0x1E00;
				else if (packedCodePoint < 0x1F600) return packedCodePoint + 0x19400;
				else if (packedCodePoint < 0x1F800) return packedCodePoint - 0xFD00;
				else if (packedCodePoint < 0x20000) return packedCodePoint + 0x10000;
			}
			throw new ArgumentOutOfRangeException(nameof(packedCodePoint));
		}
	}
}
