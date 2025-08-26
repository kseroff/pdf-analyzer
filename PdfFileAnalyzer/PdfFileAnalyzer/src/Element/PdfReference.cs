namespace PdfFileAnalyzer.Element
{
    public class PdfReference : PdfBase
    {
        /// <summary>
        /// Номер объекта (n).
        /// </summary>
        public int ObjectNumber { get; }

        /// <summary>
        /// Номер поколения (g).
        /// </summary>
        public int Generation { get; }

        /// <summary>
        /// Создаёт ссылку на объект n g R. Если поколение не указано, по умолчанию 0.
        /// </summary>
        public PdfReference(int objectNumber, int generation = 0)
        {
            ObjectNumber = objectNumber;
            Generation = generation;
        }

        /// <summary>
        /// Конвертирует в нотацию «n g R».
        /// </summary>
        public override string ToString()
        {
            return $"{ObjectNumber} {Generation} R";
        }

        public override string TypeToString()
        {
            return "Reference";
        }
    }
}
