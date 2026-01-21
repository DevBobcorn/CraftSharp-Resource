using Unity.Mathematics;

namespace CraftSharp.Resource
{
    public class VertexBuffer
    {
        /// <summary>
        /// Vertex position array
        /// </summary>
        public float3[] vert = { };

        /// <summary>
        /// Texture uv array, 3d because we're using a texture array
        /// </summary>
        public float3[] txuv = { };

        /// <summary>
        /// Texture uv animation array (frame count, frame interval, frame size, frame per row)
        /// </summary>
        public float4[] uvan = { };

        /// <summary>
        /// Extra vertex data array (tint r, tint g, tint b, extra data) or (destroy u, destroy v, _, extra data)
        /// </summary>
        public float4[] tint = { };

        public VertexBuffer(int vertexCount)
        {
            vert = new float3[vertexCount];
            txuv = new float3[vertexCount];
            uvan = new float4[vertexCount];
            tint = new float4[vertexCount];
        }
    }
}