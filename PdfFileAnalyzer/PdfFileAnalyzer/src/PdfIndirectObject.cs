using PdfFileAnalyzer.Crypto;
using PdfFileAnalyzer.Element;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PdfFileAnalyzer
	{
	public class PdfIndirectObject : PdfObject
		{
		public int StreamFilePosition {get; internal set;}

		public bool IsHidden { get; set; }

		/// <summary>
		/// Это поле – «временное поле», JS-скрипты внутри которого игнорируются
		/// </summary>
		public bool IsTimeField { get; private set; }

		/// <summary>
		/// У этого объекта есть любое JavaScript-действие
		/// </summary>
		public bool ContainsJavaScript { get; private set; }

		/// <summary>Есть ли в этом объекте вложенные файлы (File Attachment)</summary>
		public bool ContainsEmbeddedFile { get; private set; }
		/// <summary>Имена всех вложенных файлов</summary>
		public List<string> EmbeddedFileNames { get; } = new List<string>();

		/// <summary>Есть ли Launch-действие (попытка запустить .exe)</summary>
		public bool ContainsLaunchAction { get; private set; }

		public int StreamLength {get; internal set;}

		/// Contents objects array for page objects only
		public PdfIndirectObject[] ContentsArray {get; internal set;}
        public int Generation { get; internal set; }

        // parent reader object
        internal PdfReader Reader;

		// Constructor for old style cross reference
		internal PdfIndirectObject(PdfReader Reader,int ObjectNumber,int FilePosition)
			{
			// save link to main document object
			this.Reader = Reader;
			this.ObjectNumber = ObjectNumber;
			this.FilePosition = FilePosition;
			return;
			}

		// Constructor for stream style cross reference
		internal PdfIndirectObject(PdfReader Reader,int ObjectNumber,int ParentObjectNo,int ParentObjectIndex)
			{
			// save reader parent
			this.Reader = Reader;

			// save object number
			this.ObjectNumber = ObjectNumber;

			// save parent number and position
			this.ParentObjectNo = ParentObjectNo;
			this.ParentObjectIndex = ParentObjectIndex;

			return;
			}

		/// <summary>
		/// Read stream from PDF file
		/// </summary>
		/// <remarks>
		/// If the PDF file is encrypted, the stream will be decrypted.
		/// No filter is applied. In other words, if the stream is
		/// compressed it will not ne decompressed.
		/// </remarks>
		public byte[] ReadStream()
			{
			// stream is empty
			if(StreamLength == 0) 
				return new byte[0];

			// set file position
			Reader.SetFilePosition(StreamFilePosition);

			// read object stream
			byte[] ByteArray = Reader.PdfBinaryReader.ReadBytes(StreamLength);

			// decrypt stream
			if(Reader.Encryption != null) 
				ByteArray = Reader.Encryption.DecryptByteArray(ObjectNumber, ByteArray);

			return ByteArray;
			}

		/// <summary>
		/// Decompress or apply filters to input stream
		/// </summary>
		public byte[] DecompressStream(byte[] ByteArray)
			{
			// get filter name array
			string[] ReadFilterNameArray = GetFilterNameArray();
			int FilterCount = ReadFilterNameArray == null ? 0 : ReadFilterNameArray.Length;

			// loop for each filter
			for(int Index = 0; Index < FilterCount; Index++)
				{
				string FilterName = ReadFilterNameArray[Index];
				if(FilterName == "/FlateDecode")
					{
					// decompress and replace contents
					ByteArray = FlateDecode(ByteArray);
					byte[] TempContents = PredictorDecode(ByteArray);
					if(TempContents == null) 
						return null;
					ByteArray = TempContents;
					continue;
					}

				if(FilterName == "/LZWDecode")
					{
					// decompress and replace contents
					ByteArray = LZWDecode(ByteArray);
					byte[] TempContents = PredictorDecode(ByteArray);
					if(TempContents == null) 
						return null;
					ByteArray = TempContents;
					continue;
					}

				if(FilterName == "/ASCII85Decode")
					{
					// decode and replace contents
					ByteArray = Ascii85Decode(ByteArray);
					continue;
					}

				// for jpg image, return uncompressed stream
				if(FilterName == "/DCTDecode")
					{
					PdfObjectType = "/JpegImage";
					return ByteArray;
					}

				return null;
				}

			return ByteArray;
			}

		public void BuildContentsArray()
			{
			// must be a page
			if(PdfObjectType != "/Page") 
				throw new ApplicationException("Build contents array: Object must be page");

			// get Contents dictionary value
			PdfBase ContentsValue = Dictionary.FindValue("/Contents");

			// page is blank no contents
			if(ContentsValue.IsEmpty)
				{
				ContentsArray = new PdfIndirectObject[0];
				return;
				}

			// test if contents value is a reference
			if(ContentsValue.IsReference)
				{
				// find the object with Object number
				PdfIndirectObject IndirectObject = Reader.ToPdfIndirectObject((PdfReference) ContentsValue);
				if(IndirectObject != null)
					{
					// the object is a stream return array with one contents object
					if(IndirectObject.ObjectType == ObjectType.Stream)
						{
						IndirectObject.PdfObjectType = "/Contents";
						ContentsArray = new PdfIndirectObject[] {IndirectObject};
						return;
						}

					// read object must be an array
					if(IndirectObject.ObjectType == ObjectType.Other) ContentsValue = IndirectObject.Value;
					}
				}

			// test if contents value is an array
			if(!ContentsValue.IsArray) throw new ApplicationException("Build contents array: /Contents must be array");

			// array of reference numbers to contents objects
			PdfBase[] ReferenceArray = ((PdfArray) ContentsValue).ArrayItems;

			// create empty result list
			ContentsArray = new PdfIndirectObject[ReferenceArray.Length];

			// verify that all array items are references to streams
			for(int Index = 0; Index < ReferenceArray.Length; Index++)
				{
				// shortcut
				PdfBase ContentsRef = ReferenceArray[Index];

				// each item must be a reference
				if(!ContentsRef.IsReference) throw new ApplicationException("Build contents array: Array item must be reference");

				// get read object
				PdfIndirectObject Contents = Reader.ToPdfIndirectObject((PdfReference) ContentsRef);

				// the object is not a stream
				if(Contents == null || Contents.ObjectType != ObjectType.Stream) throw new ApplicationException("Build contents array: Contents must be a stream");

				// mark as page's contents
				Contents.PdfObjectType = "/Contents";

				// add stream to the array
				ContentsArray[Index] = Contents;
				}

			// successful exit
			return;
			}

		/// <summary>
		/// Page contents is the total of all its contents objects
		/// </summary>
		public byte[] PageContents()
			{
			// build contents array
			if(ContentsArray == null) 
				BuildContentsArray();

			// page has no contents
			if(ContentsArray.Length == 0) 
				return new byte[0];

			// array with one item
			if (ContentsArray.Length == 1)
				{
				// read contents stream
				byte[] StreamArray = ContentsArray[0].ReadStream();
				StreamArray = ContentsArray[0].DecompressStream(StreamArray);
				if(StreamArray == null) 
					throw new ApplicationException("Page contents decompress error");
				return StreamArray;
				}

			// read all contents streams
			byte[] ByteArray = null;
			foreach(PdfIndirectObject ContObj in ContentsArray)
				{
				// stream is empty
				if(ContObj.StreamLength == 0) 
					continue;

				// read contents stream
				byte[] StreamArray = ContObj.ReadStream();
				StreamArray = ContObj.DecompressStream(StreamArray);
				if(StreamArray == null) 
					throw new ApplicationException("Page contents error");

				// first contents
				if(ByteArray == null)
					{
					ByteArray = StreamArray;
					continue;
					}

				// append stream array to byte array
				int OldLen = ByteArray.Length;
				Array.Resize<byte>(ref ByteArray, OldLen + StreamArray.Length + 1);
				ByteArray[OldLen] = (byte) '\n';
				Array.Copy(StreamArray, 0, ByteArray, OldLen + 1, StreamArray.Length);
				}

			if(ByteArray == null) 
				ByteArray = new byte[0];

			return ByteArray;
			}

		internal void ReadObject(bool minimal = false)
			{

			// если объект удален (generation == 65535)
			if (Generation == 65535) 
				return;

			// skip if done already or child of object stream
			if (ObjectType != ObjectType.Free || ParentObjectNo != 0) 
				return;

			// set file position
			Reader.SetFilePosition(FilePosition);

			// read first byte
			Reader.ParseFile.ReadFirstChar();

			// first token must be object number "nnn 0 obj"
			if(Reader.ParseFile.ParseObjectRefNo() != ObjectNumber) 
				throw new ApplicationException("Reading object header failed");

			// read next token
			Value = Reader.ParseFile.ParseNextItem();

			// we have a dictionary
			if (Value.IsDictionary)
				{
				// set object value type to dictionary
				ObjectType = ObjectType.Dictionary;
				Dictionary = (PdfDictionary) Value;
				Value = null;

				// set object type if available in the dictionary
				string ObjectTypeStr = Dictionary.FindValue("/Type").ToName;

				if (ObjectTypeStr == "/OCG")
				{

					if (ObjectTypeStr == "/OCG" && Reader.HiddenOCGs != null && Reader.HiddenOCGs.Contains(this.ObjectNumber))
					{
						IsHidden = true;
					}
				}

				// 1) Аннотация‑виджет
				if (Dictionary.FindValue("/Subtype").ToName == "/Widget" &&
					Dictionary.FindValue("/F").GetInteger(out int aflags))
				{
					IsHidden = (aflags & (1 | 2)) != 0;
				}

				// 2) Поле AcroForm
				if (Dictionary.FindValue("/FT").IsName &&
					Dictionary.FindValue("/Ff").GetInteger(out int fflags))
				{
					IsHidden |= (fflags & 0x2000) != 0;
				}

				// 3) Поле слой
				var ocBase = Dictionary.FindValue("/OC");
				if (ocBase.IsReference)
				{
					int ocNo = ((PdfReference)ocBase).ObjectNumber;
					if (Reader.HiddenOCGs != null && Reader.HiddenOCGs.Contains(ocNo))
					{
						IsHidden = true;
					}
				}

				// 4) Определяем, является ли это текстовым полем «Time»
				//     FT=/Tx и по имени поля /T, содержащему слово time
				if (Dictionary.FindValue("/FT").ToName == "/Tx")
				{
					var fieldName = Dictionary.FindValue("/T").ToText;
					if (!string.IsNullOrEmpty(fieldName) && fieldName.IndexOf("time", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						IsTimeField = true;
					}
				}

				// 5) Если это не time-поле, проверяем, есть ли у объекта JavaScript-действия
				if (!IsTimeField)
				{
					ContainsJavaScript = DetectJavaScriptActions();
				}

				// 6) Проверка вложенных файлов: Filespec или EF
				//  Сам Filespec-объект
				if (Dictionary.FindValue("/Type").ToName == "/Filespec")
				{
					ContainsEmbeddedFile = true;
					var fname = Dictionary.FindValue("/F").ToName ?? Dictionary.FindValue("/UF").ToText;
					if (!string.IsNullOrEmpty(fname))
						EmbeddedFileNames.Add(fname);
				}
				// 6.2) «Embedded Files» у аннотации или формы (/EF)
				var efBase = Dictionary.FindValue("/EF");
				if (efBase.IsDictionary)
				{
					ContainsEmbeddedFile = true;
					foreach (var kv in efBase.ToDictionary.KeyValueArray)
					{
						var refVal = kv.Value;
						if (refVal.IsReference)
						{
							var fsObj = Reader.ToPdfIndirectObject((PdfReference)refVal);
							var name = fsObj?.Dictionary.FindValue("/F").ToName;
							if (!string.IsNullOrEmpty(name))
								EmbeddedFileNames.Add(name);
						}
					}
				}

				// 7) Детекция Launch-action (S /Launch)
				// Ищем /A, /AA, /OpenAction на /Launch
				bool IsLaunchAction(PdfDictionary act)=> act.FindValue("/S").ToName == "/Launch";
				var aBase = Dictionary.FindValue("/A").ToDictionary;
				var aaBase = Dictionary.FindValue("/AA").ToDictionary;
				var openBase = Dictionary.FindValue("/OpenAction").ToDictionary;
				if ((aBase != null && IsLaunchAction(aBase)) ||(aaBase != null && aaBase.KeyValueArray.Any(kv => 
				{
					var d = kv.Value.ToDictionary; return d != null && IsLaunchAction(d);
				})) || (openBase != null && IsLaunchAction(openBase)))
					{
						ContainsLaunchAction = true;
					}

				// set special object
				if (ObjectTypeStr != null) 
					PdfObjectType = ObjectTypeStr;

				// read next token after the dictionary
				KeyWord KeyWord = Reader.ParseFile.ParseNextItem().ToKeyWord;

				// test for stream (change object from dictionary to stream)
				if(KeyWord == KeyWord.Stream)
					{
					// set object value type to stream
					ObjectType = ObjectType.Stream;

					// save start of stream position
					StreamFilePosition = Reader.GetFilePosition();
					}

				// if it is no stream test for endobj
				else if(KeyWord != KeyWord.EndObj) 
					throw new ApplicationException("'endobj' token is missing");
				}

			// object is not a dictionary and not a sream
			else
				{
				ObjectType = ObjectType.Other;

				// test for endobj 
				if(Reader.ParseFile.ParseNextItem().ToKeyWord != KeyWord.EndObj) 
					throw new ApplicationException("'endobj' token is missing");
				}

			// exit
			return;
			}

		private bool DetectJavaScriptActions()
		{
			// 1) Additional Actions (/AA)
			var aaDict = Reader.ToPdfDictionary(Dictionary, "/AA");
			if (aaDict != null)
			{
				foreach (var kv in aaDict.KeyValueArray)
				{
					var actionDict = Reader.ToPdfDictionary(aaDict, kv.Key);
					if (actionDict != null && actionDict.FindValue("/S").ToName == "/JavaScript")
						return true;
				}
			}

			// 2) Основное действие (/A)
			var aDict = Reader.ToPdfDictionary(Dictionary, "/A");
			if (aDict != null && aDict.FindValue("/S").ToName == "/JavaScript")
				return true;

			// 3) OpenAction (страница или документ)
			var openDict = Reader.ToPdfDictionary(Dictionary, "/OpenAction");
			if (openDict != null && openDict.FindValue("/S").ToName == "/JavaScript")
				return true;

			// 4) Если в объекте есть ключ /JS — это JavaScript (literal или hex-строка)
			PdfBase jsVal = Dictionary.FindValue("/JS");
			// и literal-строки, и hex-литералы парсятся в PdfString
			if (jsVal.IsPdfString)
				return true;
			// или если /JS — ссылка на стрим
			if (jsVal.IsReference)
			{
				var jsObj = Reader.ToPdfIndirectObject((PdfReference)jsVal);
				if (jsObj != null && jsObj.ObjectType == ObjectType.Stream)
					return true;
			}

			return false;
		}

		// Stream length might be in another indirect object
		// This method must run after ReadObject was run for all objects
		internal void GetStreamLength()
			{
			// get value
			PdfBase LengthValue = Dictionary.FindValue("/Length");

			// dictionary value is reference to integer
			if(LengthValue.IsReference)
				{
				// get indirect object based on reference number
				PdfIndirectObject LengthObject = Reader.ToPdfIndirectObject((PdfReference) LengthValue);

				// read object type
				if(LengthObject != null && LengthObject.ObjectType == ObjectType.Other && LengthObject.Value.IsInteger)
					StreamLength = ((PdfInteger) LengthObject.Value).IntValue;

				// replace /Length in dictionary with actual value
				Dictionary.AddInteger("/Length", StreamLength);
				}
			else if(LengthValue.IsInteger) // dictionary value is integer
			{
				// save stream length
				StreamLength = ((PdfInteger) LengthValue).IntValue;
			}

			// stream is empty or stream length is in error
			if(StreamLength == 0) 
				return;

			// stream might be outside file boundry
			// HP Scanners Scanned PDF does not conform to PDF standards
			try
				{ 
				// set file position to the end of the stream
				Reader.SetFilePosition(StreamFilePosition + StreamLength);

				// verify end of stream
				// read first byte
				Reader.ParseFile.ReadFirstChar();

				// test for endstream 
				if(Reader.ParseFile.ParseNextItem().ToKeyWord != KeyWord.EndStream) 
					throw new ApplicationException("Endstream token missing");

				// test for endobj 
				if(Reader.ParseFile.ParseNextItem().ToKeyWord != KeyWord.EndObj) 
					throw new ApplicationException("Endobj token missing");
				return;
				}
			catch
				{
				StreamLength = 0;
				Reader.InvalidPdfFile = true;
				return;
				}
			}

		internal void ProcessObjectsStream()
		{
			// 1) Считываем и расшифровываем/декомпрессируем поток
			byte[] streamBytes = ReadStream();
			streamBytes = DecompressStream(streamBytes);

			// 2) Получаем N и First
			if (!Dictionary.FindValue("/N").GetInteger(out int objectCount) || objectCount <= 0)
				throw new ApplicationException("Object stream: count (/N) is missing");

			if (!Dictionary.FindValue("/First").GetInteger(out int firstOffset))
				throw new ApplicationException("Object stream: first byte offset (/First) is missing");

			// 3) Обработка цепочки object streams (/Extends)
			PdfBase extendsBase = Dictionary.FindValue("/Extends");
			if (extendsBase.IsReference)
				ParentObjectNo = ((PdfReference)extendsBase).ObjectNumber;

			// 4) Подготовим парсер по массиву байт
			var parser = new PdfByteArrayParser(Reader, streamBytes, false);
			parser.ReadFirstChar();

			// 5) Массив ссылок на дочерние объекты
			PdfIndirectObject[] children = new PdfIndirectObject[objectCount];

			// 6) Первый проход: читаем пары (ObjNo, ObjPos)
			for (int i = 0; i < objectCount; i++)
			{
				// номер объекта
				if (!parser.ParseNextItem().GetInteger(out int objNo))
					throw new ApplicationException("Cross reference object stream: object number error");

				// смещение внутри потока
				if (!parser.ParseNextItem().GetInteger(out int objPos))
					throw new ApplicationException("Cross reference object stream: object offset error");

				// 6.1) Пытаемся найти в ObjectTable
				if (!Reader.ObjectTable.TryGetValue(objNo, out var versions))
					continue;

				// 6.2) Ищем ту запись, у которой parent == this и индекс == i
				var child = versions.FirstOrDefault(o =>
					o.ParentObjectNo == this.ObjectNumber &&
					o.ParentObjectIndex == i &&
					o.ObjectType == ObjectType.Free  // ещё не разобранный
				);
				if (child == null)
					continue;

				// 6.3) Сохраняем на эту позицию
				children[i] = child;
				child.FilePosition = firstOffset + objPos;
			}

			// 7) Второй проход: действительно разбираем каждый дочерний объект
			foreach (var child in children)
			{
				if (child == null)
					continue;

				parser.SetPos(child.FilePosition);
				parser.ReadFirstChar();

				PdfBase parsed = parser.ParseNextItem();
				if (parsed.IsDictionary)
				{
					child.ObjectType = ObjectType.Dictionary;
					child.Dictionary = (PdfDictionary)parsed;

					// Опционально: сохраняем тип объекта из /Type
					string typeName = child.Dictionary.FindValue("/Type").ToName;
					if (typeName != null)
						child.PdfObjectType = typeName;
				}
				else
				{
					child.ObjectType = ObjectType.Other;
					child.Value = parsed;
				}
			}
		}

		// Get filter names
		internal string[] GetFilterNameArray()
			{
			// look for filter
			PdfBase Filter = Dictionary.FindValue("/Filter");

			// no filter
			if(Filter.IsEmpty) return null;

			// one filter name
			if(Filter.IsName)
				{
				string[] FilterNameArray = new string[1];
				FilterNameArray[0] = ((PdfName) Filter).NameValue;
				return FilterNameArray;
				}

			// array of filters
			if(Filter.IsArray)
				{
				// filter name items
				PdfBase[] FilterNames = ((PdfArray) Filter).ArrayItems;
				string[] FilterNameArray = new string[FilterNames.Length];

				// loop for each filter
				int Index;
				for(Index = 0; Index < FilterNames.Length; Index++)
					{
					if(!FilterNames[Index].IsName) break;
					FilterNameArray[Index] = ((PdfName) FilterNames[Index]).NameValue;
					}
				if(Index == FilterNames.Length) return FilterNameArray;
				}

			// filter is in error
			throw new ApplicationException("/Filter nust be a name or an array of names");
			}

		/// <summary>
		/// Apply flate decode filter
		/// </summary>
		internal static byte[] FlateDecode(byte[] InputBuffer)
			{
			// get ZLib header
			int Header = (int) InputBuffer[0] << 8 | InputBuffer[1];

			// test header: chksum, compression method must be deflated, no support for external dictionary
			if(Header % 31 != 0 || (Header & 0xf00) != 0x800 && (Header & 0xf00) != 0 || (Header & 0x20) != 0)
				throw new ApplicationException("ZLIB file header is in error");

			// output buffer
			byte[] OutputBuf;

			// decompress the file
			if((Header & 0xf00) == 0x800)
				{
				// create input stream
				MemoryStream InputStream = new MemoryStream(InputBuffer, 2, InputBuffer.Length - 6);

				// create output memory stream to receive the decompressed buffer
				MemoryStream OutputStream = new MemoryStream();

				// deflate decompression object
				DeflateStream Deflate = new DeflateStream(InputStream, CompressionMode.Decompress);
				Deflate.CopyTo(OutputStream);

				// decompressed file length
				int OutputLen = (int) OutputStream.Length;

				// create output buffer
				OutputBuf = new byte[OutputLen];

				// copy the compressed result
				OutputStream.Seek(0, SeekOrigin.Begin);
				OutputStream.Read(OutputBuf, 0, OutputLen);
				OutputStream.Close();
				}
			else
				{
				// no compression
				OutputBuf = new byte[InputBuffer.Length - 6];
				Array.Copy(InputBuffer, 2, OutputBuf, 0, OutputBuf.Length);
				}

			// ZLib checksum is Adler32
			int ReadPtr = InputBuffer.Length - 4;
			if((((uint) InputBuffer[ReadPtr++] << 24) | ((uint) InputBuffer[ReadPtr++] << 16) |
				((uint) InputBuffer[ReadPtr++] << 8) | ((uint) InputBuffer[ReadPtr++])) != Adler32.Checksum(OutputBuf))
					throw new ApplicationException("ZLIB file Adler32 test failed");

			// successful exit
			return OutputBuf;
			}

		internal static byte[] LZWDecode(byte[] InputBuffer)
			{
			// decompress
			return LZW.Decode(InputBuffer);
			}

		/// <summary>
		/// Apply predictor decode
		/// </summary>
		internal byte[] PredictorDecode(byte[] InputBuffer)
			{
			// test for /DecodeParams
			PdfDictionary DecodeParms = Dictionary.FindValue("/DecodeParms").ToDictionary;

			// none found
			if(DecodeParms == null) return InputBuffer;

			// look for predictor code. if default (none or 1) do nothing
			if(!DecodeParms.FindValue("/Predictor").GetInteger(out int Predictor) || Predictor == 1) return InputBuffer;

			// we only support predictor code 12
			if(Predictor != 12) return null;

			// get width
			DecodeParms.FindValue("/Columns").GetInteger(out int Width);
			if(Width < 0) throw new ApplicationException("/DecodeParms /Columns is negative");
			if(Width == 0) Width = 1;

			// calculate rows
			int Rows = InputBuffer.Length / (Width + 1);
			if(Rows < 1) throw new ApplicationException("/DecodeParms /Columns is greater than stream length");

			// create output buffer
			byte[] OutputBuffer = new byte[Rows * Width];

			// reset pointers
			int InPtr = 1;
			int OutPtr = 0;
			int OutPrevPtr = 0;

			// first row (ignore filter)
			while(OutPtr < Width) OutputBuffer[OutPtr++] = InputBuffer[InPtr++];

			// decode loop
			for(int Row = 1; Row < Rows; Row++)
				{
				// first byte is filter
				int Filter = InputBuffer[InPtr++];

				// we support PNG filter up only
				if(Filter != 2) throw new ApplicationException("/DecodeParms Only supported filter is 2");

				// convert input to output
				for(int Index = 0; Index < Width; Index++) OutputBuffer[OutPtr++] = (byte) (OutputBuffer[OutPrevPtr++] + InputBuffer[InPtr++]);
				}

			return OutputBuffer;
			}

		internal static byte[] Ascii85Decode(byte[] InputBuffer)
			{
			// array of power of 85: 85**4, 85**3, 85**2, 85**1, 85**0
			uint[] Power85 = new uint[] {85*85*85*85, 85*85*85, 85*85, 85, 1}; 

			// output buffer
			List<byte> OutputBuffer = new List<byte>();

			// convert input to output buffer
			int State = 0;
			uint FourBytes = 0;
			for(int Index = 0; Index < InputBuffer.Length; Index++)
				{
				// next character
				char NextChar = (char) InputBuffer[Index];

				// end of stream "~>"
				if(NextChar == '~')
					break;

				// ignore white space
				if(PdfBase.IsWhiteSpace(NextChar)) 
					continue;

				// special case of four zero bytes
				if(NextChar == 'z' && State == 0)
					{
					OutputBuffer.Add(0);
					OutputBuffer.Add(0);
					OutputBuffer.Add(0);
					OutputBuffer.Add(0);
					continue;
					}

				// test for valid characters
				if(NextChar < '!' || NextChar > 'u') 
					throw new ApplicationException("Illegal character in ASCII85Decode");

				// accumulate 4 output bytes from 5 input bytes
				FourBytes += Power85[State++] * (uint) (NextChar - '!');

				// we have 4 output bytes
				if(State == 5)
					{
					OutputBuffer.Add((byte)(FourBytes >> 24));
					OutputBuffer.Add((byte)(FourBytes >> 16));
					OutputBuffer.Add((byte)(FourBytes >> 8));
					OutputBuffer.Add((byte) FourBytes);

					// reset state
					State = 0;
					FourBytes = 0;
					}
				}

			// if state is not zero add one, two or three terminating bytes
			if(State != 0)
				{
				if(State == 1) 
					throw new ApplicationException("Illegal length in ASCII85Decode");

				// add padding of 84
				for(int PadState = State; PadState < 5; PadState++) 
					FourBytes += Power85[PadState] * (uint) ('u' - '!');

				// add one, two or three terminating bytes
				OutputBuffer.Add((byte)(FourBytes >> 24));
				if(State >= 3)
					{
					OutputBuffer.Add((byte)(FourBytes >> 16));
					if(State >= 4) 
						OutputBuffer.Add((byte)(FourBytes >> 8));
					}
				}

			// exit
			return OutputBuffer.ToArray();
			}

		// Write indirect object to object analysis file
		internal void ObjectSummary(OutputCtrl Ctrl)
			{
			// write object header
			Ctrl.AppendMessage(string.Format("Object number: {0}", ObjectNumber));
			Ctrl.AppendMessage(string.Format("Object Value Type: {0}", ObjectDescription()));
			Ctrl.AppendMessage(string.Format("File Position: {0} Hex: {0:X}", FilePosition));
			if(ParentObjectNo != 0)
				{
				Ctrl.AppendMessage(string.Format("Parent object number: {0}", ParentObjectNo));
				Ctrl.AppendMessage(string.Format("Parent object index: {0}", ParentObjectIndex));
				}
			if(ObjectType == ObjectType.Stream)
				{
				Ctrl.AppendMessage(string.Format("Stream Position: {0} Hex: {0:X}", StreamFilePosition));
				Ctrl.AppendMessage(string.Format("Stream Length: {0} Hex: {0:X}", StreamLength));
				}

			// dictionary or stream
			if(ObjectType == ObjectType.Dictionary || ObjectType == ObjectType.Stream)
				{
				string ObjectTypeStr = Dictionary.FindValue("/Type").ToName;
				if(ObjectTypeStr == null) 
					ObjectTypeStr = PdfObjectType;
				if(ObjectTypeStr != null) 
					Ctrl.AppendMessage(string.Format("Object Type: {0}", ObjectTypeStr));

				string ObjectSubtypeStr = Dictionary.FindValue("/Subtype").ToName;
				if(ObjectSubtypeStr != null) 
					Ctrl.AppendMessage(string.Format("Object Subtype: {0}", ObjectSubtypeStr));

				// write to pdf file
				Dictionary.ToByteArray(Ctrl);

				// final terminator
				Ctrl.AddEol();
				}

			// object has contents that is not stream
			else if(ObjectType == ObjectType.Other)
				{
				// write content to pdf file
				Value.ToByteArray(Ctrl);

				// final terminator
				Ctrl.AddEol();
				}	

			// final terminator
			Ctrl.AddEol();

			return;
			}

		public string ObjectSubtypeToString()
			{
			// not dictionary nor stream
			if(ObjectType != ObjectType.Dictionary && ObjectType != ObjectType.Stream) 
				return null;
			return Dictionary.FindValue("/Subtype").ToName;
			}
		}
	}
