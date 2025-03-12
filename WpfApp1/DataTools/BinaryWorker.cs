using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixArtConverter.DataTools
{
    public class BinaryWorker
    {
        private string _path = "";

        public string Path
        {
            get { return _path; }
            set { _path = value; }
        }

        public BinaryWorker(string path)
        {
            Path = path;
        }

        public List<(bool, bool, ushort, byte)> FileValues = new List<(bool, bool, ushort, byte)>();

        internal List<(bool isWall, bool isTorch, ushort id, byte paintId)> Read()
        {
            List<(bool, bool, ushort, byte)> Array = new List<(bool, bool, ushort, byte)>();
            byte[] bytes = File.ReadAllBytes(Path);

            ushort WidthStart = (ushort)((bytes[0] & 0xff) + ((bytes[1] & 0xff) << 8));
            ushort Width = (ushort)((bytes[2] & 0xff) + ((bytes[3] & 0xff) << 8));
            ushort Height = (ushort)((bytes[4] & 0xff) + ((bytes[5] & 0xff) << 8));

            for (int i = 6; i < bytes.Length && i + 4 < bytes.Length; i += 5)
            {
                Array.Add((
                    Convert.ToBoolean(bytes[i]),
                    Convert.ToBoolean(bytes[i + 1]),
                    (ushort)((bytes[i + 2] & 0xff) + ((bytes[i + 3] & 0xff) << 8)),
                    bytes[i + 4]
                ));
            }
            return Array;
        }

        private static string ConvertToBinary(ushort value, byte length)
        {
            string tmp = String.Empty;
            string result = Convert.ToString(value, 2);

            for (int i = 0; i < (length - result.Length); i++)
            {
                tmp += "0";
            }
            return tmp + result;
        }

        internal void Write(ushort width, ushort height, ushort widthStart = 0, List<(bool, bool, ushort, byte)> array = null)
        {
            array ??= FileValues;
            const int bufferSize = 6 * 1024 * 1024; // 6 МБ

            using var stream = new FileStream(
                Path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: bufferSize,
                FileOptions.SequentialScan
            );

            // Заголовок (6 байт)
            Span<byte> header = stackalloc byte[6];
            header[0] = (byte)widthStart;
            header[1] = (byte)(widthStart >> 8);
            header[2] = (byte)width;
            header[3] = (byte)(width >> 8);
            header[4] = (byte)height;
            header[5] = (byte)(height >> 8);
            stream.Write(header);

            // Буфер на 6 МБ (1,258,291 элементов)
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                int bufferIndex = 0;
                foreach (var item in array)
                {
                    buffer[bufferIndex++] = item.Item1 ? (byte)1 : (byte)0;
                    buffer[bufferIndex++] = item.Item2 ? (byte)1 : (byte)0;
                    buffer[bufferIndex++] = (byte)item.Item3;
                    buffer[bufferIndex++] = (byte)(item.Item3 >> 8);
                    buffer[bufferIndex++] = item.Item4;

                    if (bufferIndex >= buffer.Length - 4) // -4 для предотвращения переполнения
                    {
                        stream.Write(buffer, 0, bufferIndex);
                        bufferIndex = 0;
                    }
                }

                if (bufferIndex > 0)
                {
                    stream.Write(buffer, 0, bufferIndex);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
