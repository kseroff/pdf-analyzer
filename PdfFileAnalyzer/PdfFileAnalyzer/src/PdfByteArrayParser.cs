namespace PdfFileAnalyzer
	{

	public class PdfByteArrayParser : PdfParser
		{
		internal byte[]	Contents;
		internal int	Position;

		public PdfByteArrayParser( PdfReader Reader, byte[] Contents, bool StreamMode) : base(Reader, StreamMode)
			{
			this.Contents = Contents;
			return;
			}

		/// <summary>
		/// Read one byte from contents byte array
		/// </summary>
		/// <returns>One byte within integer</returns>
		public override int ReadChar()
			{
			return Position == Contents.Length ? EOF : Contents[Position++];
			}

		/// <summary>
		/// Step back one character
		/// </summary>
		public override void StepBack()
			{
			Position--;
			return;
			}

		/// <summary>
		/// Get current read position
		/// </summary>
		public override int GetPos()
			{
			return Position;
			}

		/// <summary>
		/// Set current read position
		/// </summary>
		public override void SetPos(int Pos)
			{
			Position = Pos;
			return;
			}

		/// <summary>
		/// Relative set position
		/// </summary>
		public override void SkipPos(int Pos)
			{
			Position += Pos;
			return;
			}
		}
	}
