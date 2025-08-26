using PdfFileAnalyzer.Crypto;
using PdfFileAnalyzer.Element;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PdfFileAnalyzer
{
    public enum DecryptionStatus
    {
        FileNotProtected, // File is not protected
        OwnerPassword, // File was decrypted with owner password
        UserPassword, // File was decrypted with user password
        InvalidPassword, // Decryption failed
        Unsupported, // No support for encryption method used
    }

    public class PdfReader : IDisposable
    {
        public bool Active { get; internal set; }
        public bool InvalidPdfFile { get; internal set; }
        public DecryptionStatus DecryptionStatus { get; internal set; }

        // Таблица всех версий объектов: ключ — номер объекта, значение — список всех поколений.
        public Dictionary<int, List<PdfIndirectObject>> ObjectTable { get; private set; }
        public PdfDictionary TrailerDict { get; internal set; }
        public PdfIndirectObject Catalog { get; internal set; }
        public string FileName { get; private set; }
        public string SafeFileName { get; private set; }

        internal BinaryReader PdfBinaryReader;
        internal PdfFileParser ParseFile;
        internal int StartPosition;
        internal bool TableCrossReference;
        internal int[] ObjStmArray;

        internal PdfDictionary EncryptionDict;
        internal byte[] DocumentID;
        internal EncryptionType EncryptionType;
        internal int Permissions;
        internal byte[] OwnerKey;
        internal byte[] UserKey;
        internal CryptoEngine Encryption;

        public HashSet<int> HiddenOCGs { get; private set; }

        private void InitObjectTable(int size)
        {
            ObjectTable = new Dictionary<int, List<PdfIndirectObject>>(size);
        }

        internal int GetFilePosition()
        {
            return (int)(PdfBinaryReader.BaseStream.Position - StartPosition);
        }

        internal void SetFilePosition(int Position)
        {
            PdfBinaryReader.BaseStream.Position = StartPosition + Position;
            return;
        }

        public bool OpenPdfFile( string FileName, string Password = null )
        {
            if (!FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("PDF file must have .pdf extension");

            if (!File.Exists(FileName)) throw new ArgumentException("PDF file does not exist");

            this.FileName = FileName;

            // safe file name is a name with no path
            SafeFileName = FileName.Substring(FileName.LastIndexOf('\\') + 1);

            // open pdf file for reading
            PdfBinaryReader = new BinaryReader(new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.UTF8);

            // create parse file object
            ParseFile = new PdfFileParser(this);

            ValidateFile();

            // search for document ID
            PdfBase TempIDArray = TrailerDict.FindValue("/ID");
            if (TempIDArray.IsArray)
            {
                // document ID is an array of two ids. Normally the two are the same
                PdfBase[] IDArray = ((PdfArray)TempIDArray).ArrayItems;

                // take the firat as the id for encryption
                if (IDArray.Length > 0 && IDArray[0].IsPdfString) DocumentID = ((PdfString)IDArray[0]).StrValue;
            }

            // search for /Encrypt
            PdfBase TempEncryptionDict = TrailerDict.FindValue("/Encrypt");

            // document is not encrypted
            if (TempEncryptionDict.IsEmpty)
            {
                // document is not encrypted
                DecryptionStatus = DecryptionStatus.FileNotProtected;

                // set reader active
                SetReaderActive();
                return true;
            }

            // value is a reference
            if (TempEncryptionDict.IsReference)
            {
                PdfIndirectObject ReaderObject = ToPdfIndirectObject((PdfReference)TempEncryptionDict);
                if (ReaderObject != null)
                {
                    ReaderObject.ReadObject();
                    if (ReaderObject.ObjectType == ObjectType.Dictionary)
                    {
                        ReaderObject.PdfObjectType = "/Encryption";
                        TempEncryptionDict = ReaderObject.Dictionary;
                    }
                }
            }

            if (!TempEncryptionDict.IsDictionary) 
                throw new ApplicationException("Encryption dictionary is missing");

            // save encryption dictionary
            EncryptionDict = (PdfDictionary)TempEncryptionDict;

            // decryption is not possible without document ID
            if (DocumentID == null) 
                throw new ApplicationException("Encrypted document document ID is missing.");

            // encryption method is not supported by this library
            if (!TestEncryptionSupport()) 
                return false;

            // try given password or default password
            if (!TestPassword(Password)) 
                return false;

            return true;
        }

        /// <summary>
        /// Get PdfReference from a parent PdfIndirectObject
        /// </summary>
        public PdfIndirectObject ToPdfIndirectObject(PdfReference reference)
        {
            if (!ObjectTable.TryGetValue(reference.ObjectNumber, out var list) || list.Count == 0)
                return null;

            // точное поколение
            var exact = list.FirstOrDefault(o => o.Generation == reference.Generation);
            if (exact != null)
            {
                // объект не должен быть помечен как удалённый
                if (exact.Generation == 65535) return null;
                return exact;
            }

            // если запрошенная генерация отсутствует, но есть удалённая — считаем, что объект недоступен
            if (list.Any(o => o.Generation == 65535)) return null;

            // возвращаем версию с наибольшим поколением
            return list.OrderByDescending(o => o.Generation).FirstOrDefault();
        }

        /// <summary>
        /// Get PdfDictionary from a parent PdfDictionary
        /// </summary>
        public PdfDictionary ToPdfDictionary(PdfDictionary ParentDict, string Key)
        {
            // look for key entry in this dictionary
            int Index = ParentDict.KeyValueArray.BinarySearch(new PdfKeyValue(Key));
            if (Index < 0) 
                return null;

            // get dictionary directly or indirectly
            return ToPdfDictionary(ParentDict.KeyValueArray[Index].Value);
        }

        /// <summary>
        /// Convert PdfBase value to PdfDictionary
        /// </summary>
        public PdfDictionary ToPdfDictionary(PdfBase BaseValue)
        {

            if (BaseValue.IsDictionary) 
                return (PdfDictionary)BaseValue;

            if (!BaseValue.IsReference) 
                return null;

            PdfIndirectObject IndirectObj = ToPdfIndirectObject((PdfReference)BaseValue);

            if (IndirectObj == null || IndirectObj.ObjectType != ObjectType.Dictionary) 
                return null;

            return IndirectObj.Dictionary;
        }

        /// <summary>
        /// Get PdfArray from a dictionary
        /// </summary>
        public PdfArray ToPdfArray(PdfDictionary ParentDict, string Key )
        {
            int Index = ParentDict.KeyValueArray.BinarySearch(new PdfKeyValue(Key));
            if (Index < 0) return null;

            return ToPdfArray(ParentDict.KeyValueArray[Index].Value);
        }

        /// <summary>
        /// Convert PdfBase value to PdfArray
        /// </summary>
        public PdfArray ToPdfArray(PdfBase BaseValue)
        {
            if (BaseValue.IsArray) 
                return (PdfArray)BaseValue;

            if (!BaseValue.IsReference) 
                return null;

            PdfIndirectObject IndirectObj = ToPdfIndirectObject((PdfReference)BaseValue);

            if (IndirectObj == null || IndirectObj.ObjectType != ObjectType.Other || !IndirectObj.Value.IsArray) 
                return null;

            return (PdfArray)IndirectObj.Value;
        }

        /// <summary>
        /// Test Password
        /// </summary>
        public bool TestPassword(string Password)
        {
            if (Active) 
                return true;

            // create encryption object
            Encryption = new CryptoEngine(EncryptionType, DocumentID, Permissions, UserKey, OwnerKey);

            // try password
            if (Encryption.TestPassword(Password))
            {
                // copy decryption status
                DecryptionStatus = Encryption.DecryptionStatus;

                // set reader active
                SetReaderActive();
                return true;
            }

            // copy decryption status
            DecryptionStatus = Encryption.DecryptionStatus;

            // dispose of encryption object
            Encryption.Dispose();
            Encryption = null;

            // decryption failed
            return false;
        }

        /// <summary>
        /// Test if this software can decrypt the file If it can create CryptoEngine object and try
        /// to decrypt with default password
        /// </summary>
        internal bool TestEncryptionSupport()
        {
            // assume unsupported
            DecryptionStatus = DecryptionStatus.Unsupported;
            EncryptionType = EncryptionType.Unsupported;

            PdfBase Temp = EncryptionDict.FindValue("/Filter");
            if (!Temp.IsName || Temp.ToName != "/Standard") 
                return false;

            if (!EncryptionDict.FindValue("/Length").GetInteger(out int KeyLen) || KeyLen != 128) 
                return false;

            if (!TryResolveInteger(EncryptionDict.FindValue("/P"), out Permissions))
                return false;

            Temp = EncryptionDict.FindValue("/O");
            if (!Temp.IsPdfString) 
                return false;
            OwnerKey = ((PdfString)Temp).StrValue;

            Temp = EncryptionDict.FindValue("/U");
            if (!Temp.IsPdfString) 
                return false;
            UserKey = ((PdfString)Temp).StrValue;

            if (!EncryptionDict.FindValue("/R").GetInteger(out int R)) 
                return false;

            if (!EncryptionDict.FindValue("/V").GetInteger(out int V)) 
                return false;

            // test for AES128
            if (R == 4 && V == 4)
            {
                Temp = EncryptionDict.FindValue("/StrF");
                if (!Temp.IsName || Temp.ToName != "/StdCF")
                    return false;

                Temp = EncryptionDict.FindValue("/StmF");
                if (!Temp.IsName || Temp.ToName != "/StdCF") 
                    return false;

                Temp = EncryptionDict.FindValue("/CF");
                if (!Temp.IsDictionary) 
                    return false;
                PdfDictionary CFDict = Temp.ToDictionary;

                Temp = CFDict.FindValue("/StdCF");
                if (!Temp.IsDictionary) 
                    return false;
                PdfDictionary StdCFDict = Temp.ToDictionary;

                if (!StdCFDict.FindValue("/Length").GetInteger(out int Len) || Len != 16) 
                    return false;

                Temp = StdCFDict.FindValue("/AuthEvent");
                if (!Temp.IsName || Temp.ToName != "/DocOpen") 
                    return false;

                Temp = StdCFDict.FindValue("/CFM");
                if (!Temp.IsName || Temp.ToName != "/AESV2") 
                    return false;

                // save result
                EncryptionType = EncryptionType.Aes128;
                return true;
            }

            // test for Standard 128
            if (R == 3 && V == 2)
            {
                // save result
                EncryptionType = EncryptionType.Standard128;
                return true;
            }

            //test AES256
            /*if (R == 6 && V == 5)
            {
                EncryptionType = EncryptionType.Aes256;

                // Извлекаем ключи
                var o = EncryptionDict.FindValue("/O");
                var u = EncryptionDict.FindValue("/U");
                var oe = EncryptionDict.FindValue("/OE");
                var ue = EncryptionDict.FindValue("/UE");
                var perms = EncryptionDict.FindValue("/Perms");

                if (!o.IsPdfString || !u.IsPdfString || !oe.IsPdfString || !ue.IsPdfString || !perms.IsPdfString)
                    return false;

                OwnerKey = ((PdfString)o).StrValue;
                UserKey = ((PdfString)u).StrValue;
                OwnerKeyEncrypted = ((PdfString)oe).StrValue;
                UserKeyEncrypted = ((PdfString)ue).StrValue;
                PermsEncrypted = ((PdfString)perms).StrValue;

                // salt из /O и /U
                OValidationSalt = new byte[8];
                OKeySalt = new byte[8];
                UValidationSalt = new byte[8];
                UKeySalt = new byte[8];
                Array.Copy(OwnerKey, 32, OValidationSalt, 0, 8);
                Array.Copy(OwnerKey, 40, OKeySalt, 0, 8);
                Array.Copy(UserKey, 32, UValidationSalt, 0, 8);
                Array.Copy(UserKey, 40, UKeySalt, 0, 8);

                return true;
            }*/

            // encryption is not supported
            return false;
        }

        private bool TryResolveInteger(PdfBase val, out int result)
        {
            result = 0;

            if (val.IsInteger && val is PdfInteger i)
            {
                result = i.IntValue;
                return true;
            }

            if (val.IsReal && val is PdfReal r)
            {
                if (r.RealValue >= int.MinValue && r.RealValue <= int.MaxValue)
                {
                    result = (int)r.RealValue;
                    return true;
                }
            }

            if (val.IsReference)
            {
                var obj = ToPdfIndirectObject((PdfReference)val);
                if (obj?.Value != null)
                    return TryResolveInteger(obj.Value, out result);
            }

            return false;
        }

        internal void SetReaderActive()
        {
            // Найти и прочитать каталог (/Root) — без этого нет доступа к /OCProperties
            var rootObj = TrailerSubDict("/Root");
            rootObj.ReadObject();
            Catalog = rootObj;
            if (Catalog == null)
                throw new ApplicationException("Catalog (/Root) is missing");

            //  Инициализировать список скрытых OCG
            InitHiddenLayers(rootObj.Dictionary);

            // Прочитать **все** объекты (без потоков)
            foreach (var kv in ObjectTable)
                foreach (var obj in kv.Value)
                    if (obj.FilePosition != 0)
                        obj.ReadObject();

            // Посчитать длины всех потоков
            foreach (var kv in ObjectTable)
                foreach (var obj in kv.Value)
                    if (obj.ObjectType == ObjectType.Stream)
                        obj.GetStreamLength();

            // Расшифровать строки (если файл защищён)
            if (DecryptionStatus != DecryptionStatus.FileNotProtected)
            {
                var ctrl = new DecryptCtrl(Encryption, 0, EncryptionDict);
                foreach (var kv in ObjectTable)
                    foreach (var obj in kv.Value)
                    {
                        if (obj.ParentObjectNo != 0) continue;
                        ctrl.ObjectNumber = obj.ObjectNumber;
                        if (obj.ObjectType == ObjectType.Dictionary || obj.ObjectType == ObjectType.Stream)
                            obj.Dictionary.DecryptStrings(ctrl);
                        else if (obj.ObjectType == ObjectType.Other)
                            obj.Value.DecryptStrings(ctrl);
                    }
            }

            // --- 5) Обработать object streams
            if (ObjStmArray != null)
                foreach (int objNo in ObjStmArray)
                {
                    var versions = ObjectTable[objNo];
                    var objStream = versions.OrderBy(o => o.Generation).FirstOrDefault()
                                   ?? throw new ApplicationException("Object stream is missing");
                    objStream.ProcessObjectsStream();
                }

            Catalog = TrailerSubDict("/Root");
            if (Catalog == null)
                throw new ApplicationException("Catalog (/Root) is missing");

            Active = true;
        }

        /// <summary>
        /// Находит в Trailer ссылку на подпоследовательность (/Root или /Info),
        /// возвращает именно тот объект, который соответствует поколению ссылки.
        /// </summary>
        public PdfIndirectObject TrailerSubDict(string Key)
        {
            // ищем значение в TrailerDict
            PdfBase baseVal = TrailerDict.FindValue(Key);
            if (!baseVal.IsReference)
                return null;

            // приводим к PdfReference (с полем Generation)
            var reference = (PdfReference)baseVal;

            // получаем нужную версию объекта
            var sub = ToPdfIndirectObject(reference);
            if (sub == null) // || sub.ObjectType != ObjectType.Dictionary)
                throw new ApplicationException("Catalog (/Root) object not found");

            return sub;
        }

        /// <summary>
        /// Find array of objects (Kids and Contents)
        /// </summary>
        internal PdfIndirectObject[] GetKidsArray(PdfDictionary Dict)
        {
            // get dictionary pair
            PdfBase KidsValue = Dict.FindValue("/Kids");

            // Kids value is a reference
            if (KidsValue.IsReference)
            {
                // get the object pointed by the reference
                PdfIndirectObject KidsObj = ToPdfIndirectObject((PdfReference)KidsValue);

                // the indirect object must be an array
                if (KidsObj == null || KidsObj.ObjectType != ObjectType.Other || !KidsObj.Value.IsArray) 
                    return null;

                // replace KidsValue with array object
                KidsValue = KidsObj.Value;

                // replace /Kids with direct array object
                Dict.AddKeyValue("/Kids", KidsValue);
            }
            else if (!KidsValue.IsArray) // Kids value must be an array or a reference
                return null;

            // array items
            PdfBase[] ReferenceArray = ((PdfArray)KidsValue).ArrayItems;

            // create result array
            PdfIndirectObject[] ResultArray = new PdfIndirectObject[ReferenceArray.Length];

            // loop for all entries
            for (int Index = 0; Index < ReferenceArray.Length; Index++)
            {
                // make sure we have a reference
                if (!ReferenceArray[Index].IsReference) 
                    return null;

                // find page or pages object
                PdfIndirectObject PageObj = ToPdfIndirectObject((PdfReference)ReferenceArray[Index]);

                // all values in reference array must be page or pages
                if (PageObj == null || PageObj.ObjectType != ObjectType.Dictionary ||
                    PageObj.PdfObjectType != "/Page" && PageObj.PdfObjectType != "/Pages") 
                    return null;

                // save page object
                ResultArray[Index] = PageObj;
            }

            return ResultArray;
        }

        public PdfOp[] ParseContents(byte[] Contents)
        {
            // create parse contents object and read first character
            PdfByteArrayParser PC = new PdfByteArrayParser(this, Contents, true);
            PC.ReadFirstChar();

            List<PdfOp> OpArray = new List<PdfOp>();
            List<PdfBase> ArgStack = new List<PdfBase>();

            // loop for tokens
            while(true)
            {
                // get next token
                PdfBase Token = PC.ParseNextItem();
                // end of contents
                if (Token.IsEmpty) 
                    break;
                // operator
                if (Token.IsOperator)
                {
                    PdfOp Op = (PdfOp)Token;
                    if (Op.OpValue != Operator.BeginInlineImage) 
                        Op.ArgumentArray = ArgStack.ToArray();

                    OpArray.Add(Op);
                    ArgStack.Clear();
                    continue;
                }

                // save argument
                ArgStack.Add(Token);
            }

            if (ArgStack.Count != 0) 
                throw new ApplicationException("Parse contents stream invalid termination");

            return OpArray.ToArray();
        }

        private void ValidateFile()
        {
            // we do not want to deal with very long files
            if (PdfBinaryReader.BaseStream.Length > 0x40000000) 
                throw new ApplicationException("File too big (Max allowed 1GB)");

            // file must have at least 32 byte
            if (PdfBinaryReader.BaseStream.Length < 32) 
                throw new ApplicationException("File too small to be a PDF document");

            // get file signature at start of file the pdf revision number
            int BufSize = PdfBinaryReader.BaseStream.Length > 1024 ? 1024 : (int)PdfBinaryReader.BaseStream.Length;
            byte[] Buffer = new byte[BufSize];
            PdfBinaryReader.Read(Buffer, 0, Buffer.Length);

            // skip white space
            int Ptr = 0;
            while (PdfBase.IsWhiteSpace(Buffer[Ptr])) Ptr++;

            // save start of file
            StartPosition = Ptr;

            // validate signature
            if (Buffer[Ptr + 0] != '%' || Buffer[Ptr + 1] != 'P' || Buffer[Ptr + 2] != 'D' ||
                Buffer[Ptr + 3] != 'F' || Buffer[Ptr + 4] != '-' || Buffer[Ptr + 5] != '1' ||
                Buffer[Ptr + 6] != '.' || (Buffer[Ptr + 7] < '0' && Buffer[Ptr + 7] > '7'))
                throw new ApplicationException("Invalid PDF file (bad signature: must be %PDF-1.x)");

            // get file signature at end of file %%EOF
            PdfBinaryReader.BaseStream.Position = PdfBinaryReader.BaseStream.Length - Buffer.Length;
            PdfBinaryReader.Read(Buffer, 0, Buffer.Length);

            // loop in case of extra text after the %%EOF
            Ptr = Buffer.Length - 1;
            while(true)
            {
                // look for last F
                for (; Ptr > 32 && Buffer[Ptr] != 'F'; Ptr--) ;
                if (Ptr == 32) 
                    throw new ApplicationException("Invalid PDF file (Missing %%EOF at end of the file)");

                // match signature
                if ((Buffer[Ptr - 5] == '\n' || Buffer[Ptr - 5] == '\r') && Buffer[Ptr - 4] == '%' &&
                    Buffer[Ptr - 3] == '%' && Buffer[Ptr - 2] == 'E' && Buffer[Ptr - 1] == 'O') 
                    break;

                // move pointer back
                Ptr--;
            }

            // set pointer to one character before %%EOF
            Ptr -= 6;

            // remove leading white space (space and eol)
            while (PdfBase.IsWhiteSpace(Buffer[Ptr])) { Ptr--; }

            // get start of cross reference position
            int XRefPos = 0;
            int Power = 1;
            for (; char.IsDigit((char)Buffer[Ptr]); Ptr--)
            {
                XRefPos += Power * (Buffer[Ptr] - '0');
                Power *= 10;
            }

            // remove leading white space (space and eol)
            while (PdfBase.IsWhiteSpace(Buffer[Ptr])) { Ptr--; }

            // verify startxref
            if (Buffer[Ptr - 8] != 's' || Buffer[Ptr - 7] != 't' || Buffer[Ptr - 6] != 'a' ||
                Buffer[Ptr - 5] != 'r' || Buffer[Ptr - 4] != 't' || Buffer[Ptr - 3] != 'x' ||
                Buffer[Ptr - 2] != 'r' || Buffer[Ptr - 1] != 'e' || Buffer[Ptr] != 'f')
                throw new ApplicationException("Missing startxref at end of the file");

            // set file position to cross reference table
            SetFilePosition(XRefPos);

            // read next character
            ParseFile.ReadFirstChar();

            // there are two possible cross reference cases xref table or xref stream old style
            // cross reference table
            if (ParseFile.ParseNextItem().ToKeyWord == KeyWord.XRef)
            {
                // set hybrid file
                TableCrossReference = true;

                // loop forward to find the trailer dictionary
                while(true)
                {
                    // get next object
                    PdfBase Token = ParseFile.ParseNextItem();

                    // test for trailer
                    if (Token.ToKeyWord == KeyWord.Trailer) 
                        break;

                    // read object number and ignore it
                    if (!Token.IsInteger) 
                        throw new ApplicationException("Cross reference Table error");

                    // read object count (can be zero)
                    if (!ParseFile.ParseNextItem().GetInteger(out int ObjectCount)) 
                        throw new ApplicationException("Cross reference Table error");
                    if (ObjectCount == 0) 
                        continue;

                    // skip white space
                    ParseFile.SkipWhiteSpace();

                    // skip forward 20 * ObjectCount
                    ParseFile.SkipPos(20 * ObjectCount - 1);
                    ParseFile.ReadFirstChar();
                }

                // read trailer dictionary
                TrailerDict = ParseFile.ParseNextItem().ToDictionary;
                if (TrailerDict == null) 
                    throw new ApplicationException("Missing table trailer dictionary");

                // search for /Size size is the largest object number plus 1
                if (!TrailerDict.FindValue("/Size").GetInteger(out int Size) || Size == 0) 
                    throw new ApplicationException("Table trailer dictionary error");

                // create initial object array
                InitObjectTable(Size);
            }

            // loop back in time for cross reference tables or streams
            while(true)
            {
                // set file position to cross reference table
                SetFilePosition(XRefPos);

                // read next character
                ParseFile.ReadFirstChar();

                // old style cross reference table
                if (ParseFile.ParseNextItem().ToKeyWord == KeyWord.XRef)
                {
                    // read cross reference table and create empty objects
                    XRefPos = ReadXrefTable();
                    if (XRefPos == 0) break;
                }
                // new style cross reference stream
                else
                {
                    // read xref stream
                    XRefPos = ReadXRefStream(XRefPos);
                    if (XRefPos == 0) break;
                }
            }

            // exit
            return;
        }

        private void InitHiddenLayers(PdfDictionary rootDict)
        {
            HiddenOCGs = new HashSet<int>();

            if (rootDict == null)
                return;

            // 1) Ищем /OCProperties — может быть либо словарём, либо ссылкой на словарь
            PdfBase ocPropsBase = rootDict.FindValue("/OCProperties");
            if (ocPropsBase.IsEmpty)
                return;

            // 2) если это указатель — разыменуем его в PdfIndirectObject и обязательно прочитаем
            PdfDictionary ocPropsDict = null;
            if (ocPropsBase.IsDictionary)
            {
                ocPropsDict = ocPropsBase.ToDictionary;
            }
            else if (ocPropsBase.IsReference)
            {
                var refObj = (PdfReference)ocPropsBase;
                var ind = ToPdfIndirectObject(refObj);
                if (ind != null)
                {
                    // если ещё не прочитали содержимое словаря — сделаем это
                    if (ind.ObjectType != ObjectType.Dictionary || ind.Dictionary == null)
                        ind.ReadObject();
                    // теперь сможем взять Dictionary
                    ocPropsDict = ind.Dictionary;
                }
            }

            // 3) если всё равно нет — выходим
            if (ocPropsDict == null)
                return;

            // 4) Из OCProperties берём Default-конфигурацию /D
            PdfBase defaultBase = ocPropsDict.FindValue("/D");
            PdfDictionary defaultDict = null;
            if (defaultBase.IsDictionary)
            {
                defaultDict = defaultBase.ToDictionary;
            }
            else if (defaultBase.IsReference)
            {
                defaultDict = ToPdfDictionary(defaultBase);
            }
            if (defaultDict == null)
                return;

            // 5) Забираем массив ссылок на выключенные OCG
            PdfBase[] offItems = defaultDict.FindValue("/OFF").ToArrayItems;
            if (offItems == null)
                return;

            foreach (var item in offItems)
            {
                if (item.IsReference)
                {
                    int ocNo = ((PdfReference)item).ObjectNumber;
                    HiddenOCGs.Add(ocNo);
                }
            }
        }

        // read old type cross reference tabl
        internal int ReadXrefTable()
        {
            // 1) Читаем блоки xref до Trailer
            while (true)
            {
                PdfBase token = ParseFile.ParseNextItem();

                if (token.ToKeyWord == KeyWord.Trailer)
                    break;

                if (!token.GetInteger(out int firstObjNo))
                    throw new ApplicationException("Cross reference Table error");

                if (!ParseFile.ParseNextItem().GetInteger(out int objCount))
                    throw new ApplicationException("Cross reference Table error");

                for (int i = 0; i < objCount; i++)
                {
                    var posToken = ParseFile.ParseNextItem();
                    var genToken = ParseFile.ParseNextItem();
                    var statusToken = ParseFile.ParseNextItem();

                    if (!posToken.GetInteger(out int pos))
                        throw new ApplicationException($"Invalid xref position: {posToken.TypeToString()}");

                    if (!genToken.GetInteger(out int gen))
                        throw new ApplicationException($"Invalid generation number: {genToken.TypeToString()} ({genToken})");

                    KeyWord status = statusToken.ToKeyWord;
                    if (status != KeyWord.N && status != KeyWord.F)
                        throw new ApplicationException($"Invalid xref status: {statusToken}");

                    if (status == KeyWord.F) continue;

                    int objNo = firstObjNo + i;

                    // создаём объект с учётом поколения
                    var obj = new PdfIndirectObject(this, objNo, pos)
                    {
                        Generation = gen,
                        FilePosition = pos
                    };

                    // сохраняем в ObjectTable
                    if (!ObjectTable.TryGetValue(objNo, out var list))
                    {
                        list = new List<PdfIndirectObject>();
                        ObjectTable[objNo] = list;
                    }
                    list.Add(obj);
                }
            }

            // 2) Trailer dictionary
            var trDict = ParseFile.ParseNextItem().ToDictionary;
            if (trDict == null)
                throw new ApplicationException("Cross reference table missing trailer dictionary");

            // 3) Обработка /XRefStm
            if (trDict.FindValue("/XRefStm").GetInteger(out int xRefStmPos))
            {
                SetFilePosition(xRefStmPos);
                ParseFile.ReadFirstChar();
                int r = ReadXRefStream(xRefStmPos);
                if (r != 0)
                    throw new ApplicationException("/XRefStm logic error");
            }

            // 4) Ссылка на предыдущий xref
            if (trDict.FindValue("/Prev").GetInteger(out int prev))
                return prev;

            return 0;
        }

        // Read cross reference stream
        internal int ReadXRefStream(int XRefPos)
        {
            // 1) Устанавливаем позицию и читаем заголовок объекта xref-stream
            SetFilePosition(XRefPos);
            ParseFile.ReadFirstChar();
            int XRefObjNo = ParseFile.ParseObjectRefNo();
            if (XRefObjNo <= 0)
                throw new ApplicationException("Cross reference stream error");

            // 2) Считываем сам объект xref-stream
            var XRefObj = new PdfIndirectObject(this, XRefObjNo, XRefPos);
            XRefObj.ReadObject();
            if (XRefObj.ObjectType != ObjectType.Stream)
                throw new ApplicationException("Cross reference stream error");

            // 3) Если это первый xref, инициализируем TrailerDict и ObjectTable
            if (TrailerDict == null)
            {
                TrailerDict = XRefObj.Dictionary;
                if (!TrailerDict.FindValue("/Size").GetInteger(out int size) || size == 0)
                    throw new ApplicationException("Cross reference stream error");
                InitObjectTable(size);
            }

            // 4) Получаем длину потока и параметры
            XRefObj.GetStreamLength();
            var indexArray = XRefObj.Dictionary.FindValue("/Index").ToArrayItems;
            if (indexArray == null)
            {
                indexArray = new PdfBase[] {
            new PdfInteger(0),
            XRefObj.Dictionary.FindValue("/Size")
        };
            }
            var w = XRefObj.Dictionary.FindValue("/W").ToArrayItems;
            if (w == null || w.Length != 3)
                throw new ApplicationException("XRef object missing W array");
            int w1 = ((PdfInteger)w[0]).IntValue,
                w2 = ((PdfInteger)w[1]).IntValue,
                w3 = ((PdfInteger)w[2]).IntValue;

            // 5) Читаем и раскомпрессируем данные
            byte[] data = XRefObj.ReadStream();
            data = XRefObj.DecompressStream(data);

            // 6) Разбираем записи
            var objStmParents = new List<int>();
            int ptr = 0;
            for (int block = 0; block < indexArray.Length; block += 2)
            {
                int objNo = ((PdfInteger)indexArray[block]).IntValue;
                int count = ((PdfInteger)indexArray[block + 1]).IntValue;

                for (int i = 0; i < count; i++, objNo++)
                {
                    int type = GetField(data, ptr, w1); ptr += w1;
                    int field2 = GetField(data, ptr, w2); ptr += w2;
                    int field3 = GetField(data, ptr, w3); ptr += w3;

                    switch (type)
                    {
                        case 0:
                            // deleted — игнорируем
                            break;

                        case 1:
                            // нормальный объект n g R
                            var obj = new PdfIndirectObject(this, objNo, field2)
                            {
                                Generation = field3,
                                FilePosition = field2
                            };
                            if (!ObjectTable.TryGetValue(objNo, out var list1))
                            {
                                list1 = new List<PdfIndirectObject>();
                                ObjectTable[objNo] = list1;
                            }
                            list1.Add(obj);
                            break;

                        case 2:
                            // объект из object stream
                            var objStream = new PdfIndirectObject(this, objNo, field2, field3)
                            {
                                Generation = 0,
                                FilePosition = XRefObj.StreamFilePosition
                            };
                            if (!ObjectTable.TryGetValue(objNo, out var list2))
                            {
                                list2 = new List<PdfIndirectObject>();
                                ObjectTable[objNo] = list2;
                            }
                            list2.Add(objStream);

                            // запомним parent-stream, чтобы ProcessObjectsStream знал, что его обрабатывать
                            if (!objStmParents.Contains(field2))
                                objStmParents.Add(field2);
                            break;

                        default:
                            throw new ApplicationException("Cross reference stream error");
                    }
                }
            }

            // 7) Обработка object streams
            if (objStmParents.Count > 0)
            {
                ObjStmArray = objStmParents.ToArray();
            }

            // 8) Ищем ссылку на предыдущий xref
            if (XRefObj.Dictionary.FindValue("/Prev").GetInteger(out int prev))
                return prev;
            return 0;
        }

        // Get cross reference stream object field
        internal static int GetField( byte[] Contents, int Pos, int Len)
        {
            int Val = 0;
            for (; Len > 0; Pos++, Len--) 
                Val = 256 * Val + Contents[Pos];
            return Val;
        }

        public void Dispose()
        {
            if (PdfBinaryReader != null)
            {
                PdfBinaryReader.Close();
                PdfBinaryReader = null;
            }
            if (Encryption != null)
            {
                Encryption.Dispose();
                Encryption = null;
            }
            GC.SuppressFinalize(this);
        }
    }
}