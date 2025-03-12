using Emgu.CV.CvEnum;
using Emgu.CV;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using PixArtConverter.MediaProcessing.Converters;

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

namespace PixArtConverter.MediaProcessing
{
    public class VideoProcessor
    {
        private readonly string VideoPath;
        private readonly string TilesPath;
        private readonly string TotalPath;
        private readonly byte FRAMERATE;
        private readonly int semafors;

        public VideoProcessor(string videoPath, string totalPath, byte frameRate = 30, int semafors = 1, string tilesPath = "")
        {
            TilesPath = tilesPath;
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

                        if (DataTools.FileDirectoryTools.IsFileAccessible(filePath) || inds.ContainsKey(index))
                            throw new Exception("File Exists");

                        if (!inds.TryAdd(index, true))
                            throw new Exception("inds error");

                        PhotoTileConverter converter = new PhotoTileConverter(VideoPath, TilesPath, filePath);
                        await converter.Convert(index, frame, MainWindow.UserTiles);
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
}
