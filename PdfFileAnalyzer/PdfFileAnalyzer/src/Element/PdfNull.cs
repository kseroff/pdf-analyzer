namespace PdfFileAnalyzer.Element
{
	public class PdfNull : PdfBase
		{
		public PdfNull() {}

		public override string ToString()
			{
			return "null";
			}

		public override string TypeToString()
			{
			return "Null";
			}
		}
	}
