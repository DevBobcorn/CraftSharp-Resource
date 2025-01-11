using System.Collections.Generic;

namespace CraftSharp.Resource
{
    public class BlockStateModel
    {
        public readonly BlockGeometry[] Geometries;
        public readonly RenderType RenderType;
        public readonly OffsetType OffsetType;
        public readonly ResourceLocation ParticleTexture;

        public BlockStateModel(List<BlockGeometry> geometries, RenderType renderType, OffsetType offsetType, ResourceLocation particleTexture)
        {
            Geometries = geometries.ToArray();
            RenderType = renderType;
            OffsetType = offsetType;
            ParticleTexture = particleTexture;
        }
    }
}