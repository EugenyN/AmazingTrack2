using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Cell-occupancy queries over live block entities: whether a solid
    /// part occupies a given XZ cell and which block entity owns it.
    /// Shared by the track generator (chain cells, golden jump pairs) and
    /// available to any gameplay code that reasons about cells. The
    /// projection builds a filter once per call - call it from build-time
    /// or rare paths, not from per-frame hot loops; when used per frame,
    /// cache the filter at the call site and reuse it across calls.
    /// </summary>
    public static class TrackQueriesHelper
    {
        /// <summary>
        /// True when a solid block part occupies the given XZ cell (y is
        /// ignored): iterates live blocks, projects the cell onto each
        /// slab's (Forward, Right) frame and matches the part index. Blocks
        /// sit at y=0, one unit per part along -Right from the head cell.
        /// </summary>
        public static bool HasSolidCell(EcsWorld world, Vector3 cell)
        {
            var blocks = world.Filter<BlockComponent>().End();
            var blockPool = world.GetPool<BlockComponent>();
            foreach (int blockEntity in blocks)
            {
                ref var block = ref blockPool.Get(blockEntity);
                if (BlockFootprintHelper.PartAt(ref block, cell) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Finds the entity of the block whose solid part occupies the given
        /// XZ cell (y ignored); -1 when no solid part covers the cell. The
        /// same projection HasSolidCell uses, but returning the entity.
        /// </summary>
        public static int BlockEntityAt(EcsWorld world, Vector3 cell)
        {
            var blocks = world.Filter<BlockComponent>().End();
            var blockPool = world.GetPool<BlockComponent>();
            foreach (int blockEntity in blocks)
            {
                ref var block = ref blockPool.Get(blockEntity);
                if (BlockFootprintHelper.PartAt(ref block, cell) >= 0)
                    return blockEntity;
            }
            return -1;
        }
    }
}
