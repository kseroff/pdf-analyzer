using System;

namespace PdfFileAnalyzer
{
    public static class Adler32
    {
        public static uint Checksum(byte[] buffer)
        {
            const uint Adler32Base = 65521;

            // Состояние для расчёта контрольной суммы
            uint AdlerLow = 1; // Младшая часть
            uint AdlerHigh = 0; // Старшая часть

            // Определяем размер и позицию
            int len = buffer.Length;
            int pos = 0;

            // Параллельная обработка блоков
            while (len > 0)
            {
                // Обрабатываем блоки данных
                int n = len < 5552 ? len : 5552; // Ограничиваем блоки максимальным размером
                len -= n;

                // Обрабатываем байты в блоках
                for (int i = 0; i < n; i++)
                {
                    AdlerLow += buffer[pos++];
                    AdlerHigh += AdlerLow;
                }

                // Модуль для предотвращения переполнения
                AdlerLow %= Adler32Base;
                AdlerHigh %= Adler32Base;
            }

            // Объединяем старшую и младшую части для получения контрольной суммы
            return (AdlerHigh << 16) | AdlerLow;
        }
    }
}
