using PdfFileAnalyzer;
using PdfFileAnalyzer.Element;
using System.Linq;
using System.Text;

namespace PdfFileAnalyzer.XObject
{
    /// <summary>
    /// Для /XObject /Form – просто парсим его контент-поток и выдаём список операторов.
    /// </summary>
    public class PdfFormXObject : PdfXObject
    {
        public PdfFormXObject(PdfIndirectObject obj, PdfReader reader)
            : base(obj, reader) { }

        /// <summary>
        /// Парсим содержимое формы как поток команд PDF.
        /// </summary>
        public PdfOp[] GetOperators()
        {
            // 1) Читаем и распаковываем
            byte[] data = _obj.ReadStream();
            data = _obj.DecompressStream(data);

            // 2) Парсим
            return _reader.ParseContents(data);
        }

        public override string Summary()
        {
            var sb = new StringBuilder(base.Summary());
            sb.AppendLine("Form XObject operators:");
            foreach (var op in GetOperators())
            {
                sb.AppendLine($"  {op.OpValue} ({string.Join(", ", op.ArgumentArray.Select(a => a.ToText))})");
            }
            return sb.ToString();
        }
    }
}
