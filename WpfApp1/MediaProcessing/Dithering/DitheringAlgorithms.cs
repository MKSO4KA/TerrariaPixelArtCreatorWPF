using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Processing;
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
namespace PixArtConverter.MediaProcessing.Dithering 
{
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
    /// Определяет контракт для работы с изображениями различных форматов.
    /// Предоставляет методы для доступа к пиксельным данным, управления каналами,
    /// вычисления и применения ошибок квантования при дизеринге.
    /// </summary>
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

    /// <summary>
    /// Реализация формата изображения для 8-битных данных (байтовый формат).
    /// Поддерживает хранение данных в 3D-массиве (x, y, канал) или 1D-массиве,
    /// оптимизирован для быстрых операций чтения/записи пикселей.
    /// </summary>
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

    /// <summary>
    /// Статический класс-селектор для выбора оптимального алгоритма дизеринга
    /// на основе результатов анализа изображения. Содержит словарь 
    /// для сопоставления названий алгоритмов с их реализациями.
    /// </summary>
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
                { "Burkes", typeof(BurkesDitheringRGBByte) },
                { "Sierra3", typeof(Sierra3DitheringRGBByte) },
                { "SierraLite", typeof(SierraLiteDitheringRGBByte) }
            };
    }

    /// <summary>
    /// Анализатор изображений, вычисляющий ключевые характеристики:
    /// контраст, уровень шума, наличие градиентов и высокочастотных деталей.
    /// Реализует IDisposable для управления ресурсами ImageSharp.
    /// </summary>
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

            clone.Mutate(c => c.DetectEdges(SixLabors.ImageSharp.Processing.Processors.Convolution.EdgeDetector2DKernel.SobelKernel));

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

            clone.Mutate(c => c.DetectEdges(SixLabors.ImageSharp.Processing.Processors.Convolution.EdgeDetector2DKernel.SobelKernel));

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

    /// <summary>
    /// Контейнер для хранения результатов анализа изображения.
    /// Содержит метаданные (размеры) и вычисленные характеристики:
    /// контраст, уровень шума, флаги структурных особенностей.
    /// </summary>
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

}
