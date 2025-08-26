using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PdfFileAnalyzer.Crypto;
using PdfFileAnalyzer.Element;

namespace PdfFileAnalyzer
{
    /// <summary>
    /// Контейнер для ресурсов и содержимого отдельной страницы.
    /// </summary>
    public class PageContext
    {
        public PdfDictionary Resources { get; set; }
        public byte[] Content { get; set; }
    }

    public class PdfDocument : IDisposable
    {
        // Путь к файлу и его основные характеристики
        public string FilePath { get; private set; }
        public string FileName => Path.GetFileName(FilePath);
        public long FileSize { get; private set; }
        public string PdfVersion { get; private set; }

        // «Нижнеуровневый» PdfReader
        public PdfReader Reader;

        // Кэш массива страниц
        private PdfIndirectObject[] _pages;

        // Флаги и словари
        public bool IsLoaded => Reader?.Active ?? false;
        public int PageCount => _pages?.Length ?? 0;
        public PdfIndirectObject[] Pages => _pages;
        public DecryptionStatus EncryptionStatus => Reader?.DecryptionStatus ?? DecryptionStatus.Unsupported;
        public CryptoEngine CryptoEngine => Reader?.Encryption;
        public PdfDictionary Trailer => Reader?.TrailerDict;
        public PdfDictionary Catalog => Reader?.Catalog?.Dictionary;
        public PdfDictionary Metadata => Reader?.TrailerSubDict("/Info")?.Dictionary;

        public PdfDocument(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) 
                throw new ArgumentException("Не указан путь к PDF", nameof(filePath));
            FilePath = filePath;
            FileSize = new FileInfo(filePath).Length;
        }

        /// <summary>
        /// Открывает и инициализирует PDF. Возвращает true, если успешно.
        /// </summary>
        public bool Load(string password = null)
        {
            Reader = new PdfReader();
            bool ok = Reader.OpenPdfFile(FilePath, password);
            if (!ok) return false;

            PdfVersion = ReadPdfVersion();
            InitializePages();
            return true;
        }

        /// <summary>
        /// Собирает массив страниц и их содержимое.
        /// </summary>
        private void InitializePages()
        {
            var pagesRoot = FindPagesObject();
            _pages = BuildPageArray(pagesRoot);

            // построить content streams
            foreach (var page in _pages)
            {
                page.BuildContentsArray();
            }
        }

        private PdfIndirectObject FindPagesObject()
        {
            // /Root из trailer
            var catalog = Reader.Catalog?.Dictionary;
            if (catalog == null)
                throw new ApplicationException("Catalog (/Root) не найден.");

            // Ищем ключ /Pages -> Reference
            PdfBase pagesBase = catalog.FindValue("/Pages");
            if (!pagesBase.IsReference)
                throw new ApplicationException("Catalog не содержит /Pages как ссылку.");

            return Reader.ToPdfIndirectObject((PdfReference)pagesBase);
        }

        private PdfIndirectObject[] BuildPageArray(PdfIndirectObject root)
        {
            var result = new List<PdfIndirectObject>();

            void Traverse(PdfIndirectObject obj, Dictionary<string, PdfBase> inherited)
            {
                if (obj.ObjectType != ObjectType.Dictionary || obj.Dictionary == null)
                    return;

                string type = obj.Dictionary.FindValue("/Type").ToName;
                if (type == "/Pages")
                {
                    // Наследование: создаём новую копию словаря
                    var inheritedThis = new Dictionary<string, PdfBase>(inherited);
                    foreach (var key in new[] { "/Resources", "/MediaBox", "/CropBox", "/Rotate", "/Annots", "/Contents" })
                    {
                        var value = obj.Dictionary.FindValue(key);
                        if (!value.IsEmpty)
                            inheritedThis[key] = value;
                    }

                    // Получаем детей
                    PdfArray kidsArray = Reader.ToPdfArray(obj.Dictionary, "/Kids");
                    if (kidsArray == null)
                        throw new ApplicationException("Pages object без /Kids.");

                    foreach (var kid in kidsArray.ToArrayItems)
                    {
                        if (!kid.IsReference) continue;
                        var child = Reader.ToPdfIndirectObject((PdfReference)kid);
                        if (child != null)
                            Traverse(child, inheritedThis);
                    }
                }
                else if (type == "/Page")
                {
                    // Подставляем унаследованные поля, если они не заданы
                    foreach (var key in new[] { "/Resources", "/MediaBox", "/CropBox", "/Rotate", "/Annots", "/Contents" })
                    {
                        if (!obj.Dictionary.Exists(key) && inherited.TryGetValue(key, out var inheritedVal))
                        {
                            obj.Dictionary.AddKeyValue(key, inheritedVal);
                        }
                    }

                    // Проверка обязательных полей
                    if (!obj.Dictionary.Exists("/MediaBox"))
                        throw new ApplicationException("Page без /MediaBox и без наследования — ошибка по PDF спецификации");

                    result.Add(obj);
                }
            }

            Traverse(root, new Dictionary<string, PdfBase>());
            return result.ToArray();
        }

        /// <summary>
        /// Возвращает PdfIndirectObject для страницы с заданным 0‑based индексом.
        /// </summary>
        public PdfIndirectObject GetPage(int index)
        {
            if (_pages == null) 
                throw new InvalidOperationException("Страницы не инициализированы.");
            if (index < 0 || index >= _pages.Length) 
                throw new ArgumentOutOfRangeException(nameof(index));
            return _pages[index];
        }

        /// <summary>
        /// Возвращает уже распакованный и расшифрованный контент страницы.
        /// </summary>
        public byte[] GetPageContent(int index)
        {
            var page = GetPage(index);
            page.BuildContentsArray();
            return page.PageContents();
        }

        /// <summary>
        /// Возвращает словарь ресурсов (/Resources) страницы.
        /// </summary>
        public PdfDictionary GetPageResources(int index)
        {
            var page = GetPage(index);
            return Reader.ToPdfDictionary(page.Dictionary, "/Resources");
        }

        /// <summary>
        /// Комбинированный метод: возвращает ресурсы и контент страницы.
        /// </summary>
        public PageContext GetPageContext(int index)
        {
            return new PageContext
            {
                Resources = GetPageResources(index),
                Content = GetPageContent(index)
            };
        }

        /// <summary>
        /// Извлекает значение из словаря /Info (например, "/Author", "/Title").
        /// </summary>
        public string GetMetadataValue(string key)
        {
            return Metadata?.FindValue(key)?.ToString();
        }

        /// <summary>
        /// Извлекает XMP‑метаданные, если они есть в PDF.
        /// </summary>
        public Dictionary<string, object> ExtractXmpMetadata()
        {
            string text = File.ReadAllText(FilePath);
            var m = Regex.Match(text, @"<\?xpacket.*?\?>.*?</x:xmpmeta>", RegexOptions.Singleline);
            if (!m.Success) return null;

            string xml = m.Value;
            int p = xml.IndexOf("<x:xmpmeta", StringComparison.Ordinal);
            if (p >= 0) xml = xml.Substring(p);

            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root == null) return null;

            var metadata = new Dictionary<string, object>();

            void ParseElement(XElement el, Dictionary<string, object> cur)
            {
                string tag = el.Name.ToString().ToLower();
                if (tag.StartsWith("x:") || (tag.StartsWith("rdf:") && tag != "rdf:li")) return;

                if (el.HasElements)
                {
                    if (tag == "rdf:li")
                    {
                        if (!cur.ContainsKey(tag)) cur[tag] = new List<object>();
                        var list = (List<object>)cur[tag];
                        var item = new Dictionary<string, object>();
                        list.Add(item);
                        foreach (var child in el.Elements()) ParseElement(child, item);
                    }
                    else
                    {
                        var childDict = new Dictionary<string, object>();
                        cur[tag] = childDict;
                        foreach (var child in el.Elements()) ParseElement(child, childDict);
                    }
                }
                else
                {
                    cur[tag] = el.Value;
                }
            }

            foreach (var el in root.Elements())
                ParseElement(el, metadata);

            if (metadata.TryGetValue("dc:format", out object fmt) &&
                fmt is string fmtStr && fmtStr != "application/pdf")
            {
                return null;
            }

            return metadata;
        }

        /// <summary>
        /// Читает первые 10 символов файла и возвращает "%PDF-1.x".
        /// </summary>
        private string ReadPdfVersion()
        {
            using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
            using (var r = new StreamReader(fs))
            {
                char[] buf = new char[10];
                r.Read(buf, 0, buf.Length);
                string hdr = new string(buf);
                int i = hdr.IndexOf("%PDF-", StringComparison.Ordinal);
                return (i >= 0 && hdr.Length >= i + 8)
                    ? hdr.Substring(i + 5, 3)
                    : "Unknown";
            }
        }

        public void Dispose()
        {
            Reader?.Dispose();
            Reader = null;
        }
    }
}
