namespace PdfFileAnalyzer.Element
{
	public class PdfString : PdfBase
		{
		public byte[] StrValue;

		public PdfString(byte[] StrValue)
			{
			this.StrValue = StrValue;
			return;
			}

		public PdfString(string CSharpStr)
			{
			// scan input text for Unicode characters
			bool Unicode = false;
			foreach(char TestChar in CSharpStr) if(TestChar > 255)
				{
				Unicode = true;
				break;
				}

			// all characters are one byte long
			if(!Unicode)
				{
				// save each imput character in one byte
				StrValue = new byte[CSharpStr.Length];
				int Index1 = 0;
				foreach(char TestChar in CSharpStr) StrValue[Index1++] = (byte) TestChar;
				return;
				}

			// Unicode case. we have some two bytes characters
			// allocate output byte array
			StrValue = new byte[2 * CSharpStr.Length + 2];

			// add Unicode marker at the start of the string
			StrValue[0] = 0xfe;
			StrValue[1] = 0xff;

			// save each character as two bytes
			int Index2 = 2;
			foreach(char TestChar in CSharpStr)
				{
				StrValue[Index2++] = (byte) (TestChar >> 8);
				StrValue[Index2++] = (byte) TestChar;
				}
			return;
			}

		public string ToUnicode()
			{
			if(StrValue == null) return string.Empty;

			// unicode
			if(StrValue.Length >= 2 && StrValue[0] == 0xfe && StrValue[1] == 0xff)
				{
				char[] UniArray = new char[StrValue.Length / 2];
				for(int Index = 0; Index < UniArray.Length; Index++) UniArray[Index] = (char) (StrValue[2 * Index] << 8 | StrValue[2 * Index + 1]);
				return new string(UniArray);
				}

			// ascii
			char[] ChrArray = new char[StrValue.Length];
			for(int Index = 0; Index < StrValue.Length; Index++) ChrArray[Index] = (char) StrValue[Index];
			return new string(ChrArray);
			}

		public override string TypeToString()
			{
			return "PDFString";
			}
		}
	}
