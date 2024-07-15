using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace CraftSharp.Resource
{
    public static class CubeGeometry
    {
        private static readonly Vector4 FULL = new(0, 0, 1, 1);

        public static int GetVertexCount(int cullFlags)
        {
            return VertexCountMap[cullFlags];
        }

        public static void Build(ref VertexBuffer buffer, ref uint vertOffset, float3 posOffset, ResourceLocation tex, int cullFlags, float3 cubeColor)
        {
            var startOffset = vertOffset;

            // Unity                   Minecraft            Top Quad Vertices
            //  A +Z (East)             A +X (East)          v0---v1
            //  |                       |                    |     |
            //  *---> +X (South)        *---> +Z (South)     v2---v3

            var verts = buffer.vert;
            var txuvs = buffer.txuv;
            var uvans = buffer.uvan;
            var tints = buffer.tint;

            var (fullUVs, anim) = ResourcePackManager.Instance.GetUVs(tex, FULL, 0);

            float4[] uvAnims = { anim, anim, anim, anim };

            if ((cullFlags & (1 << 0)) != 0) // Up
            {
                verts[vertOffset]     = new(0, 1, 1); // 4 => 2
                verts[vertOffset + 1] = new(1, 1, 1); // 5 => 3
                verts[vertOffset + 2] = new(0, 1, 0); // 3 => 1
                verts[vertOffset + 3] = new(1, 1, 0); // 2 => 0
                fullUVs.CopyTo(txuvs, vertOffset);
                uvAnims.CopyTo(uvans, vertOffset);
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 1)) != 0) // Down
            {
                verts[vertOffset]     = new(0, 0, 0); // 0 => 0
                verts[vertOffset + 1] = new(1, 0, 0); // 1 => 1
                verts[vertOffset + 2] = new(0, 0, 1); // 7 => 3
                verts[vertOffset + 3] = new(1, 0, 1); // 6 => 2
                fullUVs.CopyTo(txuvs, vertOffset);
                uvAnims.CopyTo(uvans, vertOffset);
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 2)) != 0) // South
            {
                verts[vertOffset]     = new(1, 1, 0); // 2 => 1
                verts[vertOffset + 1] = new(1, 1, 1); // 5 => 2
                verts[vertOffset + 2] = new(1, 0, 0); // 1 => 0
                verts[vertOffset + 3] = new(1, 0, 1); // 6 => 3
                fullUVs.CopyTo(txuvs, vertOffset);
                uvAnims.CopyTo(uvans, vertOffset);
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 3)) != 0) // North
            {
                verts[vertOffset]     = new(0, 1, 1); // 4 => 2
                verts[vertOffset + 1] = new(0, 1, 0); // 3 => 1
                verts[vertOffset + 2] = new(0, 0, 1); // 7 => 3
                verts[vertOffset + 3] = new(0, 0, 0); // 0 => 0
                fullUVs.CopyTo(txuvs, vertOffset);
                uvAnims.CopyTo(uvans, vertOffset);
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 4)) != 0) // East
            {
                verts[vertOffset]     = new(1, 1, 1); // 5 => 1
                verts[vertOffset + 1] = new(0, 1, 1); // 4 => 0
                verts[vertOffset + 2] = new(1, 0, 1); // 6 => 2
                verts[vertOffset + 3] = new(0, 0, 1); // 7 => 3
                fullUVs.CopyTo(txuvs, vertOffset);
                uvAnims.CopyTo(uvans, vertOffset);
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 5)) != 0) // West
            {
                verts[vertOffset]     = new(0, 1, 0); // 3 => 3
                verts[vertOffset + 1] = new(1, 1, 0); // 2 => 2
                verts[vertOffset + 2] = new(0, 0, 0); // 0 => 0
                verts[vertOffset + 3] = new(1, 0, 0); // 1 => 1
                fullUVs.CopyTo(txuvs, vertOffset);
                uvAnims.CopyTo(uvans, vertOffset);
                // Not necessary vertOffset += 4;
            }

            for (uint i = startOffset; i < vertOffset; i++) // For each new vertex in the mesh
            {
                // Calculate vertex lighting
                tints[i] = new float4(cubeColor, 0F);
                // Offset vertices
                verts[i] = verts[i] + posOffset;
            }
        }

        public static readonly Dictionary<int, int> VertexCountMap = CreateVertexCountMap();

        private static Dictionary<int, int> CreateVertexCountMap()
        {
            Dictionary<int, int> sizeMap = new();

            for (int cullFlags = 0b000000;cullFlags <= 0b111111;cullFlags++)
            {
                int vertexCount = 0;

                for (int i = 0;i < 6;i++)
                {
                    if ((cullFlags & (1 << i)) != 0) // This face(side) presents
                        vertexCount += 4;
                }

                sizeMap.Add(cullFlags, vertexCount);

            }

            return sizeMap;
        }
    }
}