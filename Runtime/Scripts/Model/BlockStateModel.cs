using System.Collections.Generic;
using System.Linq;

namespace CraftSharp.Resource
{
    public class BlockStateModel
    {
        public readonly BlockGeometry[] Geometries;
        public readonly HashSet<ResourceLocation> ModelIds;
        public readonly RenderType RenderType;
        public readonly OffsetType OffsetType;
        public readonly ResourceLocation ParticleTexture;

        public BlockStateModel(List<BlockGeometry> geometries, HashSet<ResourceLocation> modelIds,
            RenderType renderType, OffsetType offsetType, ResourceLocation particleTexture)
        {
            Geometries = geometries.ToArray();
            ModelIds = modelIds;
            RenderType = renderType;
            OffsetType = offsetType;
            ParticleTexture = particleTexture;
        }
    }
}