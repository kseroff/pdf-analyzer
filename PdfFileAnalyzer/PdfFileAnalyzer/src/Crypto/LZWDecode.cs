using System;
using System.Collections.Generic;

namespace PdfFileAnalyzer.Crypto
{
    public static class LZW
    {
        private const int ResetDictionary = 256;
        private const int EndOfStream = 257;

        // ������������� LZW
        public static byte[] Decode(byte[] readBuffer)
        {
            // �������������� �������
            var dictionary = new List<byte[]>();  // ���������� List<byte[]> ��� �������
            for (int i = 0; i < 256; i++)
                dictionary.Add(new byte[] { (byte)i });

            // ����� ��� ������
            var writeBuffer = new List<byte>();

            int readPtr = 0;
            int bitBuffer = 0;
            int bitCount = 0;
            int dictionaryPtr = 258;
            int codeLength = 9;
            int codeMask = 511; // ����� ��� 9 ���
            int oldCode = -1;

            // �������� � ������� ������
            while (true)
            {
                // ��������� ����� ����� �� ������� ������� (17-24 ����)
                while (bitCount <= 16 && readPtr < readBuffer.Length)
                {
                    bitBuffer = (bitBuffer << 8) | readBuffer[readPtr++];
                    bitCount += 8;
                }

                if (bitCount < codeLength) break;

                // �������� ��������� ���
                int code = (bitBuffer >> (bitCount - codeLength)) & codeMask;
                bitCount -= codeLength;

                // �������� �� ����� ������
                if (code == EndOfStream) break;

                // ���� ��� ����������� (reset), ���������� �������
                if (code == ResetDictionary)
                {
                    dictionaryPtr = 258;
                    codeLength = 9;
                    codeMask = 511;
                    oldCode = -1;
                    continue;
                }

                // ����� ���
                byte[] addToOutput;

                if (code < dictionaryPtr)  // ��� ���� � �������
                {
                    addToOutput = dictionary[code]; // ��������� ������ �� �������

                    // ���� ��� ������ ������ ��� ����� ������
                    if (oldCode < 0)
                    {
                        writeBuffer.AddRange(addToOutput);
                        oldCode = code;
                        continue;
                    }

                    // ��������� ����� ������� � �������
                    var newString = BuildString(dictionary[oldCode], addToOutput[0]);
                    dictionary.Add(newString); // ��������� ������ � �������
                    dictionaryPtr++;
                }
                else if (code == dictionaryPtr) // ����������� ������, ����� ��� ��������� � ����������
                {
                    addToOutput = dictionary[oldCode]; // ��������� ������
                    addToOutput = BuildString(addToOutput, addToOutput[0]);
                    dictionary.Add(addToOutput); // ��������� ������ � �������
                    dictionaryPtr++;
                }
                else
                {
                    throw new ApplicationException("LZWDecode: ������ � �����.");
                }

                // ��������� ������ � �������� �����
                writeBuffer.AddRange(addToOutput);

                // ���������� ���
                oldCode = code;

                // ����������� ����� ���� �� ���� ����� �������
                if (dictionaryPtr == 511 || dictionaryPtr == 1023 || dictionaryPtr == 2047)
                {
                    codeLength++;
                    codeMask = (codeMask << 1) + 1;
                }
            }

            return writeBuffer.ToArray(); // ����������� List<byte> � byte[]
        }

        // �������������� ������
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
