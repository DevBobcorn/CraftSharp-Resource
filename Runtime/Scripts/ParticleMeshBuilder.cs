using UnityEngine;

namespace CraftSharp.Resource
{
    public class ParticleMeshBuilder
    {
        public static Mesh BuildQuadMesh(Rect particleFrameTextureRect)
        {
            Mesh mesh = new();

            float width = 1F;
            float height = 1F;

            float halfW = width / 2F;
            float halfH = height / 2F;

            // 2 3
            // 0 1

            mesh.vertices = new Vector3[4]
            {
                new(-halfW, -halfH, 0),
                new( halfW, -halfH, 0),
                new(-halfW,  halfH, 0),
                new( halfW,  halfH, 0),
            };
            mesh.uv = new Vector2[4]
            {
                new(particleFrameTextureRect.xMin, particleFrameTextureRect.yMin),
                new(particleFrameTextureRect.xMax, particleFrameTextureRect.yMin),
                new(particleFrameTextureRect.xMin, particleFrameTextureRect.yMax),
                new(particleFrameTextureRect.xMax, particleFrameTextureRect.yMax),
            };
            mesh.triangles = new int[6]
            {
                // lower left triangle
                0, 2, 1,
                // upper right triangle
                2, 3, 1,
            };

            mesh.RecalculateBounds();

            return mesh;
        }
    }
}