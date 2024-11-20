using System.Collections.Generic;

namespace CraftSharp.Resource
{
    public class BlockStateModel
    {
        public readonly BlockGeometry[] Geometries;
        public readonly RenderType RenderType;
        public readonly OffsetType OffsetType;

        public BlockStateModel(List<BlockGeometry> geometries, RenderType renderType, OffsetType offsetType)
        {
            Geometries = geometries.ToArray();
            RenderType = renderType;
            OffsetType = offsetType;
        }
    }
}