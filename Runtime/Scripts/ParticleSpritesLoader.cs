using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CraftSharp.Resource
{
    public class ParticleSpritesLoader
    {
        private readonly ResourcePackManager manager;

        public ParticleSpritesLoader(ResourcePackManager manager)
        {
            this.manager = manager;
        }

        /// <summary>
        /// Load and build particle sprites for a particle type with given identifier.
        /// </summary>
        public Texture2D[] LoadAndBuildParticleMeshes(ResourceLocation identifier)
        {
            if (manager.ParticleFileTable.TryGetValue(identifier, out string particleFilePath))
            {
                string particleText = File.ReadAllText(particleFilePath);
                Json.JSONData modelData = Json.ParseJson(particleText);

                if (modelData.Properties.TryGetValue("textures", out var textureIds))
                {
                    List<Texture2D> textures = new(textureIds.DataArray.Count);
                    int spriteWidth = 0, spriteHeight = 0;

                    foreach (var textureIdElem in textureIds.DataArray)
                    {
                        var textureId = ResourceLocation.FromString(textureIdElem.StringValue);
                        // Prepend "particle" to texture identifier path
                        textureId = new ResourceLocation(textureId.Namespace, $"particle/{textureId.Path}");

                        var texturePath = manager.TextureFileTable[textureId];
                        var texture = new Texture2D(2, 2)
                        {
                            filterMode = FilterMode.Point
                        };
                        texture.LoadImage(File.ReadAllBytes(texturePath));

                        if (spriteWidth == 0 || spriteHeight == 0)
                        {
                            spriteWidth = texture.width;
                            spriteHeight = texture.height;
                        }
                        else
                        {
                            if (spriteWidth != texture.width || spriteHeight != texture.height)
                            {
                                Debug.LogWarning($"Sprite {texturePath} for particle type {identifier} has inconsistent size {texture.width}x{texture.height}. Expected {spriteWidth}x{spriteHeight}");
                            }
                        }

                        textures.Add(texture);
                    }

                    return textures.ToArray();
                }

                //Debug.LogWarning($"Particle {identifier} doesn't define any sprites");
                return Array.Empty<Texture2D>();
            }
            else
            {
                Debug.LogWarning($"Particle file not found: {identifier}");
                return Array.Empty<Texture2D>();
            }
        }
    }
}
