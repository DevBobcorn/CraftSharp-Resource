using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace CraftSharp.Resource
{
    public class BlockGeometry
    {
        public static readonly float3 DEFAULT_COLOR = new(1F, 1F, 1F);

        private readonly Dictionary<CullDir, float3[]> vertexArrs;
        private readonly Dictionary<CullDir, float3[]> uvArrs;
        private readonly Dictionary<CullDir, float4[]> uvAnimArrs;
        private readonly Dictionary<CullDir, int[]> tintIndexArrs;

        private readonly CullDir[] noCullingVertexDirArr;

        public BlockGeometry(Dictionary<CullDir, float3[]> vArrs, Dictionary<CullDir, float3[]> uvArrs,
                Dictionary<CullDir, float4[]> aArrs, Dictionary<CullDir, int[]> tArrs, CullDir[] ncvdArrs)
        {
            this.vertexArrs = vArrs;
            this.uvArrs = uvArrs;
            this.uvAnimArrs = aArrs;
            this.tintIndexArrs = tArrs;
            this.noCullingVertexDirArr = ncvdArrs;
        }

        // Cache for array sizes, mapping cull flags to corresponding vertex array sizes
        private readonly ConcurrentDictionary<int, int> sizeCache = new();

        private int CalculateVertexCount(int cullFlags)
        {
            int vertexCount = vertexArrs[CullDir.NONE].Length;

            for (int dirIdx = 0;dirIdx < 6;dirIdx++)
            {
                if ((cullFlags & (1 << dirIdx)) != 0)
                    vertexCount += vertexArrs[(CullDir) (dirIdx + 1)].Length;
            }

            return vertexCount;
        }

        public static float GetVertexLightFromFaceLights(CullDir dir, float[] faceLights)
        {
            // Simplified sampling: Accepts light values of neighbors and self, sample by face direction
            float neighbor = dir switch
            {
                CullDir.DOWN  => faceLights[1],
                CullDir.UP    => faceLights[0],
                CullDir.NORTH => faceLights[2],
                CullDir.SOUTH => faceLights[3],
                CullDir.EAST  => faceLights[4],
                CullDir.WEST  => faceLights[5],
                _             => faceLights[6]
            };

            return math.max(neighbor, faceLights[6]);
        }

        /// <summary>
        /// Light value range: [0, 15]
        /// </summary>
        public static float GetVertexLightFromCornerLights(float3 vertPosInBlock, float[] cornerLights)
        {
            // Enhanced sampling: Accepts averaged light values of 8 corners
            float x0z0 = math.lerp(cornerLights[0], cornerLights[4], vertPosInBlock.y);
            float x1z0 = math.lerp(cornerLights[1], cornerLights[5], vertPosInBlock.y);
            float x0z1 = math.lerp(cornerLights[2], cornerLights[6], vertPosInBlock.y);
            float x1z1 = math.lerp(cornerLights[3], cornerLights[7], vertPosInBlock.y);

            return math.lerp(
                    math.lerp(x0z0, x0z1, vertPosInBlock.x),
                    math.lerp(x1z0, x1z1, vertPosInBlock.x),
                    vertPosInBlock.z
            );
        }

        private float GetCornerAO(bool side1, bool corner, bool side2, float intensity)
        {
            return 1F - (side1 ? intensity : 0F) - (corner ? intensity : 0F) - (side2 ? intensity : 0F);
        }

        /// <summary>
        /// Get 4 AO values for each corner of a given face
        /// </summary>
        private float[] GetCornersAO(bool tl, bool tm, bool tr, bool ml, bool mr, bool bl, bool bm, bool br, float intensity)
        {
            return new float[]
            {
                GetCornerAO(ml, tl, tm, intensity), // tl
                GetCornerAO(tm, tr, mr, intensity), // tr
                GetCornerAO(bm, bl, ml, intensity), // bl
                GetCornerAO(mr, br, bm, intensity), // br
            };
        }

        private static readonly float[] NO_AO = new float[] { 1F, 1F, 1F, 1F };
        private static readonly float[] FULL_THICKNESS = new float[] { 1F, 1F, 1F, 1F };

        /// <summary>
        /// Get 4 corner AO values for a face
        /// </summary>
        public float[] GetDirCornersAO(CullDir dir, int castAOMask, float intensity)
        {
            bool castAO(int index)
            {
                return (castAOMask & (1 << index)) != 0;
            }

            return dir switch
            {
                CullDir.DOWN      =>
                    //  6  7  8    A unity x+ (South)
                    //  3  4  5    |
                    //  0  1  2    o--> unity z+ (East)
                    GetCornersAO(castAO( 6), castAO( 7), castAO( 8), castAO( 3), castAO( 5), castAO( 0), castAO( 1), castAO( 2), intensity),
                CullDir.UP        =>
                    // 20 23 26    A unity z+ (East)
                    // 19 22 25    |
                    // 18 21 24    o--> unity x+ (South)
                    GetCornersAO(castAO(20), castAO(23), castAO(26), castAO(19), castAO(25), castAO(18), castAO(21), castAO(24), intensity),
                CullDir.SOUTH     =>
                    // 24 25 26    A unity y+ (Up)
                    // 15 16 17    |
                    //  6  7  8    o--> unity z+ (East)
                    GetCornersAO(castAO(24), castAO(25), castAO(26), castAO(15), castAO(17), castAO( 6), castAO( 7), castAO( 8), intensity),
                CullDir.NORTH     =>
                    //  2 11 20    A unity z+ (East)
                    //  1 10 19    |
                    //  0  9 18    o--> unity y+ (Up)
                    GetCornersAO(castAO( 2), castAO(11), castAO(20), castAO( 1), castAO(19), castAO( 0), castAO( 9), castAO(18), intensity),
                CullDir.EAST      =>
                    //  8 17 26    A unity x+ (South)
                    //  5 14 23    |
                    //  2 11 20    o--> unity y+ (Up)
                    GetCornersAO(castAO( 8), castAO(17), castAO(26), castAO( 5), castAO(23), castAO( 2), castAO(11), castAO(20), intensity),
                CullDir.WEST      =>
                    // 18 21 24    A unity y+ (Up)
                    //  9 12 15    |
                    //  0  3  6    o--> unity x+ (South)
                    GetCornersAO(castAO(18), castAO(21), castAO(24), castAO( 9), castAO(15), castAO( 0), castAO( 3), castAO( 6), intensity),

                _                 => NO_AO
            };
        }

        /// <summary>
        /// Get 4 corner AO values for a face which is inside the block
        /// </summary>
        public float[] GetDirCornersInBlockAO(CullDir dir, int castAOMask, float intensity)
        {
            bool castAO(int index)
            {
                return (castAOMask & (1 << index)) != 0;
            }

            return dir switch
            {
                CullDir.UP        =>
                    // 11 14 17    A unity z+ (East)
                    // 10 [] 16    |
                    //  9 12 15    o--> unity x+ (South)
                    GetCornersAO(castAO(11), castAO(14), castAO(17), castAO(10), castAO(16), castAO( 9), castAO(12), castAO(15), intensity),
                CullDir.DOWN      =>
                    // 15 16 17    A unity x+ (South)
                    // 12 [] 14    |
                    //  9 10 11    o--> unity z+ (East)
                    GetCornersAO(castAO(15), castAO(16), castAO(17), castAO(12), castAO(14), castAO( 9), castAO(10), castAO(11), intensity),
                CullDir.NORTH     =>
                    //  5 14 23    A unity z+ (East)
                    //  4 [] 22    |
                    //  3 12 21    o--> unity y+ (Up)
                    GetCornersAO(castAO( 5), castAO(14), castAO(23), castAO( 4), castAO(22), castAO( 3), castAO(12), castAO(21), intensity),
                CullDir.SOUTH     =>
                    // 21 22 23    A unity y+ (Up)
                    // 12 [] 14    |
                    //  3  4  5    o--> unity z+ (East)
                    GetCornersAO(castAO(21), castAO(22), castAO(23), castAO(12), castAO(14), castAO( 3), castAO( 4), castAO( 5), intensity),
                CullDir.WEST      =>
                    // 19 22 25    A unity y+ (Up)
                    // 10 [] 16    |
                    //  1  4  7    o--> unity x+ (South)
                    GetCornersAO(castAO(19), castAO(22), castAO(25), castAO(10), castAO(16), castAO( 1), castAO( 4), castAO( 7), intensity),
                CullDir.EAST      =>
                    //  7 16 25    A unity x+ (South)
                    //  4 [] 22    |
                    //  1 10 19    o--> unity y+ (Up)
                    GetCornersAO(castAO( 7), castAO(16), castAO(25), castAO( 4), castAO(22), castAO( 1), castAO(10), castAO(19), intensity),

                _                 => NO_AO
            };
        }

        public float SampleVertexAO(CullDir dir, float[] cornersAO, float3 vertPosInBlock, float vertLight)
        {
            // AO Coord: 0 1
            //           2 3
            float2 AOCoord = dir switch
            {
                CullDir.DOWN   => vertPosInBlock.zx,
                CullDir.UP     => vertPosInBlock.xz,
                CullDir.SOUTH  => vertPosInBlock.zy,
                CullDir.NORTH  => vertPosInBlock.yz,
                CullDir.EAST   => vertPosInBlock.yx,
                CullDir.WEST   => vertPosInBlock.xy,

                _              => float2.zero
            };

            var ao = math.lerp(math.lerp(cornersAO[2], cornersAO[0], AOCoord.y),
                     math.lerp(cornersAO[3], cornersAO[1], AOCoord.y), AOCoord.x);
            
            // Reduce ambient occlusion of lit vertices
            return math.lerp(ao, 1F, vertLight / 15F);
        }

        private int GetVertexNormalIndex_Block(float3 vertPosInBlock, int castAOMask)
        {
            int index = 0;

            if (      ((castAOMask & (1 << 16)) == 0) && vertPosInBlock.x > 0.6F) // [16] South is empty
            {
                index |= 0b000001; // x+, South
            }
            else if ( ((castAOMask & (1 << 10)) == 0) && vertPosInBlock.x < 0.4F) // [10] North is empty
            {
                index |= 0b000010; // x-, North
            }

            if (      ((castAOMask & (1 << 22)) == 0) && vertPosInBlock.y > 0.6F) // [22] Up is empty
            {
                index |= 0b000100; // y+, up
            }
            else if ( ((castAOMask & (1 <<  4)) == 0) && vertPosInBlock.y < 0.4F) // [ 4] Down is empty
            {
                index |= 0b001000; // y-, down
            }

            if (      ((castAOMask & (1 << 14)) == 0) && vertPosInBlock.z > 0.6F) // [14] East is empty
            {
                index |= 0b010000; // z+, east
            }
            else if ( ((castAOMask & (1 << 12)) == 0) && vertPosInBlock.z < 0.4F) // [12] West is empty
            {
                index |= 0b100000; // z-, west
            }

            return index;
        }

        private float PackExtraVertData(float light, int vertNormalIndex)
        {
            // A float number can store an integer with a value of up to 16777216 (2^24)
            // without losing precision(See https://stackoverflow.com/a/3793950)

            // light: [0F, 15F] => [0, 255] (8-bit uint)
            int low = math.clamp(Mathf.RoundToInt(light * 17F), 0, 255);

            // vert normal index: (6-bit uint)
            // x: 2-bit, y: 2-bit, z: 2-bit

            return (vertNormalIndex << 8) | low;
        }

        public int GetVertexCount(int cullFlags)
        {
            // Compute value if absent
            return sizeCache.ContainsKey(cullFlags) ? sizeCache[cullFlags] :
                    (sizeCache[cullFlags] = CalculateVertexCount(cullFlags));
        }

        private static float3 RGB2Linear(float3 rgb)
        {
            return new float3(Mathf.GammaToLinearSpace(rgb.x), Mathf.GammaToLinearSpace(rgb.y), Mathf.GammaToLinearSpace(rgb.z));
        }

        public enum ExtraVertexData
        {
            Light,
            Light_BlockNormal,    // Used for foliage
            Light_CrossNormal     // Used for plants
        }

        /// <summary>
        /// Build block vertices into the given vertex buffer. Returns vertex offset after building.
        /// </summary>
        public void Build(VertexBuffer buffer, ref uint vertOffset, float3 posOffset, int cullFlags, int castAOMask, float aoIntensity, float[] blockLights, float3 blockColor, ExtraVertexData datFormat = ExtraVertexData.Light)
        {
            var verts = buffer.vert;
            var txuvs = buffer.txuv;
            var uvans = buffer.uvan;
            var tints = buffer.tint;

            uint i;

            if (vertexArrs[CullDir.NONE].Length > 0)
            {
                // Calculate in-block AO
                var aoDirs = noCullingVertexDirArr.ToHashSet();
                var dir2ao = aoDirs.ToDictionary(x => x, x => GetDirCornersInBlockAO(x, castAOMask, aoIntensity));

                for (i = 0U;i < vertexArrs[CullDir.NONE].Length;i++)
                {
                    // Divide vertex index by 4 to get quad index
                    var faceDir = noCullingVertexDirArr[i >> 2];

                    verts[i + vertOffset] = vertexArrs[CullDir.NONE][i] + posOffset;
                    float vertLight = GetVertexLightFromCornerLights(vertexArrs[CullDir.NONE][i], blockLights);
                    float3 vertColor = RGB2Linear(tintIndexArrs[CullDir.NONE][i] >= 0 ? blockColor : DEFAULT_COLOR)
                            * (aoIntensity > 0F ? SampleVertexAO(faceDir, dir2ao[faceDir], vertexArrs[CullDir.NONE][i], vertLight) : 1F);
                    switch (datFormat)
                    {
                        case ExtraVertexData.Light_BlockNormal:
                            int vertNormal1 = GetVertexNormalIndex_Block(vertexArrs[CullDir.NONE][i], castAOMask);
                            float packedVal1 = PackExtraVertData(vertLight, vertNormal1);
                            tints[i + vertOffset] = new float4(vertColor, packedVal1);
                            break;
                        case ExtraVertexData.Light_CrossNormal:
                            float packedVal2 = PackExtraVertData(vertLight, 0x3F);
                            tints[i + vertOffset] = new float4(vertColor, packedVal2);
                            break;
                        case ExtraVertexData.Light:
                            tints[i + vertOffset] = new float4(vertColor, vertLight);
                            break;
                    }
                }
                uvArrs[CullDir.NONE].CopyTo(txuvs, vertOffset);
                uvAnimArrs[CullDir.NONE].CopyTo(uvans, vertOffset);
                vertOffset += (uint) vertexArrs[CullDir.NONE].Length;
            }

            for (int dirIdx = 0;dirIdx < 6;dirIdx++)
            {
                var dir = (CullDir) (dirIdx + 1);

                if ((cullFlags & (1 << dirIdx)) != 0 && vertexArrs[dir].Length > 0)
                {
                    var cornersAO = GetDirCornersAO(dir, castAOMask, aoIntensity);

                    for (i = 0U;i < vertexArrs[dir].Length;i++)
                    {
                        verts[i + vertOffset] = vertexArrs[dir][i] + posOffset;
                        float vertLight = GetVertexLightFromCornerLights(vertexArrs[dir][i], blockLights);
                        float3 vertColor = RGB2Linear(tintIndexArrs[dir][i] >= 0 ? blockColor : DEFAULT_COLOR)
                                * (aoIntensity > 0F ? SampleVertexAO(dir, cornersAO, vertexArrs[dir][i], vertLight) : 1F);
                        switch (datFormat)
                        {
                            case ExtraVertexData.Light_BlockNormal:
                                int vertNormal1 = GetVertexNormalIndex_Block(vertexArrs[dir][i], castAOMask);
                                float packedVal1 = PackExtraVertData(vertLight, vertNormal1);
                                tints[i + vertOffset] = new float4(vertColor, packedVal1);
                                break;
                            case ExtraVertexData.Light_CrossNormal:
                                float packedVal2 = PackExtraVertData(vertLight, 0x3F);
                                tints[i + vertOffset] = new float4(vertColor, packedVal2);
                                break;
                            case ExtraVertexData.Light:
                                tints[i + vertOffset] = new float4(vertColor, vertLight);
                                break;
                        }
                    }
                    uvArrs[dir].CopyTo(txuvs, vertOffset);
                    uvAnimArrs[dir].CopyTo(uvans, vertOffset);
                    vertOffset += (uint) vertexArrs[dir].Length;
                }
            }
        }

        public void BuildWithCollider(VertexBuffer buffer, ref uint vertOffset, float3[] cVerts, ref uint cVertOffset, float3 posOffset,
                int cullFlags, int castAOMask, float aoIntensity, float[] blockLights, float3 blockColor, ExtraVertexData datFormat = ExtraVertexData.Light)
        {
            var startOffset = vertOffset;

            Build(buffer, ref vertOffset, posOffset, cullFlags, castAOMask, aoIntensity, blockLights, blockColor, datFormat);

            var newVertexCount = vertOffset - startOffset;

            // Copy from visual buffer to collider
            Array.Copy(buffer.vert, startOffset, cVerts, cVertOffset, newVertexCount);

            cVertOffset += newVertexCount;
        }

        public void BuildCollider(float3[] cVerts, ref uint vertOffset, float3 posOffset, int cullFlags)
        {
            uint i;

            if (vertexArrs[CullDir.NONE].Length > 0)
            {
                for (i = 0U;i < vertexArrs[CullDir.NONE].Length;i++)
                    cVerts[i + vertOffset] = vertexArrs[CullDir.NONE][i] + posOffset;
                vertOffset += (uint) vertexArrs[CullDir.NONE].Length;
            }

            for (int dirIdx = 0;dirIdx < 6;dirIdx++)
            {
                var dir = (CullDir) (dirIdx + 1);

                if ((cullFlags & (1 << dirIdx)) != 0 && vertexArrs[dir].Length > 0)
                {
                    for (i = 0U;i < vertexArrs[dir].Length;i++)
                        cVerts[i + vertOffset] = vertexArrs[dir][i] + posOffset;
                    vertOffset += (uint) vertexArrs[dir].Length;
                }
            }
        }
    }
}
