using System;

namespace PdfFileAnalyzer.Element
{

	public class PdfReal : PdfBase
		{

		public double RealValue;

		public PdfReal(double RealValue)
			{
			this.RealValue = RealValue;
			return;
			}

		/// <summary>
		/// Convert real number to string
		/// </summary>
		public override string ToString()
			{
			if(Math.Abs(RealValue) < 0.0001) return "0";
			return ((float) RealValue).ToString("G", NumFormatInfo.PeriodDecSep);
			}

		public override string TypeToString()
			{
			return "Real";
			}
		}
	}
