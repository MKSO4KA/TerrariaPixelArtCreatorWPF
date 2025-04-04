using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
using System.Threading;
using System.Runtime.InteropServices;
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
namespace PixArtConverter.MediaProcessing.EdgeDetecting
{
    /// <summary>
    /// Основной класс для обработки изображений с целью выделения границ объектов.
    /// Использует оператор Собеля для вычисления градиентов, бинаризацию и фильтрацию регионов.
    /// </summary>
    public class GradientBoundaryProcessor
    {
        private static readonly float[,] SobelX =
        {
            { -1, 0, 1 },
            { -2, 0, 2 },
            { -1, 0, 1 }
        };

        private static readonly float[,] SobelY =
        {
            { -1, -2, -1 },
            {  0,  0,  0 },
            {  1,  2,  1 }
        };
        /// <summary>
        /// Асинхронно обрабатывает изображение и возвращает координаты границ объектов
        /// </summary>
        /// <param name="image">Входное изображение в формате Rgb24</param>
        /// <param name="sensitivity">Чувствительность детектора (0.01-0.5)</param>
        /// <param name="minRegionSize">Минимальный размер сохраняемых регионов (в пикселях)</param>
        /// <returns>Массив координат (X, Y) граничных точек</returns>
        /// <exception cref="ImageProcessingException">Выбрасывается при ошибках обработки</exception>
        public static async Task<(int X, int Y)[]> ProcessImageAsync(
            Image<Rgb24> image, // Принимаем готовое изображение
            float sensitivity = 0.15f,
            int minRegionSize = 30)
        {
            if (sensitivity < 0.01f || sensitivity > 0.5f)
                throw new ArgumentOutOfRangeException(nameof(sensitivity), "Range: 0.01-0.5");

            if (minRegionSize < 0)
                throw new ArgumentOutOfRangeException(nameof(minRegionSize), "Must be non-negative");

            try
            {
                var (gradientMap, maxGradient) = await Task.Run(() => CalculateGradients(image));
                var binaryMask = await Task.Run(() => CreateBinaryMask(gradientMap, maxGradient, sensitivity));
                await Task.Run(() => FilterSmallRegions(binaryMask, image.Width, image.Height, minRegionSize));

                return await Task.Run(() => GetBoundaryCoordinatesOptimized(binaryMask, image.Width, image.Height));
            }
            catch (Exception ex)
            {
                throw new ImageProcessingException("Image processing failed", ex);
            }
        }

        private static (float[] gradients, float maxGradient) CalculateGradients(Image<Rgb24> image)
        {
            int width = image.Width;
            int height = image.Height;
            float[] gradients = new float[width * height];
            float maxGradient = 0;

            // Оптимизированное чтение пикселей с использованием ProcessPixelRows
            var pixelVectorBuffer = new Vector3[width * height];

            // Безопасное преобразование через буферный массив
            Rgb24[] pixelArray = new Rgb24[width * height];
            image.CopyPixelDataTo(pixelArray); // Копируем пиксели в массив

            // Параллельное преобразование Rgb24 -> Vector3
            Parallel.For(0, pixelArray.Length, i =>
            {
                Rgb24 pixel = pixelArray[i];
                pixelVectorBuffer[i] = new Vector3(pixel.R, pixel.G, pixel.B);
            });

            // Векторизованные вычисления
            Parallel.For(1, height - 1, y =>
            {
                float localMax = 0;
                int yOffset = y * width;

                for (int x = 1; x < width - 1; x++)
                {
                    Vector3 sumX = Vector3.Zero;
                    Vector3 sumY = Vector3.Zero;

                    for (int ky = -1; ky <= 1; ky++)
                    {
                        int rowOffset = (y + ky) * width;
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int index = rowOffset + x + kx;
                            Vector3 pixel = pixelVectorBuffer[index];

                            float sx = SobelX[ky + 1, kx + 1];
                            float sy = SobelY[ky + 1, kx + 1];

                            sumX += pixel * sx;
                            sumY += pixel * sy;
                        }
                    }

                    Vector3 gradientVec = Vector3.SquareRoot(sumX * sumX + sumY * sumY);
                    float gradient = (gradientVec.X + gradientVec.Y + gradientVec.Z) / 3;

                    int centerIndex = yOffset + x;
                    gradients[centerIndex] = gradient;

                    if (gradient > localMax) localMax = gradient;
                }

                if (localMax > maxGradient)
                    Interlocked.Exchange(ref maxGradient, Math.Max(maxGradient, localMax));
            });

            return (gradients, maxGradient);
        }

        private static bool[] CreateBinaryMask(float[] gradients, float maxGradient, float sensitivity)
        {
            float threshold = sensitivity * maxGradient;
            bool[] mask = new bool[gradients.Length];

            Parallel.For(0, gradients.Length, i =>
            {
                mask[i] = gradients[i] > threshold;
            });

            return mask;
        }

        private static void FilterSmallRegions(bool[] mask, int width, int height, int minSize)
        {
            int[] labels = new int[mask.Length];
            UnionFind uf = new UnionFind(width * height);
            int currentLabel = 1;

            // Первый проход с оптимизированным Union-Find
            Parallel.For(0, height, y =>
            {
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    int index = rowStart + x;
                    if (!mask[index]) continue;

                    int left = x > 0 ? labels[index - 1] : 0;
                    int top = y > 0 ? labels[index - width] : 0;

                    if (left == 0 && top == 0)
                    {
                        labels[index] = Interlocked.Increment(ref currentLabel);
                    }
                    else
                    {
                        int minLabel = Math.Min(
                            left != 0 ? left : int.MaxValue,
                            top != 0 ? top : int.MaxValue);

                        labels[index] = minLabel;
                        if (left != 0 && top != 0 && left != top)
                            uf.Union(left, top);
                    }
                }
            });

            // Второй проход с массивами вместо Dictionary
            int[] regionSizes = new int[currentLabel + 1];
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == 0) continue;
                int root = uf.Find(labels[i]);
                Interlocked.Increment(ref regionSizes[root]);
            }

            Parallel.For(0, labels.Length, i =>
            {
                if (labels[i] != 0 && regionSizes[uf.Find(labels[i])] < minSize)
                    mask[i] = false;
            });
        }

        private static (int X, int Y)[] GetBoundaryCoordinatesOptimized(bool[] mask, int width, int height)
        {
            int count = 0;
            var temp = new (int, int)[mask.Length];

            Parallel.For(0, height, y =>
            {
                int rowStart = y * width;
                int localCount = 0;
                (int, int)[] localTemp = new (int, int)[width]; // Локальный буфер для строки

                for (int x = 0; x < width; x++)
                {
                    if (mask[rowStart + x])
                    {
                        localTemp[localCount++] = (x, y);
                    }
                }

                if (localCount > 0)
                {
                    int globalIndex = Interlocked.Add(ref count, localCount);
                    Array.Copy(localTemp, 0, temp, globalIndex - localCount, localCount);
                }
            });

            Array.Resize(ref temp, count);
            return temp;
        }
    }
    /// <summary>
    /// Реализация структуры данных "Система непересекающихся множеств" (Union-Find)
    /// для эффективного объединения и поиска связанных компонентов
    /// </summary>
    public class UnionFind
    {
        private readonly int[] parent;

        public UnionFind(int size)
        {
            parent = new int[size + 1]; // +1 для обработки меток, начинающихся с 1
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
        }

        public int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]]; // Path compression
                x = parent[x];
            }
            return x;
        }

        public void Union(int x, int y)
        {
            int rootX = Find(x);
            int rootY = Find(y);
            if (rootX != rootY) parent[rootY] = rootX;
        }
    }
    /// <summary>
    /// Специальное исключение для ошибок обработки изображений
    /// </summary>
    public class ImageProcessingException : Exception
    {
        public ImageProcessingException(string message, Exception inner)
            : base(message, inner) { }
    }
}