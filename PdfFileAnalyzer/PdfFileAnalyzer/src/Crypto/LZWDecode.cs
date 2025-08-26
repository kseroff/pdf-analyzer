using System;
using System.Collections.Generic;

namespace PdfFileAnalyzer.Crypto
{
    public static class LZW
    {
        private const int ResetDictionary = 256;
        private const int EndOfStream = 257;

        // Декодирование LZW
        public static byte[] Decode(byte[] readBuffer)
        {
            // Инициализируем словарь
            var dictionary = new List<byte[]>();  // Используем List<byte[]> для словаря
            for (int i = 0; i < 256; i++)
                dictionary.Add(new byte[] { (byte)i });

            // Буфер для выхода
            var writeBuffer = new List<byte>();

            int readPtr = 0;
            int bitBuffer = 0;
            int bitCount = 0;
            int dictionaryPtr = 258;
            int codeLength = 9;
            int codeMask = 511; // Маска для 9 бит
            int oldCode = -1;

            // Работаем с потоком данных
            while (true)
            {
                // Наполняем буфер битов до нужного размера (17-24 бита)
                while (bitCount <= 16 && readPtr < readBuffer.Length)
                {
                    bitBuffer = (bitBuffer << 8) | readBuffer[readPtr++];
                    bitCount += 8;
                }

                if (bitCount < codeLength) break;

                // Получаем следующий код
                int code = (bitBuffer >> (bitCount - codeLength)) & codeMask;
                bitCount -= codeLength;

                // Проверка на конец потока
                if (code == EndOfStream) break;

                // Если код специальный (reset), сбрасываем словарь
                if (code == ResetDictionary)
                {
                    dictionaryPtr = 258;
                    codeLength = 9;
                    codeMask = 511;
                    oldCode = -1;
                    continue;
                }

                // Новый код
                byte[] addToOutput;

                if (code < dictionaryPtr)  // код есть в словаре
                {
                    addToOutput = dictionary[code]; // Извлекаем строку из словаря

                    // Если код найден первый раз после сброса
                    if (oldCode < 0)
                    {
                        writeBuffer.AddRange(addToOutput);
                        oldCode = code;
                        continue;
                    }

                    // Добавляем новый элемент в словарь
                    var newString = BuildString(dictionary[oldCode], addToOutput[0]);
                    dictionary.Add(newString); // Добавляем строку в словарь
                    dictionaryPtr++;
                }
                else if (code == dictionaryPtr) // Специальный случай, когда код совпадает с предыдущим
                {
                    addToOutput = dictionary[oldCode]; // Повторяем строку
                    addToOutput = BuildString(addToOutput, addToOutput[0]);
                    dictionary.Add(addToOutput); // Добавляем строку в словарь
                    dictionaryPtr++;
                }
                else
                {
                    throw new ApplicationException("LZWDecode: Ошибка в кодах.");
                }

                // Добавляем данные в выходной буфер
                writeBuffer.AddRange(addToOutput);

                // Запоминаем код
                oldCode = code;

                // Увеличиваем длину кода по мере роста словаря
                if (dictionaryPtr == 511 || dictionaryPtr == 1023 || dictionaryPtr == 2047)
                {
                    codeLength++;
                    codeMask = (codeMask << 1) + 1;
                }
            }

            return writeBuffer.ToArray(); // Преобразуем List<byte> в byte[]
        }

        // Восстановление строки
        private static byte[] BuildString(byte[] oldString, byte addedByte)
        {
            int length = oldString.Length;
            byte[] newString = new byte[length + 1];
            Array.Copy(oldString, 0, newString, 0, length);
            newString[length] = addedByte;
            return newString;
        }
    }
}
