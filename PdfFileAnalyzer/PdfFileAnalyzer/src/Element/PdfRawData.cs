namespace PdfFileAnalyzer.Element
{

	public class PdfRawData : PdfBase
		{
		public string RawDataValue;

		public PdfRawData(string RawDataValue)
			{
			this.RawDataValue = RawDataValue;
			return;
			}

		public override string ToString()
			{
			return RawDataValue;
			}

		public override string TypeToString()
			{
			return "RawData";
			}
		}
	}
