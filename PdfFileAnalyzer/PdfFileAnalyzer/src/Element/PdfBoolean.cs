namespace PdfFileAnalyzer.Element
{

	public class PdfBoolean : PdfBase
		{

		public bool BooleanValue;

		public PdfBoolean(bool BooleanValue)
			{
			this.BooleanValue = BooleanValue;
			return;
			}

		public override string ToString()
			{
			return BooleanValue ? "true" : "false";
			}

		public override string TypeToString()
			{
			return "bool";
			}

		public static readonly PdfBoolean False = new PdfBoolean(false);

		public static readonly PdfBoolean True = new PdfBoolean(true);
		}
	}
