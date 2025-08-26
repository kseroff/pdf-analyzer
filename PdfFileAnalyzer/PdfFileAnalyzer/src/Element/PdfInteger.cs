namespace PdfFileAnalyzer.Element
{
	public class PdfInteger : PdfBase
		{
		public int IntValue;

		public PdfInteger(int IntValue)
			{
			this.IntValue = IntValue;
			return;
			}

		public override string ToString()
			{
			return IntValue.ToString();
			}

		public override string TypeToString()
			{
			return "Integer";
			}

		/// <summary>
		/// PdfInteger constant = 0
		/// </summary>
		public static readonly PdfInteger Zero = new PdfInteger(0);

		/// <summary>
		/// PdfInteger constant = 1
		/// </summary>
		public static readonly PdfInteger One = new PdfInteger(1);

		/// <summary>
		/// PdfInteger constant = 2
		/// </summary>
		public static readonly PdfInteger Two = new PdfInteger(2);

		/// <summary>
		/// PdfInteger constant = 3
		/// </summary>
		public static readonly PdfInteger Three = new PdfInteger(3);

		/// <summary>
		/// PdfInteger constant = 4
		/// </summary>
		public static readonly PdfInteger Four = new PdfInteger(4);

		/// <summary>
		/// PdfInteger constant = 8
		/// </summary>
		public static readonly PdfInteger Eight = new PdfInteger(8);

		/// <summary>
		/// PdfInteger constant = 16
		/// </summary>
		public static readonly PdfInteger Sixteen = new PdfInteger(16);

		/// <summary>
		/// PdfInteger constant = 128
		/// </summary>
		public static readonly PdfInteger OneTwoEight = new PdfInteger(128);

		}
	}
