using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Robust.Client.Map
{
    internal sealed class ClydeTileDefinitionManager : TileDefinitionManager, IClydeTileDefinitionManager
    {
        [Dependency] private readonly IResourceCache _resourceCache = default!;

        private Texture? _tileTextureAtlas;

        public Texture TileTextureAtlas => _tileTextureAtlas ?? Texture.Transparent;

        private readonly Dictionary<ushort, Box2> _tileRegions = new();

        public Box2? TileAtlasRegion(Tile tile)
        {
            if (_tileRegions.TryGetValue(tile.TypeId, out var region))
            {
                return region;
            }

            return null;
        }

        public override void Initialize()
        {
            base.Initialize();

            _genTextureAtlas();
        }

        private void _genTextureAtlas()
        {
            var defList = TileDefs.Where(t => !string.IsNullOrEmpty(t.SpriteName)).ToList();

            // If there are no tile definitions, we do nothing.
            if (defList.Count <= 0)
                return;

            const int tileSize = EyeManager.PixelsPerMeter;

            var dimensionX = (int) Math.Ceiling(Math.Sqrt(defList.Count));
            var dimensionY = (int) Math.Ceiling((float) defList.Count / dimensionX);

            var sheet = new Image<Rgba32>(dimensionX * tileSize, dimensionY * tileSize);

            for (var i = 0; i < defList.Count; i++)
            {
                var def = defList[i];
                var column = i % dimensionX;
                var row = i / dimensionX;

                Image<Rgba32> image;
                using (var stream = _resourceCache.ContentFileRead(new ResourcePath(def.Path) / $"{def.SpriteName}.png"))
                {
                    image = Image.Load<Rgba32>(stream);
                }

                if (image.Width != tileSize || image.Height != tileSize)
                {
                    throw new NotSupportedException("Unable to use tiles with a dimension other than 32x32.");
                }

                var point = new Vector2i(column * tileSize, row * tileSize);

                image.Blit(new UIBox2i(0, 0, image.Width, image.Height), sheet, point);

                var w = (float) sheet.Width;
                var h = (float) sheet.Height;

                _tileRegions.Add(def.TileId,
                    Box2.FromDimensions(
                        point.X / w, (h - point.Y - EyeManager.PixelsPerMeter) / h,
                        tileSize / w, tileSize / h));
            }

            _tileTextureAtlas = Texture.LoadFromImage(sheet, "Tile Atlas");
        }
    }
}
