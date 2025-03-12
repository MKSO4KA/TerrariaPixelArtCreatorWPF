using WpfApp1.Dithering;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System;
using Emgu.CV.Structure;
using System.Xml.Linq;
using System.Runtime.InteropServices;
using System.Buffers.Binary;
using System.Buffers;

/*
 * Copyright (C) 2025 MKSO4KA
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

namespace WpfApp1
{
    namespace MainScript
    {
        public class UniqueIndexGenerator : IDisposable
        {
            private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
            private int _currentIndex = 1;

            public async Task<int> GetNextIndexAsync()
            {
                await _semaphore.WaitAsync();
                try
                {
                    return _currentIndex++;
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            public void Dispose()
            {
                _semaphore?.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        public class VideoProcessor
        {
            private readonly string VideoPath;
            private readonly string TotalPath;
            private readonly byte FRAMERATE;
            private readonly int semafors;

            public VideoProcessor(string videoPath, string totalPath, byte frameRate = 30, int semafors = 1)
            {
                VideoPath = videoPath;
                TotalPath = totalPath;
                FRAMERATE = frameRate;
                this.semafors = semafors;
            }

            public async Task ProcessFramesAsync()
            {
                using SemaphoreSlim semaphore = new SemaphoreSlim(semafors);
                List<Task> tasks = new List<Task>();
                using var indexGenerator = new UniqueIndexGenerator();
                ConcurrentDictionary<int, bool> inds = new ConcurrentDictionary<int, bool>();
                ConcurrentQueue<(int index, Image<Rgb24> frame)> frameQueue = new ConcurrentQueue<(int, Image<Rgb24>)>();

                foreach (var frame in VideoFrameExtractor.ExtractFrames(VideoPath, FRAMERATE))
                {
                    int index = await indexGenerator.GetNextIndexAsync();
                    frameQueue.Enqueue((index, frame));
                }

                while (frameQueue.TryDequeue(out var item))
                {
                    await semaphore.WaitAsync();

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            int index = item.index;
                            using Image<Rgb24> frame = item.frame;
                            string filePath = TotalPath + $"\\photo{index}.txt";

                            if (File.Exists(filePath) || inds.ContainsKey(index))
                                throw new Exception("File Exists");

                            if (!inds.TryAdd(index, true))
                                throw new Exception("inds error");

                            PhotoTileConverter converter = new PhotoTileConverter(VideoPath, "", filePath);
                            await converter.Convert(index, frame,MainWindow.UserTiles);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                        finally
                        {
                            inds.TryRemove(item.index, out _);
                            semaphore.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }
        }

        public class VideoFrameExtractor
        {
            public static IEnumerable<Image<Rgb24>> ExtractFrames(string videoPath, uint targetFrameRate)
            {
                using var capture = new VideoCapture(videoPath);

                if (!capture.IsOpened)
                    throw new InvalidOperationException("Could not open video file");

                double sourceFps = capture.Get(CapProp.Fps);
                if (sourceFps <= 0)
                    throw new InvalidOperationException("Invalid frame rate");

                // Корректный расчет интервала кадров
                int frameInterval = (int)(sourceFps / targetFrameRate);
                frameInterval = Math.Max(1, frameInterval); // Гарантируем минимум 1

                Mat frame = new Mat();
                int frameCount = 0;

                try
                {
                    while (capture.Read(frame) && !frame.IsEmpty)
                    {
                        if (frameCount % frameInterval == 0)
                        {
                            yield return ConvertMatToImage(frame);
                        }
                        frameCount++;
                    }
                }
                finally
                {
                    frame?.Dispose();
                }
            }

            private static Image<Rgb24> ConvertMatToImage(Mat mat)
            {
                using var rgbMat = new Mat();
                CvInvoke.CvtColor(mat, rgbMat, ColorConversion.Bgr2Rgb); // EMGU CV аналог

                var array = new byte[rgbMat.Width * rgbMat.Height * 3];
                rgbMat.CopyTo(array);

                return Image.LoadPixelData<Rgb24>(
                    MemoryMarshal.Cast<byte, Rgb24>(array),
                    rgbMat.Width,
                    rgbMat.Height
                );
            }
        }

        public class PhotoProcessor
        {
            private readonly string PhotoPath;
            private readonly string TotalPath;
            private readonly int semafors;

            public PhotoProcessor(string photoPath, string totalPath)
            {
                PhotoPath = photoPath;
                TotalPath = totalPath;
                this.semafors = 1;
            }

            public async Task ProcessFramesAsync()
            {
                using SemaphoreSlim semaphore = new SemaphoreSlim(semafors);
                List<Task> tasks = new List<Task>();
                using var indexGenerator = new UniqueIndexGenerator();
                ConcurrentDictionary<int, bool> inds = new ConcurrentDictionary<int, bool>();
                ConcurrentQueue<(int index, Image<Rgb24> frame)> frameQueue = new ConcurrentQueue<(int, Image<Rgb24>)>();

                var imageFiles = new List<string> { PhotoPath };

                foreach (var frame in PhotoReturner.GetPhotos(imageFiles))
                {
                    int index = await indexGenerator.GetNextIndexAsync();
                    frameQueue.Enqueue((index, frame));
                }

                while (frameQueue.TryDequeue(out var item))
                {
                    await semaphore.WaitAsync();

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            int index = item.index;
                            using Image<Rgb24> frame = item.frame;
                            string filePath = Path.Combine(TotalPath, $"photo{index}.txt");

                            if (File.Exists(filePath) || inds.ContainsKey(index))
                            {
                                while (true)
                                {
                                    index++;
                                    filePath = Path.Combine(TotalPath, $"photo{index}.txt");
                                    if (!File.Exists(filePath))
                                    {
                                        break;
                                    }
                                }
                                //throw new Exception("File Exists");
                            }

                            if (!inds.TryAdd(index, true))
                                throw new Exception("inds error");

                            PhotoTileConverter converter = new PhotoTileConverter(PhotoPath, "", filePath);
                            await converter.Convert(index, frame,MainWindow.UserTiles);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Processing error: {ex.Message}");
                        }
                        finally
                        {
                            inds.TryRemove(item.index, out _);
                            semaphore.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }
        }

        public class PhotoReturner
        {
            public static IEnumerable<Image<Rgb24>> GetPhotos(IEnumerable<string> imageFiles)
            {
                foreach (var file in imageFiles)
                {
                    using var image = Image.Load<Rgb24>(file);
                    yield return image.Clone();
                }
            }
        }

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

        public class Pixels : IDisposable
        {
            private bool _disposed = false;
            public List<Pixel> Objects = new List<Pixel>();

            // Добавляем реализацию IDisposable
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        // Освобождаем управляемые ресурсы
                        Objects?.Clear();
                        _pixels?.Clear();
                        _colors?.Clear();
                    }
                    _disposed = true;
                }
            }

            public Pixels(string[] Path, bool Mode = true)
            {
                if (Mode)
                {
                    foreach (string tileLine in Path)
                    {
                        string[] parts = tileLine.Split(':');
                        Add(new Pixel(parts, parts[0] == "0"));
                    }
                    return;
                }
                if (!Mode)
                {
                    foreach (string tileLine in Path)
                    {
                        string[] parts = tileLine.Split(':');
                        Add(new Pixel(parts, true));
                    }
                    return;
                }
            }

            public void Add(Pixel pixel)
            {
                Objects.Add(pixel);
            }

            public void Del(Pixel pixel)
            {
                Objects.Remove(pixel);
            }

            List<(bool, bool, ushort, byte)> _pixels;
            public List<(bool, bool, ushort, byte)> GetPixels()
            {
                _pixels ??= Objects.Select(x => (x.Wall, x.WallAtached, x.id, x.paint)).ToList();
                return _pixels;
            }

            List<(byte, byte, byte)> _colors;
            public List<(byte, byte, byte)> GetColors()
            {
                _colors ??= Objects.Select(x => x.color).ToList();
                return _colors;
            }
        }

        public partial class Pixel
        {
            public string Name;
            public bool Wall = false;
            public ushort id;
            public byte paint;
            public (byte, byte, byte) color;
            public bool WallAtached = false;

            public Pixel(XElement element, bool wall = false)
            {
                Name = element.Attribute("name").Value;
                Wall = wall;
                id = Convert.ToUInt16(element.Attribute("num").Value);
                paint = Convert.ToByte(element.Attribute("paintID").Value);
                color = ToBytes(element.Attribute("color").Value) ?? (0, 0, 0);
                WallAtached = element.Attribute("Torch").Value == "true";
            }

            public Pixel(string[] parts, bool wall = false)
            {
                Name = string.Concat(parts[5], " ", parts[6]);
                Wall = wall;
                id = Convert.ToUInt16(parts[1]);
                paint = Convert.ToByte(parts[2]);
                color = ToBytes(parts[3]) ?? (0, 0, 0);
                WallAtached = wall == true ? false : DefTorchs.IndexOf(parts[1]) != -1;
            }

            public static (byte, byte, byte)? ToBytes(string hexValue)
            {
                int hexColor = Convert.ToInt32(hexValue.Replace("#", ""), 16);
                return ((byte)((hexColor >> 16) & 0xff),
                        (byte)((hexColor >> 8) & 0xff),
                        (byte)(hexColor & 0xff));
            }

            private static List<string> DefTorchs
            {
                get { return _defTorchs; }
                set { _defTorchs = value; }
            }
            // Статический список для хранения значений по умолчанию для исключений стен
            private static List<string> DefExceptionsWalls
            {
                get { return _defExceptionsWalls; }
                set { _defExceptionsWalls = value; }
            }
            private static List<string> DefExceptionsTiles
            {
                get { return _defExceptionsTiles; }
                set { _defExceptionsTiles = value; }
            }
        }

        internal class PhotoTileConverter
        {
            private string _path;
            private string _totalpath;
            private string _tilespath;

            public PhotoTileConverter(string PhotoPath, string TilesPath = "", string TotalPath = "")
            {
                _path = PhotoPath;
                _tilespath = TilesPath;
                _totalpath = string.IsNullOrEmpty(TotalPath) ?
                    System.Environment.GetEnvironmentVariable("USERPROFILE") + "\\Documents\\PixelArtCreatorByMixailka\\photo.txt" :
                    TotalPath;
            }

            private Color[]? _colors;
            public Color[] Colors
            {
                get { return _colors ?? Array.Empty<Color>(); }
                set { _colors = value; }
            }

            public IEnumerable<Color>[] ReadPhoto(Image<Rgb24> image, out int width, out int height)
            {
                width = image.Width;
                height = image.Height;
                List<Color> pixelColors = new List<Color>(width * height);

                for (var i = 0; i < width; i++)
                {
                    for (var j = 0; j < height; j++)
                    {
                        var pixel = image[i, j];
                        pixelColors.Add(Color.FromRgb(pixel.R, pixel.G, pixel.B));
                    }
                }

                return Enumerable.Range(0, (int)Math.Ceiling((double)(width * height) / 100000.0))
                                 .Select(i => pixelColors.Skip(i * 100000).Take(100000)).ToArray();
            }

            public async Task Convert(int i, Image<Rgb24> image = null, bool UserTilesOn = false)
            {
                ColorApproximater approximater = new ColorApproximater(_tilespath);
                if (UserTilesOn)
                {
                    approximater.SetTiles(File.ReadAllLines(MainWindow.UserTilesPath).ToList());
                }
                
                bool isImageOwned = image == null;
                image ??= Image.Load<Rgb24>(_path);

                try
                {
                    await Task.Run(() =>
                    {
                        var Dither = new AtkinsonDithering();
                        Dither.Do(image, approximater, new BinaryWorker(_totalpath));
                    });
                }
                finally
                {
                    if (isImageOwned)
                        image?.Dispose();
                }
            }
        }
    }
}
