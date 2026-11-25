using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Footprint test for a composite block slab: whether the XZ projection
    /// of a point (the ball's center) lies within the block's footprint.
    /// The slab spans Parts unit cells along -Right from the head cell. The
    /// hole cell (HolePart &ge; 0) is excluded from the footprint,
    /// shrunk by the seam margin so the hole edge behaves exactly like a
    /// track edge: a center still on the solid part counts as grounded, a
    /// center that crosses into the hole cell does not.
    /// </summary>
    public static class BlockFootprintHelper
    {
        /// <summary>Seam tolerance, units: the same margin the track edges use.</summary>
        private const float Margin = 0.05f;

        /// <summary>
        /// Whether the XZ projection of the point lies within the block's
        /// footprint: |along| &le; half length and |lateral| &le; half span
        /// (Parts unit cells), each with the seam margin - minus the hole
        /// cell (shrunk by the margin) when the block has a hole.
        /// </summary>
        public static bool Contains(ref BlockComponent block, Vector3 point, float blockLength)
        {
            var toPoint = point - block.Pos;
            float along = Vector3.Dot(toPoint, block.Forward);
            float lateral = Vector3.Dot(toPoint, block.Right);
            float halfSpan = block.Parts * blockLength * 0.5f;
            if (Mathf.Abs(along) > blockLength * 0.5f + Margin
                || Mathf.Abs(lateral) > halfSpan + Margin)
                return false;

            if (block.HolePart < 0)
                return true;

            // Hole cell center: part i sits at lateral offset
            // ((Parts - 1) * 0.5 - i) * blockLength along Right from the
            // block center. The cell counts as missing ground once the
            // point is inside it beyond the seam margin.
            float holeCenter = ((block.Parts - 1) * 0.5f - block.HolePart) * blockLength;
            return Mathf.Abs(lateral - holeCenter) > blockLength * 0.5f - Margin;
        }

        /// <summary>
        /// Index of the slab part covering the given XZ cell (y ignored),
        /// or -1 when the cell is outside the slab or on its hole part.
        /// Projection onto the slab's (Forward, Right) frame: the forward
        /// band is |along| &le; 0.5 (one cell deep), parts count along
        /// -Right from the head cell. The cell test is exact (no seam
        /// margin): generation-time queries ask about cell occupancy, not
        /// about the ball's grounded state.
        /// </summary>
        public static int PartAt(ref BlockComponent block, Vector3 cell)
        {
            var rel = cell - block.Pos;
            rel.y = 0f;
            float alongRight = Vector3.Dot(rel, block.Right);
            float alongForward = Vector3.Dot(rel, block.Forward);
            if (Mathf.Abs(alongForward) > 0.5f)
                return -1; // not in this slab's forward band
            float partF = (block.Parts - 1) * 0.5f - alongRight;
            int part = Mathf.RoundToInt(partF);
            if (part < 0 || part >= block.Parts)
                return -1; // outside the slab
            if (part == block.HolePart)
                return -1; // the hole part is not solid
            return part;
        }
    }
}
