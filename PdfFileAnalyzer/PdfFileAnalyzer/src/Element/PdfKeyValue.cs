using System;

namespace PdfFileAnalyzer.Element
{
	public class PdfKeyValue : PdfBase, IComparable<PdfKeyValue>
		{

		public string Key;
		public PdfBase Value;

		public PdfKeyValue(string Key, PdfBase Value)
			{
			if(Key[0] != '/') 
				throw new ApplicationException("Key must start with /");

			this.Key = Key;
			this.Value = Value;
			return;
			}

		public PdfKeyValue(string Key)
			{
			if(Key[0] != '/') 
				throw new ApplicationException("Key must start with /");

			this.Key = Key;
			return;
			}

		/// <summary>
		/// Compare two key value pairs
		/// </summary>
		public int CompareTo( PdfKeyValue Other)
			{
			return string.Compare(this.Key, Other.Key);
			}

		}
	}
