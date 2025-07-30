using System.Collections.Generic;
using System.Linq;

namespace CraftSharp.Resource.BedrockEntity
{
    public class EntityRenderDefinition
    {
        private static readonly char SP = System.IO.Path.DirectorySeparatorChar;
        
        // File versions
        public BedrockVersion FormatVersion;
        public BedrockVersion MinEngineVersion;
        // Identifier of this entity type
        public ResourceLocation EntityType;
        // Texture name => texture path in pack
        public readonly Dictionary<string, string> TexturePaths;
        // Material name => material identifier
        public readonly Dictionary<string, string> MaterialIdentifiers;
        // Variant name => geometry name
        public readonly Dictionary<string, string> GeometryNames;
        // State name => animation name
        public readonly Dictionary<string, string> AnimationNames;

        internal EntityRenderDefinition(BedrockVersion formatVersion, BedrockVersion minEnVersion, ResourceLocation entityType,
                Dictionary<string, string> texturePaths, Dictionary<string, string> materialIds,
                Dictionary<string, string> geometryNames,Dictionary<string, string> animationNames)
        {
            FormatVersion = formatVersion;
            MinEngineVersion = minEnVersion;

            EntityType = entityType;

            TexturePaths = texturePaths;
            MaterialIdentifiers = materialIds;
            GeometryNames = geometryNames;
            AnimationNames = animationNames;
        }

        public static EntityRenderDefinition FromJson(Json.JSONData data)
        {
            var defVersion = BedrockVersion.FromString(data.Properties["format_version"].StringValue);

            var desc = data.Properties["minecraft:client_entity"].Properties["description"];
            var entityType = ResourceLocation.FromString(desc.Properties["identifier"].StringValue);

            Dictionary<string, string> materialIds;
            if (desc.Properties.TryGetValue("materials", out var val))
            {
                materialIds = val.Properties.ToDictionary(x => x.Key,
                        x => x.Value.StringValue);
            }
            else
            {
                materialIds = new();
            }

            Dictionary<string, string> texturePaths;
            if (desc.Properties.TryGetValue("textures", out val))
            {
                texturePaths = val.Properties.ToDictionary(x => $"{entityType.Path}/{x.Key}",
                        x => x.Value.StringValue);
            }
            else
            {
                texturePaths = new();
            }

            Dictionary<string, string> geometryNames;
            if (desc.Properties.TryGetValue("geometry", out val))
            {
                geometryNames = val.Properties.ToDictionary(x => x.Key,
                    x => x.Value.StringValue);
            }
            else
            {
                geometryNames = new();
            }

            Dictionary<string, string> animationNames;
            if (desc.Properties.TryGetValue("animations", out val))
            {
                animationNames = val.Properties.ToDictionary(x => x.Key,
                    x => x.Value.StringValue);
            }
            else
            {
                animationNames = new();
            }

            var minEnVersion = BedrockEntityResourceManager.UNSPECIFIED_VERSION;
            if (desc.Properties.TryGetValue("min_engine_version", out val))
            {
                minEnVersion = BedrockVersion.FromString(val.StringValue);
            }

            return new(defVersion, minEnVersion, entityType,
                texturePaths, materialIds, geometryNames, animationNames);
        }
    }
}