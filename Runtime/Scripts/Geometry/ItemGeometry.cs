using System.Collections.Generic;
using Unity.Mathematics;

namespace CraftSharp.Resource
{
    public class ItemGeometry
    {
        public readonly Dictionary<DisplayPosition, float3x3> DisplayTransforms;
        private readonly float3[] vertexArr;
        private readonly float3[] uvArr;
        private readonly float4[] uvAnimArr;
        private readonly int[] tintIndexArr;

        public ItemGeometry(float3[] vArr, float3[] uvArr, float4[] aArr, int[] tArr,
                Dictionary<DisplayPosition, float3x3> displayTransforms)
        {
            this.vertexArr = vArr;
            this.uvArr = uvArr;
            this.uvAnimArr = aArr;
            this.tintIndexArr = tArr;
            this.DisplayTransforms = displayTransforms;
        }

        public int GetVertexCount()
        {
            return vertexArr.Length;
        }

        public void Build(VertexBuffer buffer, ref uint vertOffset, float3 posOffset, float3[] itemTints)
        {
            var verts = buffer.vert;
            var txuvs = buffer.txuv;
            var uvans = buffer.uvan;
            var tints = buffer.tint;

            uint i;

            if (vertexArr.Length > 0)
            {
                for (i = 0U;i < vertexArr.Length;i++)
                {
                    verts[i + vertOffset] = vertexArr[i] + posOffset;
                    tints[i + vertOffset] = tintIndexArr[i] >= 0 && tintIndexArr[i] < itemTints.Length ?
                            new(itemTints[tintIndexArr[i]], 1F) : new(BlockGeometry.DEFAULT_COLOR, 1F);
                }
                uvArr.CopyTo(txuvs, vertOffset);
                uvAnimArr.CopyTo(uvans, vertOffset);
            }

            vertOffset += (uint) vertexArr.Length;
        }
    }
}