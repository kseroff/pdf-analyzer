using System;
using System.Collections.Generic;

namespace PdfFileAnalyzer.Element
{
	public class PdfDictionary : PdfBase
	{

	public List<PdfKeyValue> KeyValueArray;

	public PdfDictionary()
	{
		KeyValueArray = new List<PdfKeyValue>();
		return;
	}

	public void AddArray(string Key, params PdfBase[] Items )
		{
			// key (first character must be forward slash /)
			AddKeyValue(Key, new PdfArray(Items));
			return;
		}

	public void AddBoolean(string Key, bool Bool)
		{
			// key (first character must be forward slash /)
			AddKeyValue(Key, Bool ? PdfBoolean.True : PdfBoolean.False);
			return;
		}

	public void AddDictionary(string Key, PdfDictionary	Value)
		{
			// key (first character must be forward slash /)
			AddKeyValue(Key, Value);
			return;
		}

	public void AddInteger (string Key, int Integer)
		{
			// key (first character must be forward slash /)
			AddKeyValue(Key, new PdfInteger(Integer));
			return;
		}

	/// <summary>
	/// Add PdfName to dictionark
	/// </summary>
	public void AddName(string	Key, string	NameStr)
		{
			// key (first character must be forward slash /)
			// name (first character must be forward slash /)
			if (NameStr[0] != '/') 
				throw new ApplicationException("DEBUG Name must start with /");
			AddKeyValue(Key, new PdfName(NameStr));
			return;
		}

	public void AddPdfString(string Key, string Str)
		{
			// key (first character must be forward slash /)
			AddKeyValue(Key, new PdfString(Str));
			return;
		}

	public void AddPdfString(string Key, byte[] Str)
		{
			// key (first character must be forward slash /)
			AddKeyValue(Key, new PdfString(Str));
			return;
		}

	public void AddReal(string Key, double Real)
		{
		AddKeyValue(Key, new PdfReal(Real));
		return;
		}

	/// <summary>
	/// Add any object derived from PdfBase to dictionary
	/// </summary>
	public void AddKeyValue(string Key, PdfBase Value)
		{
		// create pair
		PdfKeyValue KeyValue = new PdfKeyValue(Key, Value);

		// keep dictionary sorted
		int Index = KeyValueArray.BinarySearch(KeyValue);

		// replace existing duplicate entry
		if(Index >= 0) 
				KeyValueArray[Index] = KeyValue;

		// add to result dictionary
		else KeyValueArray.Insert(~Index, KeyValue);

		// exit
		return;
		}

	/// <summary>
	/// Search dictionary for key and return the associated value
	/// </summary>
	public PdfBase FindValue(string	Key)
		{
		int Index = KeyValueArray.BinarySearch(new PdfKeyValue(Key));
		return Index < 0 ? PdfBase.Empty : KeyValueArray[Index].Value;
		}

	/// <summary>
	/// Search dictionary for key and return the associated value
	/// </summary>
	public bool Exists(string	Key)
		{
		int Index = KeyValueArray.BinarySearch(new PdfKeyValue(Key));
		return Index >= 0;
		}

	/// <summary>
	/// Gets number of items in the dictionary
	/// </summary>
	public int Count
		{
		get { return KeyValueArray.Count; }
		}

		// key (first character must be forward slash /)
		public void Remove(string Key)
		{
		int Index = KeyValueArray.BinarySearch(new PdfKeyValue(Key));
		if(Index >= 0) KeyValueArray.RemoveAt(Index);
		return;
		}

	public override string TypeToString()
		{
		return "Dictionary";
		}
	}
}
