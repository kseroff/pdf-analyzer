using System;
using System.IO;

namespace PdfFileAnalyzer
	{

	public class PdfFileParser : PdfParser
	{

	internal BinaryReader PdfBinaryReader;

	public PdfFileParser( PdfReader Reader ) : base(Reader, false)
		{
		PdfBinaryReader = Reader.PdfBinaryReader;
		return;
		}

	/// <summary>
	/// Read one byte from input stream
	/// </summary>
	public override int ReadChar()
	{
		try {
			return PdfBinaryReader.ReadByte();
		}
		catch {
			throw new ApplicationException("Unexpected end of file");
		}
	}

	// Step back one byte
	public override void StepBack()
		{
		PdfBinaryReader.BaseStream.Position--;
		return;
		}

	public override int GetPos()
		{
		return (int) PdfBinaryReader.BaseStream.Position;
		}

	public override void SetPos(int Pos)
		{
		PdfBinaryReader.BaseStream.Position = Pos;
		return;
		}

	// Set relative position
	public override void SkipPos(int Pos)
		{
		PdfBinaryReader.BaseStream.Position += Pos;
		return;
		}
	}
}
