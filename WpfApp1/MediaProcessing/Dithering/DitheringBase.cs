
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PixArtConverter.MediaProcessing.Converters;
using PixArtConverter.DataTools;
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
    /// Базовый абстрактный класс для реализации алгоритмов дизеринга.
    /// Инкапсулирует общую логику обработки изображения: итерацию по пикселям,
    /// применение цветовой функции, расчет и распространение ошибок квантования.
    /// Наследники должны реализовать специфичную для алгоритма логику распределения ошибок.
    /// </summary>
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
    /// Основной класс для выполнения адаптивного дизеринга изображений.
    /// Автоматически выбирает оптимальный алгоритм на основе анализа изображения,
    /// выполняет конвертацию цветов в целевую палитру и сохраняет результат.
    /// Интегрирует компоненты анализа, селекции алгоритмов и непосредственного применения дизеринга.
    /// </summary>
    public class OptimalDitheringSelector
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
                    "Burkes" => new BurkesDitheringRGBByte(ColorFunction),
                    "Sierra3" => new Sierra3DitheringRGBByte(ColorFunction),
                    "SierraLite" => new SierraLiteDitheringRGBByte(ColorFunction),
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
                    Span<byte> rowBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(rowSpan);

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

    /// <summary>
    /// Специализированное исключение для обработки ошибок в процессе дизеринга.
    /// Содержит контекст исключения и кастомное сообщение об ошибке.
    /// </summary>
    public class DitheringException : Exception
    {
        public DitheringException(string message, Exception inner) : base(message, inner) { }
    }
}
