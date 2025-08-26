using PdfFileAnalyzer.Element;
using System.Collections.Generic;

namespace PdfFileAnalyzer
	{

	public class OutputCtrl
		{
		private readonly List<byte> ByteArray;
		private int EolMarker;

		public OutputCtrl()
			{
			ByteArray = new List<byte>();
			EolMarker = 100;
			return;
			}

		public void Add(byte Chr)
			{
			ByteArray.Add(Chr);
			return;
			}

		public void Add(char Chr)
			{
			ByteArray.Add((byte) Chr);
			return;
			}

		public void AppendText(string Text)
			{
			// remove double delimeters
			if(ByteArray.Count > 0 && !PdfBase.IsDelimiter(ByteArray[ByteArray.Count - 1]) && !PdfBase.IsDelimiter(Text[0]))
				ByteArray.Add((byte) ' '); 

			// move charaters to bytes
			foreach(char Chr in Text) ByteArray.Add((byte) Chr);
			return;
			}

		/// <summary>
		/// Append text message and add end of linw
		/// </summary>
		public void AppendMessage(string Text)
			{
			foreach(char Chr in Text) ByteArray.Add((byte) Chr);
			AddEol();
			return;
			}

		/// <summary>
		/// Add end of line
		/// </summary>
		public void AddEol()
			{
			ByteArray.Add((byte) '\n');
			EolMarker = ByteArray.Count + 100;
			return;
			}

		public void TestEol()
			{
			// add new line to cut down very long lines (just appearance)
			if(ByteArray.Count > EolMarker)
				{
				ByteArray.Add((byte) '\n');
				EolMarker = ByteArray.Count + 100;
				}
			return;
			}

		public void TestEscEol()
			{
			// add new line to cut down very long lines (just appearance)
			if(ByteArray.Count > EolMarker)
				{
				ByteArray.Add((byte) '\\');
				ByteArray.Add((byte) '\n');
				EolMarker = ByteArray.Count + 100;
				}
			return;
			}

		public byte[] ToArray()
			{
			return ByteArray.ToArray();
			}
		}
	}
