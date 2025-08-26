using PdfFileAnalyzer;
using PdfFileAnalyzer.Element;
using System.Text;

namespace PdfFileAnalyzer.XObject
{
    /// <summary>
    /// Базовый класс для любого XObject (/XObject).
    /// Реализует фабричный метод Create, который по субтипу возвращает нужный подкласс.
    /// </summary>
    public abstract class PdfXObject
    {
        public readonly PdfIndirectObject _obj;
        public readonly PdfReader _reader;
        public readonly PdfDictionary _dict;

        protected PdfXObject(PdfIndirectObject obj, PdfReader reader)
        {
            _obj = obj;
            _reader = reader;
            _dict = obj.Dictionary;
        }

        /// <summary>
        /// Субтип XObject ("/Image", "/Form", ...).
        /// </summary>
        public string Subtype => _dict.FindValue("/Subtype").ToName;

        /// <summary>
        /// Фабрика: смотрим /Subtype и возвращаем нужный класс.
        /// </summary>
        public static PdfXObject Create(PdfIndirectObject obj, PdfReader reader)
        {
            if (obj.PdfObjectType != "/XObject")
                return null;

            var subtype = obj.Dictionary.FindValue("/Subtype").ToName;
            switch (subtype)
            {
                case "/Image":
                    return new PdfImageXObject(obj, reader);
                case "/Form":
                    return new PdfFormXObject(obj, reader);
                default:
                    return new GenericPdfXObject(obj, reader);
            }
        }

        /// <summary>
        /// Общая информация по XObject: словарь, BBox, Matrix и т.д.
        /// </summary>
        public virtual string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"XObject {Subtype} (obj #{_obj.ObjectNumber})");
            foreach (var kv in _dict.KeyValueArray)
            {
                sb.AppendLine($"{kv.Key} = {kv.Value.ToText}");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Для неподдерживаемых субтипов рисуем просто summary.
    /// </summary>
    class GenericPdfXObject : PdfXObject
    {
        public GenericPdfXObject(PdfIndirectObject obj, PdfReader reader)
            : base(obj, reader) { }
    }
}
