namespace PdfFileAnalyzer.Element
{

	public class PdfName : PdfBase
		{
		public string NameValue;

		/// <summary>
		/// Constructor
		/// First character must be /
		/// </summary>
		public PdfName(string NameValue) 
			{
			this.NameValue = NameValue;
			return;
			}

		public override string ToString()
			{
			return NameValue;
			}

		public override string TypeToString()
			{
			return "Name";
			}
		}
	}
