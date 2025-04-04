using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using PixArtConverter.MediaProcessing.Dithering;
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

namespace PixArtConverter.MediaProcessing.Converters
{
    internal class PhotoTileConverter
    {
        private readonly string _path;
        private readonly string _totalpath;
        private readonly string _tilespath;

        public PhotoTileConverter(string PhotoPath, string TilesPath = "", string TotalPath = "")
        {
            _path = PhotoPath;
            _tilespath = TilesPath;
            _totalpath = (string.IsNullOrEmpty(TotalPath) || FileDirectoryTools.IsFileAccessible(TotalPath)) ?
                System.Environment.GetEnvironmentVariable("USERPROFILE") + "\\Documents\\PixelArtCreatorByMixailka\\photo.txt" :
                TotalPath;
        }

        public async Task Convert(int i, Image<Rgb24> image = null, bool UserTilesOn = false)
        {
            ColorApproximater approximater = new ColorApproximater(_tilespath);

            bool isImageOwned = image == null;
            image ??= Image.Load<Rgb24>(_path);

            try
            {
                await Task.Run(() =>
                {
                    var Dither = new OptimalDitheringSelector();
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
