
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PixArtConverter.MediaProcessing.Converters;
using System.IO;
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

namespace PixArtConverter.MediaProcessing
{
    public class PhotoProcessor
    {
        private readonly string PhotoPath;
        private readonly string TotalPath;
        private readonly string TilesPath;
        private readonly int semafors;

        public PhotoProcessor(string photoPath, string totalPath, string tilesPath = "")
        {
            TilesPath = tilesPath;
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
                        if (!FileDirectoryTools.IsFileAccessible(filePath))
                        {
                            throw new FileNotFoundException("Файл не доступен или не существует: " + filePath);
                        }
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

                        PhotoTileConverter converter = new PhotoTileConverter(PhotoPath, TilesPath, filePath);
                        await converter.Convert(index, frame, MainWindow.UserTiles);
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
}
