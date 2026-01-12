void Unpack_float(float Packed, out float VertBlockLight, out float VertSkyLight)
{
    int packedInt = (int) Packed;

    int vertBlockLight  = packedInt & 0xFF;         // Lowest  8 bits
    int vertSkyLight    = (packedInt >> 8) & 0xFF;  // Middle  8 bits

    // Get vertex light value
    VertBlockLight = vertBlockLight;
    VertSkyLight = vertSkyLight;
}

void UnpackWithVertexNormal_float(float Packed, float3 MeshNormal, out float VertBlockLight, out float VertSkyLight, out float3 VertNormal)
{
    int packedInt = (int) Packed;

    int vertBlockLight  = packedInt & 0xFF;         // Lowest  8 bits
    int vertSkyLight    = (packedInt >> 8) & 0xFF;  // Middle  8 bits
    int vertNormalIndex = (packedInt >> 16) & 0x3F; // Highest 6 bits

    // Get vertex light value
    VertBlockLight = vertBlockLight;
    VertSkyLight = vertSkyLight;

    if (vertNormalIndex == (int) 0x3F)
    {
        // All 6 bits are set, use mesh vertex normal
        VertNormal = MeshNormal;
    }
    else
    {
        // Decode approximate vertex normal
        float3 decoded = float3(0, 0, 0); // float3(1, 1, 1);

        if (      (vertNormalIndex &  0x1) != 0)
        {
            decoded += float3( 1,  0,  0);
        }
        else if ( (vertNormalIndex &  0x2) != 0)
        {
            decoded += float3(-1,  0,  0);
        }

        if (      (vertNormalIndex &  0x4) != 0)
        {
            decoded += float3( 0,  1,  0);
        }
        else if ( (vertNormalIndex &  0x8) != 0)
        {
            decoded += float3(0,  -1,  0);
        }

        if (      (vertNormalIndex & 0x10) != 0)
        {
            decoded += float3( 0,  0,  1);
        }
        else if ( (vertNormalIndex & 0x20) != 0)
        {
            decoded += float3( 0,  0, -1);
        }

        VertNormal = decoded;
    }
}