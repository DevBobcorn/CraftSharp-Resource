#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

namespace CraftSharp.Resource.BedrockEntity
{
    public class BedrockEntityResourceManager
    {
        public static readonly BedrockVersion UNSPECIFIED_VERSION = new(-1, 0, 0);
        private static readonly char SP = Path.DirectorySeparatorChar;

        public readonly Dictionary<ResourceLocation, EntityRenderDefinition> EntityRenderDefinitions = new();
        public readonly Dictionary<string, EntityGeometry> EntityGeometries = new();
        public readonly Dictionary<string, EntityAnimation> EntityAnimations = new();

        public readonly Dictionary<string, EntityRenderType> MaterialRenderTypes = new();
        private readonly Dictionary<string, Texture2D> CachedTextures = new();
        
        // Texture name -> Texture file path (For atlas)
        private readonly Dictionary<string, string> TextureFileTable = new();

        private readonly Dictionary<string, Dictionary<string, string>> ExtraEntityModelTextures = new()
        {
            ["chest"] = new()
            {
                ["normal"] = "textures/entity/chest/normal",
                ["trapped"] = "textures/entity/chest/trapped",
                ["ender"] = "textures/entity/chest/ender"
            },
            ["large_chest"] = new()
            {
                ["normal"] = "textures/entity/chest/double_normal",
                ["trapped"] = "textures/entity/chest/trapped_double"
            }
        };
        
        private readonly string resourcePath;
        private readonly string playerModelsPath;
        private readonly string blockEntityModelsPath;
        
        public static readonly BedrockEntityResourceManager Instance = new();

        private BedrockEntityResourceManager()
        {
            resourcePath = PathHelper.GetPackDirectoryNamed("bedrock_res");
            playerModelsPath = PathHelper.GetPackDirectoryNamed("player_models");
            blockEntityModelsPath = PathHelper.GetPackDirectoryNamed("block_entity_models");
        }

        private static string GetFullTexturePath(string pathWithoutExtension)
        {
            // Image could be either tga or png
            if (File.Exists($"{pathWithoutExtension}.png"))
            {
                return $"{pathWithoutExtension}.png";
            }
            return File.Exists($"{pathWithoutExtension}.tga") ? $"{pathWithoutExtension}.tga" : pathWithoutExtension;
        }
        
        private static bool CheckShouldReplaceEntry(BedrockVersion prevVer, BedrockVersion newVer)
        {
            if (newVer.Equals(UNSPECIFIED_VERSION))
            {
                return false;
            }

            if (prevVer.Equals(UNSPECIFIED_VERSION))
            {
                return true;
            }

            // Both are specified, return false if they're the same
            return newVer > prevVer;
        }

        private void LoadExtraEntityModelFolder(string baseResourcePath, string extraModelsPath, string geometryNamePrefix)
        {
            var extraModelFolderDir = new DirectoryInfo(extraModelsPath);
            if (!extraModelFolderDir.Exists)
            {
                Debug.Log($"BE extra entity model folder {extraModelsPath} does not exist, skipping.");
                return;
            }
            
            foreach (var extraModelFolder in extraModelFolderDir.GetDirectories())
            {
                var modelFolderRoot = new DirectoryInfo(extraModelFolder.FullName);

                var geoFile = $"{modelFolderRoot}{SP}main.json";
                
                if (File.Exists(geoFile)) // This model is valid
                {
                    //Debug.Log($"Loading bedrock player model from {geoFile}");
                    var data = Json.ParseJson(File.ReadAllText(geoFile));
                    var geoName = string.Empty;

                    try
                    {
                        var geometries = EntityGeometry.TableFromJson(data);
                        if (geometries.Count > 1) // Only one geometry is expected
                            Debug.LogWarning($"More than 1 geometries is found in file {geoFile}");

                        geoName = $"geometry.{geometryNamePrefix}_{extraModelFolder.Name}";
                        var geometry = geometries.First().Value;

                        EntityGeometries.Add(geoName, geometry);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"An error occurred when parsing {geoFile}: {e}");
                    }
                    
                    Dictionary<string, string> texturePaths;
                    
                    var dummyEntityIdentifier = new ResourceLocation(extraModelFolder.Name);
                    var defaultTexturePath = $"{modelFolderRoot}{SP}{extraModelFolder.Name}"; // extra_models/foo/foo.png

                    if (ExtraEntityModelTextures.TryGetValue(extraModelFolder.Name, out var textureList))
                    {
                        texturePaths = textureList.ToDictionary(x => $"{extraModelFolder.Name}/{x.Key}",
                            x => $"{baseResourcePath}{SP}{x.Value}");
                    }
                    else
                    {
                        texturePaths = new() { [ $"{extraModelFolder.Name}/default" ] = defaultTexturePath };
                    }

                    foreach (var (textureName, texturePath) in texturePaths)
                    {
                        TextureFileTable[textureName] = texturePath;
                    }
                    
                    EntityRenderDefinitions.Add(dummyEntityIdentifier, new EntityRenderDefinition(
                            UNSPECIFIED_VERSION, UNSPECIFIED_VERSION, dummyEntityIdentifier, texturePaths,
                            new Dictionary<string, string> { [ "default" ] = extraModelFolder.Name },
                            new Dictionary<string, string> { [ "default" ] = geoName },
                            new Dictionary<string, string>()
                    ));
                }
            }
        }

        public Texture2D LoadBedrockEntityTexture(int geometryTextureWidth, int geometryTextureHeight, string textureName)
        {
            if (CachedTextures.TryGetValue(textureName, out var texture))
            {
                return texture;
            }
            
            if (!TextureFileTable.TryGetValue(textureName, out var texturePathWithoutExtension)) // Texture path not registered
            {
                CachedTextures[textureName] = ResourcePackManager.GetMissingEntityTexture(
                    geometryTextureWidth <= 0 ? 32 : geometryTextureWidth, geometryTextureHeight <= 0 ? 32 : geometryTextureHeight);
                
                return CachedTextures[textureName];
            }

            var fileName = GetFullTexturePath(texturePathWithoutExtension);
            // Load texture from file
            var imageBytes = File.ReadAllBytes(fileName);
            if (fileName.EndsWith(".tga")) // Read as tga image
            {
                texture = TGALoader.TextureFromTGA(imageBytes);
            }
            else // Read as png image
            {
                texture = new Texture2D(2, 2);
                texture.LoadImage(imageBytes);
            }

            texture.filterMode = FilterMode.Point;
            //Debug.Log($"Loaded texture from {fileName} ({texture.width}x{texture.height})");

            if (geometryTextureWidth != texture.width || geometryTextureHeight != texture.height)
            {
                if (geometryTextureWidth != 0 && geometryTextureHeight != 0) // The sizes doesn't match, use specified texture size
                {
                    Debug.LogWarning($"Specified texture size({geometryTextureWidth}x{geometryTextureHeight}) doesn't match image file {texturePathWithoutExtension} ({texture.width}x{texture.height})! Resizing...");

                    var textureWithRightSize = new Texture2D(geometryTextureWidth, geometryTextureHeight)
                    {
                        filterMode = FilterMode.Point
                    };

                    var fillColor = Color.clear;
                    Color[] pixels = new Color[geometryTextureHeight * geometryTextureWidth];
                    for (int i = 0; i < pixels.Length; i++)
                        pixels[i] = fillColor;
                    
                    textureWithRightSize.SetPixels(pixels);

                    var blitHeight = Mathf.Min(texture.height, geometryTextureHeight);
                    var blitOffset = geometryTextureHeight > texture.height ? geometryTextureHeight - texture.height : 0;

                    for (int y = 0; y < blitHeight; y++)
                        for (int x = 0; x < Mathf.Min(texture.width, geometryTextureWidth); x++)
                        {
                            textureWithRightSize.SetPixel(x, y + blitOffset, texture.GetPixel(x, y));
                        }
                    
                    textureWithRightSize.Apply();

                    texture = textureWithRightSize;
                }
            }
            
            CachedTextures[textureName] =  texture;

            return texture;
        }

        private void RegisterEntityTextures(EntityRenderDefinition definition)
        {
            foreach (var (textureName, texturePath) in definition.TexturePaths)
            {
                TextureFileTable[textureName] = texturePath;
            }
        }
        
        public IEnumerator LoadEntityResources(DataLoadFlag flag, Action<string> updateStatus)
        {
            // Clean up
            EntityRenderDefinitions.Clear();
            EntityGeometries.Clear();
            EntityAnimations.Clear();
            MaterialRenderTypes.Clear();
            CachedTextures.Clear();
            TextureFileTable.Clear();

            // Load animations
            var animFolderDir = new DirectoryInfo($"{resourcePath}{SP}animations");
            foreach (var animFile in animFolderDir.GetFiles("*.json", SearchOption.AllDirectories)) // Allow sub folders...
            {
                var data = Json.ParseJson(File.ReadAllText(animFile.FullName)).Properties["animations"];

                foreach (var (animName, animVal) in data.Properties)
                {
                    var anim = EntityAnimation.FromJson(animVal);

                    EntityAnimations.Add(animName, anim);
                }
            }

            LoadExtraEntityModelFolder(resourcePath, playerModelsPath, "player");
            LoadExtraEntityModelFolder(resourcePath, blockEntityModelsPath, "block_entity");
            
            yield return null;

            if (!Directory.Exists(resourcePath))
            {
                Debug.LogWarning("Bedrock resource not present!");
                yield break;
            }

            // Load entity definitions
            var defFolderDir = new DirectoryInfo($"{resourcePath}{SP}entity");
            foreach (var defFile in defFolderDir.GetFiles("*.json", SearchOption.TopDirectoryOnly)) // No sub folders...
            {
                var data = Json.ParseJson(File.ReadAllText(defFile.FullName));

                var entityDef = EntityRenderDefinition.FromJson(resourcePath, data);
                var entityType = entityDef.EntityType;

                if (EntityRenderDefinitions.TryAdd(entityType, entityDef)) // Check version
                {
                    RegisterEntityTextures(entityDef);
                }
                else
                {
                    var prev = EntityRenderDefinitions[entityType];

                    if (CheckShouldReplaceEntry(prev.FormatVersion, entityDef.FormatVersion)
                            || CheckShouldReplaceEntry(prev.MinEngineVersion, entityDef.MinEngineVersion)) // Update this entry
                    {
                        EntityRenderDefinitions[entityType] = entityDef;
                        RegisterEntityTextures(entityDef);
                        //Debug.Log($"Updating entry: [{entityType}] {defFile} v{entityDef.MinEngineVersion}");
                    }
                }
            }

            yield return null;

            // Load entity geometries
            var geoFolderDir = new DirectoryInfo($"{resourcePath}{SP}models");
            foreach (var geoFile in geoFolderDir.GetFiles("*.json", SearchOption.AllDirectories)) // Allow sub folders...
            {
                var data = Json.ParseJson(File.ReadAllText(geoFile.FullName));
                //Debug.Log($"START {geoFile}");

                try
                {
                    foreach (var (geoName, geometry) in EntityGeometry.TableFromJson(data)) // For each geometry in this file
                    {
                        if (!EntityGeometries.TryAdd(geoName, geometry)) // Check version
                        {
                            var prev = EntityGeometries[geoName];

                            if (CheckShouldReplaceEntry(prev.FormatVersion, geometry.FormatVersion)
                                    || CheckShouldReplaceEntry(prev.MinEngineVersion, geometry.MinEngineVersion)) // Update this entry
                            {
                                EntityGeometries[geoName] = geometry;
                                //Debug.Log($"Updating entry: [{geoName}] {geoFile} v{geometry.MinEngineVersion}");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"An error occurred when parsing {geoFile}: {e}");
                }
            }

            /*
            // Collect all entity materials
            HashSet<string> matIds = new();
            foreach (var def in EntityRenderDefinitions.Values)
            {
                foreach (var matId in def.MaterialIdentifiers.Values)
                {
                    matIds.Add(matId);
                }
            }

            string a = Json.Object2Json(matIds.ToDictionary(x => x, x => (object) "cutout_culloff"));
            File.WriteAllText(PathHelper.GetExtraDataFile("entity_bedrock_model_render_type.json"), a);
            */

            // Read entity material render types
            var matDictionary = Json.ParseJson(File.ReadAllText(PathHelper
                .GetExtraDataFile("entity_bedrock_model_render_type.json"))).Properties;
            
            foreach (var pair in matDictionary)
            {
                EntityRenderType renderType = pair.Value.StringValue switch
                {
                    "solid"          => EntityRenderType.SOLID,
                    "cutout"         => EntityRenderType.CUTOUT,
                    "cutout_culloff" => EntityRenderType.CUTOUT_CULLOFF,
                    "translucent"    => EntityRenderType.TRANSLUCENT,

                    "solid_emissive"          => EntityRenderType.SOLID_EMISSIVE,
                    "cutout_emissive"         => EntityRenderType.CUTOUT_EMISSIVE,
                    "cutout_culloff_emissive" => EntityRenderType.CUTOUT_CULLOFF_EMISSIVE,
                    "translucent_emissive"    => EntityRenderType.TRANSLUCENT_EMISSIVE,

                    _                => EntityRenderType.SOLID
                };

                MaterialRenderTypes.Add(pair.Key, renderType);
            }
        }
    }
}