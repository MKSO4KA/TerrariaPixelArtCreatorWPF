using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixArtConverter.DataTools
{
    public static class StringExtensions
    {
        public static string FormatInputData(string input)
        {
            // Разделяем строку на части
            var parts = input.Split(new[] { ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
            // Инициализация переменных
            string id = string.Empty;
            string color = string.Empty;
            string name = string.Empty;

            // Определяем id, color и name
            foreach (var part in parts)
            {
                string trimmedPart = part.Trim();

                // Проверяем, является ли часть id
                if (uint.TryParse(trimmedPart, out uint idValue))
                {
                    id = trimmedPart;
                }
                // Проверяем, является ли часть цветом
                else if (IsHexColor(trimmedPart))
                {
                    color = trimmedPart;
                }
                // Если это не id и не color, то это имя
                else
                {
                    name = trimmedPart;
                }
            }

            // Формируем новую строку
            return $"1:{id}:0:{color}:Block:{name}:-";
        }
        // Метод для проверки, является ли строка шестнадцатеричным цветом
        private static bool IsHexColor(string input)
        {
            return input.Length == 7 && input.All(c => "#0123456789ABCDEFabcdef".Contains(c));
        }
    }
    public static class MatrixCoder
    {
        private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private static readonly int[,] EncodingMatrix = { { 3, 2 }, { 1, 4 } };
        private static readonly int[,] DecodingMatrix = { { 4, -2 }, { -1, 3 } };

        public static string Encode(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Преобразование в ASCII-коды с паддингом
            int[] codes = input.Select(c => (int)c).ToArray();
            if (codes.Length % 2 != 0) codes = codes.Append(0).ToArray();

            // Матричное кодирование
            List<int> encodedNumbers = new List<int>();
            for (int i = 0; i < codes.Length; i += 2)
            {
                int x = codes[i];
                int y = codes[i + 1];

                int a = EncodingMatrix[0, 0] * x + EncodingMatrix[0, 1] * y;
                int b = EncodingMatrix[1, 0] * x + EncodingMatrix[1, 1] * y;

                encodedNumbers.Add(a);
                encodedNumbers.Add(b);
            }

            // Base64 кодирование с фиксированной длиной
            return string.Join("", encodedNumbers.Select(n => ToBase62(n)));
        }

        public static string Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return string.Empty;
            if (encoded.Length % 2 != 0) throw new ArgumentException("Invalid encoded string");

            // Разбиение на пары символов
            List<int> numbers = new List<int>();
            for (int i = 0; i < encoded.Length; i += 2)
            {
                string pair = encoded.Substring(i, 2);
                numbers.Add(FromBase62(pair));
            }

            // Матричное декодирование
            List<int> decodedCodes = new List<int>();
            for (int i = 0; i < numbers.Count; i += 2)
            {
                int a = numbers[i];
                int b = numbers[i + 1];

                int x = (DecodingMatrix[0, 0] * a + DecodingMatrix[0, 1] * b) / 10;
                int y = (DecodingMatrix[1, 0] * a + DecodingMatrix[1, 1] * b) / 10;

                decodedCodes.Add(x);
                decodedCodes.Add(y);
            }

            // Удаление паддинга и преобразование
            return new string(decodedCodes.TakeWhile(c => c != 0).Select(c => (char)c).ToArray());
        }

        private static string ToBase62(int number)
        {
            if (number < 0) throw new ArgumentException("Number must be non-negative");

            var sb = new StringBuilder();
            do
            {
                sb.Insert(0, Base62Chars[number % 62]);
                number /= 62;
            } while (number > 0);

            // Паддинг до 2 символов
            return sb.Length switch
            {
                1 => "0" + sb,
                2 => sb.ToString(),
                _ => throw new InvalidOperationException("Unexpected base62 length")
            };
        }

        private static int FromBase62(string str)
        {
            return str.Aggregate(0, (current, c) =>
                current * 62 + Base62Chars.IndexOf(c));
        }
    }
    public static class FileDirectoryTools
    {
        public static bool IsFileAccessible(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false; // Путь не должен быть пустым
            }

            return File.Exists(filePath) &&
                   (new FileInfo(filePath).Attributes & FileAttributes.ReadOnly) == 0; // Проверка на доступность
        }
    }
}
