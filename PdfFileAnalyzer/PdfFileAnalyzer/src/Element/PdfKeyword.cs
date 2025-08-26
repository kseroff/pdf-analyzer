namespace PdfFileAnalyzer.Element
{
	public class PdfKeyword : PdfBase
		{
		public KeyWord KeywordValue;

		public PdfKeyword(KeyWord KeywordValue)
			{
			this.KeywordValue = KeywordValue;
			return;
			}

		public override string ToString()
			{
			return KeywordValue.ToString();
			}

		public override string TypeToString()
			{
			return "Keyword";
			}
		}
	}
