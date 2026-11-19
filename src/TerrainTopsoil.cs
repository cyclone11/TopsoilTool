using Mafi;
using Mafi.Base;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;

namespace TopsoilTool
{
    /// <summary>
    /// 地形换土逻辑。
    /// 已验证：LooseProductQuantity.ToTerrainThickness()、TerrainTile.DepositMaterialOnTop(...)
    /// 待确认：按坐标取 TerrainTile、读取/移除顶层材料。
    /// </summary>
    internal static class TerrainTopsoil
    {
        public static int Apply(TerrainManager terrain, ProtosDb protos, RectangleTerrainArea2i area)
        {
            // 土：游戏里的散料产品（dirt）。TODO(API): 确认 Ids.Products.Dirt 是否存在。
            LooseProductProto dirt = protos.GetOrThrow<LooseProductProto>(Ids.Products.Dirt);
            TerrainMaterialThickness dirtThickness =
                new LooseProductQuantity(dirt, Quantity.MaxValue).ToTerrainThickness();

            int changed = 0;

            // TODO(API): 确认 RectangleTerrainArea2i 的 Min/Max 成员名（Min/Max/MinCoord/MaxCoord）。
            for (int y = area.Min.Y; y <= area.Max.Y; y++)
            {
                for (int x = area.Min.X; x <= area.Max.X; x++)
                {
                    var coord = new Tile2i(x, y);

                    // TODO(API): 确认取瓦片的方法（TerrainManager.GetTile / TryGetTile）。
                    TerrainTile? tile = terrain.GetTile(coord);
                    if (tile == null) continue;

                    // TODO(API): 确认海洋判断签名（TerrainManager.IsOcean(Tile2iIndex)）。
                    if (terrain.IsOcean(coord)) continue;

                    ReplaceTopWith(tile, dirtThickness);
                    changed++;
                }
            }

            return changed;
        }

        /// <summary>
        /// 把一格的最上层地形材料替换成土，厚度保持不变、高度不变。
        /// </summary>
        private static void ReplaceTopWith(TerrainTile tile, TerrainMaterialThickness dirt)
        {
            // TODO(API): 以下两步需要按实际 API 校准，二选一：
            //
            // 方案 A（油漆式，推荐）：直接改顶层材料，不动厚度。
            //   tile.SetSurfaceMaterial(dirt.Material);  // 名称待确认
            //
            // 方案 B（移除 + 铺土）：
            //   TerrainMaterialThickness top = tile.GetTopMaterial();      // 名称待确认
            //   tile.RemoveMaterialFromTop(top.Thickness);                  // 名称待确认
            //   tile.DepositMaterialOnTop(dirt.Material, top.Thickness,
            //       doNotRaiseEvents: true, doNotDisruptTerrain: true, doNotChangeHeight: true);
            //
            // 目前先抛异常，避免误以为已实现。
            throw new System.NotImplementedException(
                "TerrainTopsoil.ReplaceTopWith: 需要按游戏程序集校准顶层材料 API（见 README 第 5 节）。");
        }
    }
}
