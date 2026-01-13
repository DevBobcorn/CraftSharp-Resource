using System;
using UnityEngine;
using Unity.Mathematics;

namespace CraftSharp.Resource
{
    public static class FluidGeometry
    {
        public static readonly ResourceLocation[] LiquidTextures = new ResourceLocation[]
        {
            new("block/water_still"),
            new("block/lava_still"),
            new("block/water_flow"),
            new("block/lava_flow")
        };

        private static readonly Vector4 FULL_STILL = new(0, 0, 1, 1);
        private static readonly Vector4 FULL_FLOW = new(0.25F, 0.25F, 0.75F, 0.75F);

        // Add a subtle offset to sides of water to avoid z-fighting
        private const float O = 0F;
        private const float I = 1F;

        public static int GetVertexCount(int cullFlags)
        {
            return CubeGeometry.VertexCountMap[cullFlags];
        }

        private static float3 GetUVPosInFace(float faceU, float faceV, float3[] fullUVs)
        {
            var u = faceU * (fullUVs[1].x - fullUVs[0].x);
            var v = faceV * (fullUVs[2].y - fullUVs[0].y);
            return new float3(fullUVs[0].x + u, fullUVs[0].y + v, fullUVs[0].z);
        }

        private static float3[] GetLiquidSideUVs(float leftHeight, float rightHeight, float3[] fullUVs)
        {
            return new float3[]
            {
                GetUVPosInFace(0F, 1F - leftHeight, fullUVs),  // Top Left
                GetUVPosInFace(1F, 1F - rightHeight, fullUVs),  // Top Right
                GetUVPosInFace(0F, 1F, fullUVs),  // Bottom Left
                GetUVPosInFace(1F, 1F, fullUVs), // Bottom Right
            };
        }
        public static void Build(VertexBuffer buffer, ref uint vertOffset, float3 posOffset, ResourceLocation liquidStill,
                ResourceLocation liquidFlow, ReadOnlySpan<float> cornerHeights, int cullFlags, byte[] blockLights, int fluidColorInt)
        {
            var startOffset = vertOffset;
            var fluidColor = ColorConvert.GetFloat3(fluidColorInt);
            
            // Unity                   Minecraft            Top Quad Vertices     Height References
            //  A +Z (East)             A +X (East)          v0---v1               NE---SE
            //  |                       |                    |     |               |     |
            //  *---> +X (South)        *---> +Z (South)     v2---v3               NW---SW
            
            var full = (cullFlags & (1 << 0)) == 0;

            var hne = full ? 1F : math.clamp(cornerHeights[0], 0F, 1F);
            var hse = full ? 1F : math.clamp(cornerHeights[1], 0F, 1F);
            var hnw = full ? 1F : math.clamp(cornerHeights[2], 0F, 1F);
            var hsw = full ? 1F : math.clamp(cornerHeights[3], 0F, 1F);

            var verts = buffer.vert;
            var txuvs = buffer.txuv;
            var uvans = buffer.uvan;
            var tints = buffer.tint;

            var (stillFullUVs, stillAnim) = ResourcePackManager.Instance.GetUVs(liquidStill, FULL_STILL, 0);
            var (flowFullUVs, flowAnim) = ResourcePackManager.Instance.GetUVs(liquidFlow, FULL_FLOW, 0);

            float4[] stillUVAnims = { stillAnim, stillAnim, stillAnim, stillAnim };
            float4[] flowUVAnims = { flowAnim, flowAnim, flowAnim, flowAnim };

            if ((cullFlags & (1 << 0)) != 0) // Up
            {
                verts[vertOffset]     = new(0, hne, 1); // 4 => 2
                verts[vertOffset + 1] = new(1, hse, 1); // 5 => 3
                verts[vertOffset + 2] = new(0, hnw, 0); // 3 => 1
                verts[vertOffset + 3] = new(1, hsw, 0); // 2 => 0
                stillFullUVs.CopyTo(txuvs, vertOffset);
                stillUVAnims.CopyTo(uvans, vertOffset);
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 1)) != 0) // Down
            {
                verts[vertOffset]     = new(0, O, 0); // 0 => 0
                verts[vertOffset + 1] = new(1, O, 0); // 1 => 1
                verts[vertOffset + 2] = new(0, O, 1); // 7 => 3
                verts[vertOffset + 3] = new(1, O, 1); // 6 => 2
                stillFullUVs.CopyTo(txuvs, vertOffset);
                stillUVAnims.CopyTo(uvans, vertOffset);
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 2)) != 0) // South
            {
                verts[vertOffset]     = new(I, hsw, O); // 2 => 1
                verts[vertOffset + 1] = new(I, hse, I); // 5 => 2
                verts[vertOffset + 2] = new(I,   0, O); // 1 => 0
                verts[vertOffset + 3] = new(I,   0, I); // 6 => 3
                GetLiquidSideUVs(hsw, hse, flowFullUVs).CopyTo(txuvs, vertOffset);
                flowUVAnims.CopyTo(uvans, vertOffset);
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 3)) != 0) // North
            {
                verts[vertOffset]     = new(O, hne, I); // 4 => 2
                verts[vertOffset + 1] = new(O, hnw, O); // 3 => 1
                verts[vertOffset + 2] = new(O,   0, I); // 7 => 3
                verts[vertOffset + 3] = new(O,   0, O); // 0 => 0
                GetLiquidSideUVs(hne, hnw, flowFullUVs).CopyTo(txuvs, vertOffset);
                flowUVAnims.CopyTo(uvans, vertOffset);
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 4)) != 0) // East
            {
                verts[vertOffset]     = new(I, hse, I); // 5 => 1
                verts[vertOffset + 1] = new(O, hne, I); // 4 => 0
                verts[vertOffset + 2] = new(I,   0, I); // 6 => 2
                verts[vertOffset + 3] = new(O,   0, I); // 7 => 3
                GetLiquidSideUVs(hse, hne, flowFullUVs).CopyTo(txuvs, vertOffset);
                flowUVAnims.CopyTo(uvans, vertOffset);
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 5)) != 0) // West
            {
                verts[vertOffset]     = new(O, hnw, O); // 3 => 3
                verts[vertOffset + 1] = new(I, hsw, O); // 2 => 2
                verts[vertOffset + 2] = new(O,   0, O); // 0 => 0
                verts[vertOffset + 3] = new(I,   0, O); // 1 => 1
                GetLiquidSideUVs(hnw, hsw, flowFullUVs).CopyTo(txuvs, vertOffset);
                flowUVAnims.CopyTo(uvans, vertOffset);
                vertOffset += 4;
            }

            for (uint i = startOffset; i < vertOffset; i++) // For each new vertex in the mesh
            {
                var vertBlockLight = BlockGeometry.GetVertexLightFromCornerLights(verts[i], blockLights);
                var vertSkyLight = (byte) 0;
                var packedVal = BlockGeometry.PackVertexColorAlphaData(vertBlockLight, vertSkyLight, 0);

                // Calculate vertex lighting
                tints[i] = new float4(fluidColor, packedVal);
                // Offset vertices
                verts[i] = verts[i] + posOffset;
            }
        }

        public static void BuildCollider(float3[] verts, ref uint vertOffset, float3 posOffset, ReadOnlySpan<float> cornerHeights, int cullFlags)
        {
            var startOffset = vertOffset;
            var full = (cullFlags & (1 << 0)) == 0;

            var hne = full ? 1F : math.clamp(cornerHeights[0], 0F, 1F);
            var hse = full ? 1F : math.clamp(cornerHeights[1], 0F, 1F);
            var hnw = full ? 1F : math.clamp(cornerHeights[2], 0F, 1F);
            var hsw = full ? 1F : math.clamp(cornerHeights[3], 0F, 1F);

            if ((cullFlags & (1 << 0)) != 0) // Up
            {
                verts[vertOffset]     = new(0, hne, 1); // 4 => 2
                verts[vertOffset + 1] = new(1, hse, 1); // 5 => 3
                verts[vertOffset + 2] = new(0, hnw, 0); // 3 => 1
                verts[vertOffset + 3] = new(1, hsw, 0); // 2 => 0
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 1)) != 0) // Down
            {
                verts[vertOffset]     = new(0, O, 0); // 0 => 0
                verts[vertOffset + 1] = new(1, O, 0); // 1 => 1
                verts[vertOffset + 2] = new(0, O, 1); // 7 => 3
                verts[vertOffset + 3] = new(1, O, 1); // 6 => 2
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 2)) != 0) // South
            {
                verts[vertOffset]     = new(I, hsw, O); // 2 => 1
                verts[vertOffset + 1] = new(I, hse, I); // 5 => 2
                verts[vertOffset + 2] = new(I,   0, O); // 1 => 0
                verts[vertOffset + 3] = new(I,   0, I); // 6 => 3
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 3)) != 0) // North
            {
                verts[vertOffset]     = new(O, hne, I); // 4 => 2
                verts[vertOffset + 1] = new(O, hnw, O); // 3 => 1
                verts[vertOffset + 2] = new(O,   0, I); // 7 => 3
                verts[vertOffset + 3] = new(O,   0, O); // 0 => 0
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 4)) != 0) // East
            {
                verts[vertOffset]     = new(I, hse, I); // 5 => 1
                verts[vertOffset + 1] = new(O, hne, I); // 4 => 0
                verts[vertOffset + 2] = new(I,   0, I); // 6 => 2
                verts[vertOffset + 3] = new(O,   0, I); // 7 => 3
                vertOffset += 4;
            }

            if ((cullFlags & (1 << 5)) != 0) // West
            {
                verts[vertOffset]     = new(O, hnw, O); // 3 => 3
                verts[vertOffset + 1] = new(I, hsw, O); // 2 => 2
                verts[vertOffset + 2] = new(O,   0, O); // 0 => 0
                verts[vertOffset + 3] = new(I,   0, O); // 1 => 1
                vertOffset += 4;
            }

            for (uint i = startOffset; i < vertOffset; i++) // For each new vertex in the mesh
            {
                // Offset vertices
                verts[i] = verts[i] + posOffset;
            }
        }
    }
}