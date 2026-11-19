using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Mafi;
using Mafi.Base;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Terrain.Props;

namespace TopsoilTool
{
    [GlobalDependency(RegistrationMode.AsSelf)]
    public sealed class TerrainTopsoil
    {
        internal const int BlockSize = TerrainDesignation.SIZE_TILES;
        internal static readonly RelTile2i BlockExtent = new(BlockSize, BlockSize);

        private static readonly ThicknessTilesF Depth = 1.0.TilesThick();
        private readonly TerrainManager m_terrain;
        private readonly TerrainOccupancyManager m_occupancy;
        private readonly TerrainPropsManager m_props;
        private readonly TerrainMaterialProto m_dirt;
        private readonly ConcurrentQueue<(RectangleTerrainArea2i Area, Action<int, int, int> Completed)> m_pending = new();

        public TerrainTopsoil(TerrainManager terrain, TerrainOccupancyManager occupancy,
            TerrainPropsManager props, ProtosDb protos)
        {
            m_terrain = terrain;
            m_occupancy = occupancy;
            m_props = props;
            m_dirt = protos.GetOrThrow<LooseProductProto>(Ids.Products.Dirt).TerrainMaterial.Value;
        }

        internal void Enqueue(RectangleTerrainArea2i area, Action<int, int, int> completed)
        {
            m_pending.Enqueue((area, completed));
        }

        internal void ProcessPending()
        {
            if (!m_pending.TryDequeue(out var job))
                return;

            int selected = 0;
            int changed = 0;
            foreach (Tile2i origin in EnumerateBlockOrigins(job.Area))
            {
                selected++;
                if (CanConvertBlock(origin) && ReplaceBlock(origin))
                    changed++;
            }

            int rocks = RemoveDecorativeRocks(job.Area);
            Log.Info($"[TopsoilTool] Changed {changed}/{selected} blocks (4×4), removed {rocks} decorative rocks.");
            job.Completed(changed, selected, rocks);
        }

        internal static IEnumerable<Tile2i> EnumerateBlockOrigins(RectangleTerrainArea2i area)
        {
            for (int y = 0; y < area.Size.Y; y += BlockSize)
            {
                for (int x = 0; x < area.Size.X; x += BlockSize)
                    yield return area.Origin + new RelTile2i(x, y);
            }
        }

        internal bool IsBlockInBounds(Tile2i origin)
        {
            return !m_terrain.IsOffLimitsOrInvalid(origin)
                && !m_terrain.IsOffLimitsOrInvalid(origin + BlockExtent - RelTile2i.One)
                && m_terrain.IsValidCoord(origin + BlockExtent);
        }

        internal bool CanConvertBlock(Tile2i origin)
        {
            if (!IsBlockInBounds(origin))
                return false;

            foreach (var tile in new RectangleTerrainArea2i(origin, BlockExtent).EnumerateTilesAndIndices(m_terrain))
            {
                var index = tile.Index;
                if (m_terrain.IsOcean(index) || !m_terrain.IsFlat(index)
                    || m_terrain.HasTileSurface(index) || m_occupancy.IsOccupiedAt(index))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ReplaceBlock(Tile2i origin)
        {
            bool changed = false;
            var dirt = m_dirt.SlimId;
            foreach (var tile in new RectangleTerrainArea2i(origin, BlockExtent).EnumerateTilesAndIndices(m_terrain))
            {
                var top = m_terrain.GetFirstLayerSlimOrNoneNoBedrock(tile.Index);
                if (!top.HasValue || top.SlimId != dirt || top.Thickness < Depth)
                {
                    Replace(m_terrain, tile, dirt);
                    changed = true;
                }
            }
            return changed;
        }

        private int RemoveDecorativeRocks(RectangleTerrainArea2i area)
        {
            int removed = 0;
            foreach (Tile2i tile in area.EnumerateTiles())
            {
                if (!m_props.TerrainTileToProp.TryGetValue(tile.AsSlim, out var id)
                    || !m_props.TerrainProps.TryGetValue(id, out var prop)
                    || !area.ContainsTile(prop.Position.Tile2i)
                    || !IsDecorativeRock(prop.Proto))
                {
                    continue;
                }

                if (m_props.TryRemoveProp(id))
                    removed++;
            }
            return removed;
        }

        internal static bool IsDecorativeRock(TerrainPropProto proto)
        {
            var id = proto.Id;
            return id == Ids.TerrainProps.Stone01
                || id == Ids.TerrainProps.Stone02
                || id == Ids.TerrainProps.Stone03
                || id == Ids.TerrainProps.StoneSharp01;
        }

        internal static void Replace(TerrainManager terrain, Tile2iAndIndex tile, TerrainMaterialSlimId dirt)
        {
            var top = terrain.GetFirstLayerSlimOrNoneNoBedrock(tile.Index);
            if (!top.HasValue)
            {
                terrain.DumpMaterial_NoHeightChange(tile, new TerrainMaterialThicknessSlim(dirt, Depth));
                return;
            }

            if (top.SlimId != dirt)
                terrain.ConvertMaterialInFirstLayer(tile, dirt, Depth, ThicknessTilesF.Zero);

            // Dirt stays on top as each thin underlying layer is converted and merged into it.
            var remaining = Depth - terrain.GetFirstLayerSlimOrNoneNoBedrock(tile.Index).Thickness;
            while (!remaining.IsNotPositive)
            {
                if (!terrain.GetSecondLayerSlimOrNoneNoBedrock(tile.Index).HasValue)
                {
                    terrain.DumpMaterial_NoHeightChange(tile, new TerrainMaterialThicknessSlim(dirt, remaining));
                    return;
                }

                terrain.ConvertMaterialInSecondLayer(tile, dirt, remaining, ThicknessTilesF.Zero);
                remaining = Depth - terrain.GetFirstLayerSlimOrNoneNoBedrock(tile.Index).Thickness;
            }
        }
    }
}
