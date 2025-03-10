using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using WpfApp1.MainScript;
using WpfApp1.Shiftren;
using System.Windows;
using static WpfApp1.Dithering.ColorApproximater;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using System.Windows.Media;
using Windows.Data.Text;
using SixLabors.ImageSharp.Processing.Processors.Convolution;









#if DEBUG
using System.Diagnostics;
#endif
namespace WpfApp1
{
    namespace Dithering
    {
        public static class DitheringSelector
        {
            public static string GetOptimalDithererName(ImageAnalysisResult analysis)
            {
                return analysis switch
                {
                    // 1. Очень маленькие изображения
                    { Width: <= 64, Height: <= 64, NoiseLevel: < 0.1 } => "Atkinson",
                    { Width: <= 64, Height: <= 64 } => "SierraLite",

                    // 2. Маленькие изображения (до 500x500)
                    { IsSmallImage: true, NoiseLevel: < 0.1 } => "Atkinson",
                    { IsSmallImage: true, HasSmoothGradients: true } => "FloydSteinberg",
                    { IsSmallImage: true } => "SierraLite",

                    // 3. Плавные градиенты + шум
                    { HasSmoothGradients: true, NoiseLevel: < 0.05 } => "JarvisJudiceNinke",
                    { HasSmoothGradients: true, NoiseLevel: < 0.2 } => "Stucki",
                    { HasSmoothGradients: true } => "Burkes",

                    // 4. Высокочастотные изображения
                    { HasHighFrequency: true, NoiseLevel: < 0.1 } => "Burkes",
                    { HasHighFrequency: true, NoiseLevel: < 0.3 } => "Sierra3",
                    { HasHighFrequency: true } => "FloydSteinberg",

                    // 5. Высокий контраст
                    { Contrast: > 0.25, NoiseLevel: < 0.15 } => "Atkinson",
                    { Contrast: > 0.25 } => "FloydSteinberg",

                    // 6. Фоллбек
                    _ => analysis.NoiseLevel < 0.1 ? "JarvisJudiceNinke" : "FloydSteinberg"
                };
            }
            public static Dictionary<string, Type> DitheringAlgorithms = new()
            {
                { "Atkinson", typeof(AtkinsonDitheringRGBByte) },
                { "FloydSteinberg", typeof(FloydSteinbergDitheringRGBByte) },
                { "JarvisJudiceNinke", typeof(JarvisJudiceNinkeDitheringRGBByte) },
                { "Stucki", typeof(StuckiDitheringRGBByte) },
                { "Burkes", typeof(BurkesDitheringRGBByte) },
                { "Sierra3", typeof(Sierra3DitheringRGBByte) },
                { "SierraLite", typeof(SierraLiteDitheringRGBByte) },
                { "Sierra24a", typeof(Sierra24aDitheringRGBByte) }
            };
        }
        public sealed class ImageAnalyzer : IDisposable
        {
            private readonly Image<Rgb24> _image;
            private bool _disposed;

            public ImageAnalyzer(Image<Rgb24> image)
            {
                _image = image.Clone();
                _image.Mutate(c => c.Grayscale());
            }

            public ImageAnalysisResult Analyze()
            {
                int width = _image.Width;
                int height = _image.Height;

                return new ImageAnalysisResult(
                    width: width,
                    height: height,
                    contrast: CalculateContrast(),
                    hasSmoothGradients: DetectSmoothGradients(),
                    hasHighFrequency: DetectHighFrequency(),
                    hasSharpEdges: DetectSharpEdges(),
                    colorCount: CalculateColorCount(),
                    noiseLevel: CalculateNoiseLevel(_image)
                );
            }

            public static double CalculateNoiseLevel(Image<Rgb24> image)
            {
                double variance = CalculateBrightnessVariance(image);
                return Math.Clamp(variance / 500.0, 0, 1);
            }
            private int CalculateColorCount()
            {
                var uniqueColors = new HashSet<string>();

                _image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgb24> row = accessor.GetRowSpan(y);
                        foreach (ref Rgb24 pixel in row)
                        {
                            uniqueColors.Add(pixel.ToString());
                        }
                    }
                });

                return uniqueColors.Count;
            }

            private bool DetectSharpEdges()
            {
                int strongEdgeCount = 0;
                using var clone = _image.Clone();

                clone.Mutate(c => c.DetectEdges(EdgeDetector2DKernel.SobelKernel));

                clone.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgb24> row = accessor.GetRowSpan(y);
                        foreach (ref Rgb24 pixel in row)
                        {
                            if (pixel.R > 200) strongEdgeCount++;
                        }
                    }
                });

                return strongEdgeCount > (_image.Width * _image.Height * 0.05);
            }
            private static double CalculateBrightnessVariance(Image<Rgb24> image, int sampleStep = 5)
            {
                List<double> brightnessValues = new();

                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y += sampleStep)
                    {
                        Span<Rgb24> row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x += sampleStep)
                        {
                            double brightness = GetBrightness(ref row[x]);
                            brightnessValues.Add(brightness);
                        }
                    }
                });

                return CalculateVariance(brightnessValues);
            }

            private double CalculateContrast()
            {
                double min = 255, max = 0;

                _image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y += 3)
                    {
                        Span<Rgb24> row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x += 3)
                        {
                            double brightness = GetBrightness(ref row[x]);
                            if (brightness < min) min = brightness;
                            if (brightness > max) max = brightness;
                        }
                    }
                });

                return (max - min) / 255;
            }

            private bool DetectSmoothGradients()
            {
                int smoothCount = 0;
                int totalChecked = 0;

                _image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height - 1; y++)
                    {
                        Span<Rgb24> currentRow = accessor.GetRowSpan(y);
                        Span<Rgb24> nextRow = accessor.GetRowSpan(y + 1);

                        for (int x = 0; x < accessor.Width - 1; x++)
                        {
                            double hDiff = ColorDiff(currentRow[x], currentRow[x + 1]);
                            double vDiff = ColorDiff(currentRow[x], nextRow[x]);

                            if (hDiff < 0.1 && vDiff < 0.1) smoothCount++;
                            totalChecked++;
                        }
                    }
                });

                return (double)smoothCount / totalChecked > 0.3;
            }

            private bool DetectHighFrequency()
            {
                int edgeCount = 0;
                using var clone = _image.Clone();

                clone.Mutate(c => c.DetectEdges(EdgeDetector2DKernel.SobelKernel));

                clone.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgb24> row = accessor.GetRowSpan(y);
                        foreach (ref Rgb24 pixel in row)
                        {
                            if (pixel.R > 50) edgeCount++;
                        }
                    }
                });

                return edgeCount > (_image.Width * _image.Height * 0.1);
            }

            private static double GetBrightness(ref Rgb24 pixel) =>
                0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;

            private static double ColorDiff(Rgb24 a, Rgb24 b) =>
                Math.Abs(GetBrightness(ref a) - GetBrightness(ref b)) / 255;

            private static double CalculateVariance(List<double> values)
            {
                if (values.Count < 2) return 0;
                double mean = values.Average();
                return values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _image.Dispose();
                _disposed = true;
            }
        }

        public class ImageAnalysisResult
        {
            public int Width { get; }
            public int Height { get; }
            public double Contrast { get; }
            public bool HasSmoothGradients { get; }
            public bool HasHighFrequency { get; }
            public bool HasSharpEdges { get; }
            public int ColorCount { get; }
            public double NoiseLevel { get; }
            public bool IsSmallImage => Width * Height < 500_000;

            public ImageAnalysisResult(
                int width,
                int height,
                double contrast,
                bool hasSmoothGradients,
                bool hasHighFrequency,
                bool hasSharpEdges,
                int colorCount,
                double noiseLevel)
            {
                Width = width;
                Height = height;
                Contrast = contrast;
                HasSmoothGradients = hasSmoothGradients;
                HasHighFrequency = hasHighFrequency;
                HasSharpEdges = hasSharpEdges;
                ColorCount = colorCount;
                NoiseLevel = noiseLevel;
            }
        }



        #region Dithering Implementation (Optimized with Error Handling)



        public abstract class DitheringBase<T>
        {
            protected int width;
            protected int height;
            protected int channelsPerPixel;
            protected ColorFunction colorFunction;
            private IImageFormat<T> currentBitmap;

            public delegate void ColorFunction(in T[] inputColors, ref T[] outputColors, ColorApproximater colorApproximater);

            protected DitheringBase(ColorFunction colorfunc)
            {
                colorFunction = colorfunc ?? throw new ArgumentNullException(nameof(colorfunc));
            }

            public IImageFormat<T> DoDithering(IImageFormat<T> input, ColorApproximater approximater)
            {
                if (input == null)
                    throw new ArgumentNullException(nameof(input));
                if (approximater == null)
                    throw new ArgumentNullException(nameof(approximater));

                try
                {
                    width = input.GetWidth();
                    height = input.GetHeight();
                    channelsPerPixel = input.GetChannelsPerPixel();
                    currentBitmap = input;

                    T[] originalPixel = new T[channelsPerPixel];
                    T[] newPixel = new T[channelsPerPixel];
                    double[] quantError = new double[channelsPerPixel];

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            input.GetPixelChannels(x, y, ref originalPixel);
                            colorFunction(in originalPixel, ref newPixel, approximater);

                            if (newPixel.Length != channelsPerPixel)
                                throw new InvalidOperationException("Color function returned incorrect pixel format.");

                            input.SetPixelChannels(x, y, newPixel);
                            input.GetQuantErrorsPerChannel(in originalPixel, in newPixel, ref quantError);
                            PushError(x, y, quantError);
                        }
                    }
                    return input;
                }
                catch (Exception ex)
                {
                    throw new DitheringException("Dithering process failed.", ex);
                }
            }

            protected bool IsValidCoordinate(int x, int y)
            {
                return x >= 0 && x < width && y >= 0 && y < height;
            }

            protected abstract void PushError(int x, int y, double[] quantError);

            protected void ModifyImageWithErrorAndMultiplier(int x, int y, double[] quantError, double multiplier)
            {
                T[] tempBuffer = new T[channelsPerPixel];
                currentBitmap.GetPixelChannels(x, y, ref tempBuffer);
                currentBitmap.ModifyPixelChannelsWithQuantError(ref tempBuffer, quantError, multiplier);
                currentBitmap.SetPixelChannels(x, y, tempBuffer);
            }
        }
        /// <summary>
        /// Использование:<br/>
        /// Размеры: Малые и средние (500–1500 пикселей).<br/>
        /// Особенности: Даёт чёткие паттерны с минимальным размытием, подходит для пиксель-арта.
        /// </summary>
        public sealed class AtkinsonDitheringRGBByte : DitheringBase<byte>
        {
            private static readonly int[] xOffsets = { 1, 2, -1, 0, 1, 0 };
            private static readonly int[] yOffsets = { 0, 0, 1, 1, 1, 2 };

            public AtkinsonDitheringRGBByte(ColorFunction colorfunc) : base(colorfunc) { }

            protected override void PushError(int x, int y, double[] quantError)
            {
                const double multiplier = 1.0 / 8.0;
                for (int i = 0; i < xOffsets.Length; i++)
                {
                    int nx = x + xOffsets[i];
                    int ny = y + yOffsets[i];
                    if (IsValidCoordinate(nx, ny))
                        ModifyImageWithErrorAndMultiplier(nx, ny, quantError, multiplier);
                }
            }
        }
        /// <summary>
        /// Использование:<br/>
        /// Размеры: Универсальный (500–5000 пикселей).<br/>
        /// Особенности: Классический алгоритм с естественным распределением ошибок, сохраняет детализацию.
        /// </summary>
        public sealed class FloydSteinbergDitheringRGBByte : DitheringBase<byte>
        {
            // Смещения для распространения ошибки (право, лево-вниз, вниз, право-вниз)
            private static readonly int[] xOffsets = { 1, -1, 0, 1 };
            private static readonly int[] yOffsets = { 0, 1, 1, 1 };

            // Множители ошибки для каждого смещения (7/16, 3/16, 5/16, 1/16)
            private static readonly double[] multipliers =
                { 7.0 / 16, 3.0 / 16, 5.0 / 16, 1.0 / 16 };

            public FloydSteinbergDitheringRGBByte(ColorFunction colorfunc)
                : base(colorfunc) { }

            protected override void PushError(int x, int y, double[] quantError)
            {
                for (int i = 0; i < xOffsets.Length; i++)
                {
                    int nx = x + xOffsets[i];
                    int ny = y + yOffsets[i];
                    if (IsValidCoordinate(nx, ny))
                    {
                        // Применяем соответствующий множитель для каждого направления
                        ModifyImageWithErrorAndMultiplier(nx, ny, quantError, multipliers[i]);
                    }
                }
            }
        }
        /// <summary>
        /// Использование:<br/>
        /// Размеры: Большие (2000+ пикселей).<br/>
        /// Особенности: Сложное распределение ошибок для плавных переходов, требует больше ресурсов.
        /// </summary>
        public sealed class JarvisJudiceNinkeDitheringRGBByte : DitheringBase<byte>
        {
            private static readonly int[] xOffsets = { 1, 2, -2, -1, 0, 1, 2, -2, -1, 0, 1, 2 };
            private static readonly int[] yOffsets = { 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2 };
            private static readonly double[] multipliers =
                { 7.0/48, 5.0/48, 3.0/48, 5.0/48, 7.0/48, 5.0/48, 3.0/48, 1.0/48, 3.0/48, 5.0/48, 3.0/48, 1.0/48 };

            public JarvisJudiceNinkeDitheringRGBByte(ColorFunction colorfunc) : base(colorfunc) { }

            protected override void PushError(int x, int y, double[] quantError)
            {
                for (int i = 0; i < xOffsets.Length; i++)
                {
                    int nx = x + xOffsets[i];
                    int ny = y + yOffsets[i];
                    if (IsValidCoordinate(nx, ny))
                        ModifyImageWithErrorAndMultiplier(nx, ny, quantError, multipliers[i]);
                }
            }
        }
        /// <summary>
        /// Использование:<br/>
        /// Размеры: Очень большие (3000+ пикселей).<br/>
        /// Особенности: Модификация JJN с оптимизацией вычислений, сохраняет плавные градиенты.
        /// </summary>
        public sealed class StuckiDitheringRGBByte : DitheringBase<byte>
        {
            private static readonly int[] xOffsets = { 1, 2, -2, -1, 0, 1, 2, -2, -1, 0, 1, 2 };
            private static readonly int[] yOffsets = { 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2 };
            private static readonly double[] multipliers =
                { 8.0/42, 4.0/42, 2.0/42, 4.0/42, 8.0/42, 4.0/42, 2.0/42, 1.0/42, 2.0/42, 4.0/42, 2.0/42, 1.0/42 };

            public StuckiDitheringRGBByte(ColorFunction colorfunc) : base(colorfunc) { }

            protected override void PushError(int x, int y, double[] quantError)
            {
                for (int i = 0; i < xOffsets.Length; i++)
                {
                    int nx = x + xOffsets[i];
                    int ny = y + yOffsets[i];
                    if (IsValidCoordinate(nx, ny))
                        ModifyImageWithErrorAndMultiplier(nx, ny, quantError, multipliers[i]);
                }
            }
        }
        /// <summary>
        /// Использование:<br/>
        /// Размеры: Средние (1000–2500 пикселей).<br/>
        /// Особенности: Упрощённая версия Stucki с фокусом на производительность.
        /// </summary>
        public sealed class BurkesDitheringRGBByte : DitheringBase<byte>
        {
            private static readonly int[] xOffsets = { 1, 2, -2, -1, 0, 1, 2 };
            private static readonly int[] yOffsets = { 0, 0, 1, 1, 1, 1, 1 };
            private static readonly double[] multipliers =
                { 8.0/32, 4.0/32, 2.0/32, 4.0/32, 8.0/32, 4.0/32, 2.0/32 };

            public BurkesDitheringRGBByte(ColorFunction colorfunc) : base(colorfunc) { }

            protected override void PushError(int x, int y, double[] quantError)
            {
                for (int i = 0; i < xOffsets.Length; i++)
                {
                    int nx = x + xOffsets[i];
                    int ny = y + yOffsets[i];
                    if (IsValidCoordinate(nx, ny))
                        ModifyImageWithErrorAndMultiplier(nx, ny, quantError, multipliers[i]);
                }
            }
        }
        /// <summary>
        /// Использование:<br/>
        /// Размеры: Большие (2000–4000 пикселей).<br/>
        /// Особенности: Баланс между качеством и производительностью, минимизирует артефакты.
        /// </summary>
        public sealed class Sierra3DitheringRGBByte : DitheringBase<byte>
        {
            private static readonly int[] xOffsets = { 1, 2, -2, -1, 0, 1, 2 };
            private static readonly int[] yOffsets = { 0, 0, 1, 1, 1, 1, 1 };
            private static readonly double[] multipliers =
                { 5.0 /32, 3.0/32, 2.0/32, 3.0/32, 5.0/32, 3.0/32, 1.0/32 };

            public Sierra3DitheringRGBByte(ColorFunction colorfunc) : base(colorfunc) { }

            protected override void PushError(int x, int y, double[] quantError)
            {
                for (int i = 0; i < xOffsets.Length; i++)
                {
                    int nx = x + xOffsets[i];
                    int ny = y + yOffsets[i];
                    if (IsValidCoordinate(nx, ny))
                        ModifyImageWithErrorAndMultiplier(nx, ny, quantError, multipliers[i]);
                }
            }
        }
        /// <summary>
        /// Использование:<br/>
        /// Размеры: Малые (до 1000 пикселей).<br/>
        /// Особенности: Быстрый алгоритм для превью и реалтайм-обработки.
        /// </summary>
        public sealed class SierraLiteDitheringRGBByte : DitheringBase<byte>
        {
            private static readonly int[] xOffsets = { 1, 0, 1 };
            private static readonly int[] yOffsets = { 0, 1, 1 };
            private static readonly double[] multipliers = { 2.0 / 4, 1.0 / 4, 1.0 / 4 };

            public SierraLiteDitheringRGBByte(ColorFunction colorfunc) : base(colorfunc) { }

            protected override void PushError(int x, int y, double[] quantError)
            {
                for (int i = 0; i < xOffsets.Length; i++)
                {
                    int nx = x + xOffsets[i];
                    int ny = y + yOffsets[i];
                    if (IsValidCoordinate(nx, ny))
                        ModifyImageWithErrorAndMultiplier(nx, ny, quantError, multipliers[i]);
                }
            }
        }
        /// <summary>
        /// Использование:<br/>
        /// Размеры: Средние и большие(1000–3000 пикселей). <br/>
        /// Особенности: Хорошо подходит для изображений с плавными градиентами.
        /// </summary>
        public sealed class Sierra24aDitheringRGBByte : DitheringBase<byte>
        {
            private static readonly int[] xOffsets = { 1, 2, -2, -1, 0, 1, 2 };
            private static readonly int[] yOffsets = { 0, 0, 1, 1, 1, 1, 1 };
            private static readonly double[] multipliers =
                { 5.0/32, 3.0/32, 2.0/32, 3.0/32, 5.0/32, 3.0/32, 1.0/32 };

            public Sierra24aDitheringRGBByte(ColorFunction colorfunc) : base(colorfunc) { }

            protected override void PushError(int x, int y, double[] quantError)
            {
                for (int i = 0; i < xOffsets.Length; i++)
                {
                    int nx = x + xOffsets[i];
                    int ny = y + yOffsets[i];
                    if (IsValidCoordinate(nx, ny))
                        ModifyImageWithErrorAndMultiplier(nx, ny, quantError, multipliers[i]);
                }
            }
        }
        public class DitheringException : Exception
        {
            public DitheringException(string message, Exception inner) : base(message, inner) { }
        }

        public interface IImageFormat<T>
        {
            int GetWidth();
            int GetHeight();
            int GetChannelsPerPixel();
            T[] GetRawContent();
            void SetPixelChannels(int x, int y, T[] newValues);
            T[] GetPixelChannels(int x, int y);
            void GetPixelChannels(int x, int y, ref T[] pixelStorage);
            double[] GetQuantErrorsPerChannel(T[] originalPixel, T[] newPixel);
            void GetQuantErrorsPerChannel(in T[] originalPixel, in T[] newPixel, ref double[] errorValues);
            void ModifyPixelChannelsWithQuantError(ref T[] modifyValues, double[] quantErrors, double multiplier);
        }

        public sealed class TempByteImageFormat : IImageFormat<byte>
        {
            private readonly byte[,,] content3d;
            private readonly byte[] content1d;
            public int Width { get; }
            public int Height { get; }
            public int ChannelsPerPixel { get; }

            public TempByteImageFormat(byte[,,] input, bool createCopy = false)
            {
                if (input == null)
                    throw new ArgumentNullException(nameof(input));

                if (createCopy)
                {
                    content3d = (byte[,,])input.Clone();
                }
                else
                {
                    content3d = input;
                }
                content1d = null;
                Width = input.GetLength(0);
                Height = input.GetLength(1);
                ChannelsPerPixel = input.GetLength(2);
            }

            public TempByteImageFormat(byte[] input, int width, int height, int channels, bool createCopy = false)
            {
                if (input == null)
                    throw new ArgumentNullException(nameof(input));
                if (width * height * channels != input.Length)
                    throw new ArgumentException("Invalid dimensions.");

                content3d = null;
                if (createCopy)
                {
                    content1d = new byte[input.Length];
                    Buffer.BlockCopy(input, 0, content1d, 0, input.Length);
                }
                else
                {
                    content1d = input;
                }
                Width = width;
                Height = height;
                ChannelsPerPixel = channels;
            }

            public int GetWidth() => Width;
            public int GetHeight() => Height;
            public int GetChannelsPerPixel() => ChannelsPerPixel;

            public byte[] GetRawContent()
            {
                if (content1d != null) return content1d;

                byte[] result = new byte[Width * Height * ChannelsPerPixel];
                int index = 0;
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        for (int c = 0; c < ChannelsPerPixel; c++)
                        {
                            result[index++] = content3d[x, y, c];
                        }
                    }
                }
                return result;
            }

            public void SetPixelChannels(int x, int y, byte[] newValues)
            {
                ValidateCoordinates(x, y);
                if (newValues == null || newValues.Length != ChannelsPerPixel)
                    throw new ArgumentException("Invalid pixel data.");

                if (content1d != null)
                {
                    int index = (y * Width + x) * ChannelsPerPixel;
                    Array.Copy(newValues, 0, content1d, index, ChannelsPerPixel);
                }
                else
                {
                    for (int i = 0; i < ChannelsPerPixel; i++)
                    {
                        content3d[x, y, i] = newValues[i];
                    }
                }
            }

            public byte[] GetPixelChannels(int x, int y)
            {
                ValidateCoordinates(x, y);
                byte[] pixel = new byte[ChannelsPerPixel];
                GetPixelChannels(x, y, ref pixel);
                return pixel;
            }

            public void GetPixelChannels(int x, int y, ref byte[] pixelStorage)
            {
                ValidateCoordinates(x, y);
                if (pixelStorage == null || pixelStorage.Length < ChannelsPerPixel)
                    throw new ArgumentException("Invalid pixel buffer.");

                if (content1d != null)
                {
                    int index = (y * Width + x) * ChannelsPerPixel;
                    Array.Copy(content1d, index, pixelStorage, 0, ChannelsPerPixel);
                }
                else
                {
                    for (int i = 0; i < ChannelsPerPixel; i++)
                    {
                        pixelStorage[i] = content3d[x, y, i];
                    }
                }
            }

            public double[] GetQuantErrorsPerChannel(byte[] originalPixel, byte[] newPixel)
            {
                if (originalPixel == null || newPixel == null)
                    throw new ArgumentNullException();
                if (originalPixel.Length != ChannelsPerPixel || newPixel.Length != ChannelsPerPixel)
                    throw new ArgumentException("Invalid pixel data.");

                double[] errors = new double[ChannelsPerPixel];
                GetQuantErrorsPerChannel(originalPixel, newPixel, ref errors);
                return errors;
            }

            public void GetQuantErrorsPerChannel(in byte[] originalPixel, in byte[] newPixel, ref double[] errorValues)
            {
                if (originalPixel == null || newPixel == null || errorValues == null)
                    throw new ArgumentNullException();
                if (originalPixel.Length != ChannelsPerPixel || newPixel.Length != ChannelsPerPixel || errorValues.Length != ChannelsPerPixel)
                    throw new ArgumentException("Array length mismatch.");

                for (int i = 0; i < ChannelsPerPixel; i++)
                {
                    errorValues[i] = originalPixel[i] - newPixel[i];
                }
            }

            public void ModifyPixelChannelsWithQuantError(ref byte[] modifyValues, double[] quantErrors, double multiplier)
            {
                if (modifyValues == null || quantErrors == null)
                    throw new ArgumentNullException();
                if (modifyValues.Length != ChannelsPerPixel || quantErrors.Length != ChannelsPerPixel)
                    throw new ArgumentException("Array length mismatch.");

                for (int i = 0; i < ChannelsPerPixel; i++)
                {
                    double newValue = modifyValues[i] + quantErrors[i] * multiplier;
                    modifyValues[i] = Clamp(newValue);
                }
            }

            private static byte Clamp(double value)
            {
                return (byte)Math.Clamp(value, 0, 255);
            }

            private void ValidateCoordinates(int x, int y)
            {
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                    throw new ArgumentOutOfRangeException($"Coordinates ({x}, {y}) are out of bounds.");
            }
        }

        public class AtkinsonDithering
        {
            private static readonly int ColorFunctionMode = 1;

            public Image<Rgb24> Do(Image<Rgb24> image, ColorApproximater approximater, BinaryWorker worker)
            {
                if (image == null)
                    throw new ArgumentNullException(nameof(image));
                if (approximater == null)
                    throw new ArgumentNullException(nameof(approximater));

                try
                {
#if DEBUG
                    var stopwatch = Stopwatch.StartNew(); // Запуск таймера
#endif
                    //var atkinson = new AtkinsonDitheringRGBByte(ColorFunction);
                    ImageAnalyzer imageAnalyzer = new ImageAnalyzer(image);
                    var analysis = imageAnalyzer.Analyze();

                    // Получение названия алгоритма
                    string algorithmName = DitheringSelector.GetOptimalDithererName(analysis); // TODO: РЕАЛИЗОВАТЬ!!!
                    //string algorithmName = "Sierra24a";
                    Debug.Print($"{algorithmName}");
                    // Создание экземпляра алгоритма
                    //var algorithmName = DitheringSelector.DitheringAlgorithms[algorithmName].FullName;
                    DitheringBase<byte> ditherer = algorithmName switch
                    {
                        "Atkinson" => new AtkinsonDitheringRGBByte(ColorFunction),
                        "FloydSteinberg" => new FloydSteinbergDitheringRGBByte(ColorFunction),
                        "JarvisJudiceNinke" => new JarvisJudiceNinkeDitheringRGBByte(ColorFunction),
                        "Stucki" => new StuckiDitheringRGBByte(ColorFunction),
                        "Burkes" => new BurkesDitheringRGBByte(ColorFunction),
                        "Sierra3" => new Sierra3DitheringRGBByte(ColorFunction),
                        "SierraLite" => new SierraLiteDitheringRGBByte(ColorFunction),
                        "Sierra24a" => new Sierra24aDitheringRGBByte(ColorFunction),
                        _ => throw new NotSupportedException($"Algorithm {algorithmName} is not supported")
                    };
                    //var floydstein = new FloydSteinbergDitheringRGBByte(ColorFunction);
                    byte[,,] bytes = ReadBitmapToColorBytes(image);
                    var tempImage = new TempByteImageFormat(bytes);
                    ditherer.DoDithering(tempImage, approximater);
                    WriteToBitmap(image, tempImage.GetPixelChannels, worker, approximater);
#if DEBUG
                    stopwatch.Stop(); // Остановка таймера
                    Debug.WriteLine($"Время выполнения: {stopwatch.Elapsed.TotalMilliseconds} мс"); // Вывод результата

#endif
                    return image;
                }
                catch (Exception ex)
                {
                    throw new DitheringException("Dithering failed.", ex);
                }
            }

            private void ColorFunction(in byte[] input, ref byte[] output, ColorApproximater approximater)
            {
                if (ColorFunctionMode == 0)
                    TrueColorBytesToWebSafeColorBytes(input, ref output);
                else
                    TrueColorBytesToPalette(input, ref output, approximater);
            }

            private void TrueColorBytesToWebSafeColorBytes(in byte[] input, ref byte[] output)
            {
                for (int i = 0; i < input.Length; i++)
                {
                    output[i] = (byte)(Math.Round(input[i] / 51.0) * 51);
                }
            }

            private static void TrueColorBytesToPalette(in byte[] input, ref byte[] output, ColorApproximater approximater)
            {
                output = approximater.Convert((input[0], input[1], input[2]));
            }

            private byte[,,] ReadBitmapToColorBytes(Image<Rgb24> image)
            {
#if DEBUG
                var stopwatch = Stopwatch.StartNew(); // Запуск таймера
#endif
                int width = image.Width;
                int height = image.Height;
                byte[,,] result = new byte[width, height, 3];
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < height; y++)
                    {
                        Span<Rgb24> rowSpan = accessor.GetRowSpan(y);
                        Span<byte> rowBytes = MemoryMarshal.AsBytes(rowSpan);

                        for (int x = 0; x < width; x++)
                        {
                            int offset = x * 3;
                            result[x, y, 0] = rowBytes[offset];     // R
                            result[x, y, 1] = rowBytes[offset + 1]; // G
                            result[x, y, 2] = rowBytes[offset + 2]; // B
                        }
                    }
                });
#if DEBUG
                stopwatch.Stop(); // Остановка таймера
                Debug.WriteLine($"Время выполнения: {stopwatch.Elapsed.TotalMilliseconds} мс"); // Вывод результата

#endif
                return result;
            }

            private void WriteToBitmap(Image<Rgb24> bitmap, Func<int, int, byte[]> reader, BinaryWorker worker, ColorApproximater approximater)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    for (int y = 0; y < bitmap.Height; y++)
                    {

                        byte[] read = reader(x, y);
                        worker.FileValues.Add(approximater.GetColor((read[0], read[1], read[2])));
                    }
                }
                worker.Write((ushort)bitmap.Width, (ushort)bitmap.Height);
            }
        }
        #endregion
        /// <summary>
        /// This code is the intellectual property of MKSO4KA (Mixailkin) and is protected by copyright law.
        /// Unauthorized copying, modification, or distribution of this code without explicit permission from the author is strictly prohibited.
        /// 
        /// License: GNU General Public License v3.0
        /// 
        /// For inquiries, please contact the author at: mk06ru@gmail.com
        /// </summary>
        public partial class ColorApproximater
        {
            public static string[] TilesDefault
            {
                get { return _tilesDefault; }
                set { _tilesDefault = value; }
            }

            public string TilesPath
            {
                get { return _tilesPath; }
                set { _tilesPath = value; }
            }
            /// <summary>
            /// Call:
            /// <br></br>     Color color = Color.White;
            /// <br></br>     ColorApproximater Approximater = new ColorApproximater(list_colors);
            /// <br></br>     var cl = Approximater.Convert(color);
            /// </summary>
            public ColorApproximater(string path2tiles = "", int maxlenght = 1000)
            {
                TilesPath = path2tiles;
                _maxLenght = maxlenght;
                if (TilesPath == "")
                {
                    _pixels = new Pixels(TilesDefault);
                }
                else
                {
                    if (!FileDirectoryTools.IsFileAccessible(TilesPath))
                    {
                        throw new FileNotFoundException("Файл не доступен или не существует: " + TilesPath);
                    }
                    _pixels = new Pixels(File.ReadAllLines(TilesPath));
                }

                _hueRgbRange = SetHueEqRgb();
                _findedColors = new List<(byte, byte, byte)>();
                _convertedColors = new List<(byte, byte, byte)>();
                _colors = new List<List<(byte, byte, byte)>>();
                _list_colors = _pixels.GetColors().ToArray();
                SetColors();
            }
            public ColorApproximater(Pixels colorslist, string Path = "", int maxlenght = 1000)
            {
                _pixels = colorslist;
                _maxLenght = maxlenght;
                _hueRgbRange = SetHueEqRgb();
                _findedColors = new List<(byte, byte, byte)>();
                _convertedColors = new List<(byte, byte, byte)>();
                _colors = new List<List<(byte, byte, byte)>>();
                _list_colors = colorslist.GetColors().ToArray();
                SetColors();
            }
            public (bool, bool, ushort, byte) GetColor((byte, byte, byte) a)
            {
                return _pixels.GetPixels()[_pixels.GetColors().IndexOf(a)];
            }

            /// <summary>
            /// The Convert method takes a Color object as an argument and returns a Color? object.
            /// <br></br>Inside the method, an empty Diffs list is created that will store the differences between the color of the color and each color in the array obtained using the GetColors method and the index obtained using the GetIndexOfColor method.
            /// <br></br> Next, a loop occurs in which for each color from the array the difference is calculated using the ColorDiff method and added to the Diffs list.
            /// <br></br> Finally, the method returns the color from the array that has the minimum color difference.
            /// <br></br>
            /// </summary>
            /// <param name="color"></param>
            /// <returns></returns>
            public byte[] Convert((byte, byte, byte) color)
            {
                //SetColors();
                int index;
                if ((index = _findedColors.IndexOf(color)) != -1)
                {
                    return new byte[] { _convertedColors[index].Item1, _convertedColors[index].Item2, _convertedColors[index].Item3 };
                }
                List<double> Diffs = new List<double>();
                int indas = GetIndexOfColor(color);
                var Array = GetColors(indas);
                foreach (var item in Array)
                {
                    Diffs.Add(ColorDiff(item, color));
                }

                _findedColors.Add(color);
                var color2 = Array[Diffs.IndexOf(Diffs.Min())];
                _convertedColors.Add(color2);
                if (_findedColors.Count == _maxLenght)
                {
                    ResetAHalfOfConverted();
                }
                return new byte[] { color2.Item1, color2.Item2, color2.Item3 };
            }

            private static (byte, byte, byte)[] ColorsToBytes(System.Drawing.Color[] colors)
            {
                (byte, byte, byte)[] result = new (byte, byte, byte)[colors.Length];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = (colors[i].R, colors[i].G, colors[i].B);
                }
                return result;
            }


            public static int MaxLenght
            {
                get { return _maxLenght; }
            }


            private void Reset()
            {
                _findedColors.Clear();
                _convertedColors.Clear();
            }


            /// <summary>
            ///The Colors class contains several static methods for working with colors.
            /// </summary>
            #region Colors
            /// <summary>
            ///The SetHueEqRgb method creates a new list of color lists, where each inner list contains colors corresponding to a specific degree range. This method uses the GetColorsFromHueRange method.
            /// </summary>
            /// <returns></returns>
            private static List<List<(byte, byte, byte)>> SetHueEqRgb()
            {
                (float, float) Hue;
                List<List<(byte, byte, byte)>> list = new List<List<(byte, byte, byte)>>(24);
                for (int i = 0; i < HueRange.Count; i += 1)
                {
                    Hue = HueRange[i];
                    list.Add(GetColorsFromHueRange(Hue));
                }
                list[HueRange.Count - 1].RemoveAt(list[HueRange.Count - 1].Count - 1);
                return list;
            }
            private float GetHue(byte r, byte g, byte b)
            {

                if (r == g && g == b)
                    return 0f;

                MinMaxRgb(out int min, out int max, r, g, b);

                float delta = max - min;
                float hue;

                if (r == max)
                    hue = (g - b) / delta;
                else if (g == max)
                    hue = (b - r) / delta + 2f;
                else
                    hue = (r - g) / delta + 4f;

                hue *= 60f;
                if (hue < 0f)
                    hue += 360f;

                return hue;
            }
            private static void MinMaxRgb(out int min, out int max, byte r, byte g, byte b)
            {
                if (r > g)
                {
                    max = r;
                    min = g;
                }
                else
                {
                    max = g;
                    min = r;
                }
                if (b > max)
                {
                    max = b;
                }
                else if (b < min)
                {
                    min = b;
                }
            }
            /// <summary>
            ///The SetColors method initializes the _colors list and fills it with the colors from _list_colors. It then sorts each internal list by its color degree value
            /// </summary>
            private void SetColors()
            {
                for (int i = 0; i < 24; i += 1)
                {
                    _colors.Add(new List<(byte, byte, byte)>());
                }
                foreach ((byte, byte, byte) color in _list_colors)
                {
                    _colors[GetIndexOfColor(color)].Add((color.Item1, color.Item2, color.Item3));
                }
                for (int ind = 0; ind < _colors.Count; ind += 1)
                {
                    _colors[ind] = _colors[ind].OrderBy(x => GetHue(x.Item1, x.Item2, x.Item3)).ToList();
                }
                for (int i = 0; i < GetColors().Count; i++)
                {
                    if (GetColors(i).Count == 0)
                        skip_colorslist.Add(i);
                }
            }
            private List<List<(byte, byte, byte)>> GetColors()
            {
                return _colors;
            }
            private List<(byte, byte, byte)> GetColors(int id)
            {
                return _colors[id];
            }
            #endregion
            /// <summary>
            ///The Conversation class contains several static methods for converting colors from the hsl color model.
            /// </summary>


            private static System.Drawing.Color HSLToRGB(double H, double S = 1, double L = 0.5)
            {
                return Conversation.ToRGB(H, S, L);
            }
            private static (byte, byte, byte) HSLToBytes(double H, double S = 1, double L = 0.5)
            {
                return Conversation.ToBytes(H, S, L);
            }
            private void ResetAHalfOfConverted()
            {
                _findedColors = _findedColors.Skip(MaxLenght / 2).ToList();
                _convertedColors = _convertedColors.Skip(MaxLenght / 2).ToList();
            }

            private static float ColorDiff(System.Drawing.Color c1, System.Drawing.Color c2)
            {
                return (float)Math.Sqrt((c1.R - c2.R) * (c1.R - c2.R)
                                     + (c1.G - c2.G) * (c1.G - c2.G)
                                     + (c1.B - c2.B) * (c1.B - c2.B));
            }
            private static float ColorDiff((byte, byte, byte) c1, (byte, byte, byte) c2)
            {
                return (float)Math.Sqrt((c1.Item1 - c2.Item1) * (c1.Item1 - c2.Item1)
                                     + (c1.Item2 - c2.Item2) * (c1.Item2 - c2.Item2)
                                     + (c1.Item3 - c2.Item3) * (c1.Item3 - c2.Item3));
            }

            private int GetIndexOfColor(System.Drawing.Color color)
            {
                List<float> diffs = new List<float>(HueRange.Count * 16);
                List<float> tmp = new List<float> {
               720, 720, 720, 720,
               720, 720, 720, 720,
               720, 720, 720, 720,
               720, 720, 720, 720};
                for (int rangeInd = 0; rangeInd < HueRange.Count; rangeInd++)
                {
                    if (skip_colorslist.Contains(rangeInd))
                    {
                        diffs.AddRange(tmp);
                        continue;
                    }
                    foreach (var item in _hueRgbRange[rangeInd])
                    {
                        diffs.Add(ColorDiff(color, System.Drawing.Color.FromArgb(item.Item1, item.Item2, item.Item3)));
                    }
                }
                return (int)Math.Floor((double)diffs.IndexOf(diffs.Min()) / 16);
            }
            private int GetIndexOfColor((byte, byte, byte) color)
            {
                List<float> diffs = new List<float>(HueRange.Count * 16);
                List<float> tmp = new List<float> {
               720, 720, 720, 720,
               720, 720, 720, 720,
               720, 720, 720, 720,
               720, 720, 720, 720};
                for (int rangeInd = 0; rangeInd < HueRange.Count; rangeInd++)
                {
                    if (skip_colorslist.Contains(rangeInd))
                    {
                        diffs.AddRange(tmp);
                        continue;
                    }
                    foreach (var item in _hueRgbRange[rangeInd])
                    {
                        diffs.Add(ColorDiff(color, item));
                    }
                }
                return (int)Math.Floor((double)diffs.IndexOf(diffs.Min()) / 16);
            }
            private static List<(byte, byte, byte)> GetColorsFromHueRange((float, float) hue)
            {
                float min = hue.Item1, max = hue.Item2;
                List<(byte, byte, byte)> list = new List<(byte, byte, byte)>(16);
                if (min == 352.5)
                {
                    for (float degree = min; degree < 360; degree += 1)
                    {
                        list.Add(HSLToBytes(degree));
                    }
                    for (float degree = 0.5f; degree <= hue.Item2; degree += 1)
                    {
                        list.Add(HSLToBytes(degree));
                    }
                }
                else
                {
                    for (float degree = hue.Item1; degree <= hue.Item2; degree += 1)
                    {
                        list.Add(HSLToBytes(degree));
                    }
                }
                return list;
            }

            public static bool SetGray()
            {
                if (_tilesDefault.Length == 0) return false;
                List<string> grayPalette = new List<string>();
                (byte, byte, byte) color;
                string[] palette = _tilesDefault.ToArray();
                foreach (var tile in palette)
                {
                    color = Pixel.ToBytes(tile.Split(':')[3]) ?? (0, 0, 0);
                    // Проверка, является ли цвет градацией серого
                    if ((color.Item1 == color.Item2 && color.Item2 == color.Item3))
                    {
                        grayPalette.Add(tile);
                    }
                }
                _tilesDefault = grayPalette.ToArray();
                return true;
            }

        }
    }
}


