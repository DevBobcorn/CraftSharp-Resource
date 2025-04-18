#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

namespace CraftSharp.Resource.BedrockEntity
{
    public struct BedrockVersion : IComparable
    {
        public int a;
        public int b;
        public int c;

        public BedrockVersion(int va, int vb, int vc)
        {
            a = va;
            b = vb;
            c = vc;
        }

        public readonly int CompareTo(object obj)
        {
            if (obj is BedrockVersion ver)
            {
                if (a == ver.a)
                {
                    if (b == ver.b)
                    {
                        if (c == ver.c)
                        {
                            return 0;
                        }
                        else
                        {
                            return c > ver.c ? 1 : -1;
                        }
                    }
                    else
                    {
                        return b > ver.b ? 1 : -1;
                    }
                }
                else
                {
                    return a > ver.a ? 1 : -1;
                }
            }
            else
            {
                throw new InvalidDataException("Trying to compare a bedrock object to unknown object!");
            }
        }
    
        public static BedrockVersion FromString(string version)
        {
            var nums = version.Split(".");
            if (nums.Length == 3)
            {
                return new(int.Parse(nums[0]), int.Parse(nums[1]), int.Parse(nums[2]));
            }
            else
            {
                throw new InvalidDataException($"Malformed version string: {version}");
            }
        }

        public static bool operator >(BedrockVersion n1, BedrockVersion n2) => n1.CompareTo(n2) > 0;
        public static bool operator >=(BedrockVersion n1, BedrockVersion n2) => n1.CompareTo(n2) >= 0;
        public static bool operator <(BedrockVersion n1, BedrockVersion n2) => n1.CompareTo(n2) < 0;
        public static bool operator <=(BedrockVersion n1, BedrockVersion n2) => n1.CompareTo(n2) <= 0;

        public override readonly bool Equals(object obj)
        {
            if (obj is not BedrockVersion) return false;
            return Equals((BedrockVersion) obj);
        }

        public readonly bool Equals(BedrockVersion other)
        {
            return other.a == a && other.b == b && other.c == c;
        }

        public override readonly int GetHashCode()
        {
            return a.GetHashCode() ^ b.GetHashCode() ^ c.GetHashCode();
        }

        public override readonly string ToString()
        {
            return $"[ {a}, {b}, {c} ]";
        }
    }

    public class BedrockEntityResourceManager
    {
        public static readonly BedrockVersion UNSPECIFIED_VERSION = new(-1, 0, 0);
        private static readonly char SP = Path.DirectorySeparatorChar;

        public readonly Dictionary<ResourceLocation, EntityRenderDefinition> EntityRenderDefinitions = new();
        public readonly Dictionary<string, EntityGeometry> EntityGeometries = new();
        public readonly Dictionary<string, EntityAnimation> EntityAnimations = new();

        public readonly Dictionary<string, EntityRenderType> MaterialRenderTypes = new();

        public readonly string[] EntityMaterialNames = { };
        
        private readonly string resourcePath;
        private readonly string playerModelsPath;

        public BedrockEntityResourceManager(string resPath, string playerPath)
        {
            resourcePath = resPath;
            playerModelsPath = playerPath;
        }

        private bool ReplaceCheck(BedrockVersion prevVer, BedrockVersion newVer)
        {
            if (newVer.Equals(UNSPECIFIED_VERSION))
            {
                return false;
            }
            else
            {
                if (prevVer.Equals(UNSPECIFIED_VERSION))
                {
                    return true;
                }
                else // Both are specified
                {
                    // Return false if they're the same
                    return newVer > prevVer;
                }
            }
        }

        public IEnumerator LoadEntityResources(DataLoadFlag flag, Action<string> updateStatus)
        {
            // Clean up
            EntityRenderDefinitions.Clear();
            EntityGeometries.Clear();
            EntityAnimations.Clear();
            MaterialRenderTypes.Clear();

            // Load animations
            var animFolderDir = new DirectoryInfo($"{resourcePath}{SP}animations");
            foreach (var animFile in animFolderDir.GetFiles("*.json", SearchOption.AllDirectories)) // Allow sub folders...
            {
                var data = Json.ParseJson(File.ReadAllText(animFile.FullName)).Properties["animations"];

                foreach (var pair in data.Properties)
                {
                    var animName = pair.Key;
                    var anim = EntityAnimation.FromJson(pair.Value);

                    EntityAnimations.Add(animName, anim);
                }
            }

            var playerModelFolderDir = new DirectoryInfo(playerModelsPath);
            foreach (var modelFolder in playerModelFolderDir.GetDirectories())
            {
                var playerFolderRoot = new DirectoryInfo(modelFolder.FullName);

                var geoFile = $"{playerFolderRoot}{SP}main.json";
                if (File.Exists(geoFile)) // This model is valid
                {
                    //Debug.Log($"Loading bedrock player model from {geoFile}");
                    var data = Json.ParseJson(File.ReadAllText(geoFile));

                    var geoName = string.Empty;

                    try
                    {
                        var geometries = EntityGeometry.TableFromJson(data);
                        
                        if (geometries.Count > 1) // Only one geometry is expected
                        {
                            Debug.LogWarning($"More than 1 geometries is found in file {geoFile}");
                        }

                        geoName = $"geometry.player_{modelFolder.Name}";
                        var geometry = geometries.First().Value;

                        EntityGeometries.Add(geoName, geometry);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"An error occurred when parsing {geoFile}: {e}");
                    }
                    
                    var dummyEntityIdentifier = new ResourceLocation("custom_player", modelFolder.Name);

                    EntityRenderDefinitions.Add(dummyEntityIdentifier, new EntityRenderDefinition(
                            UNSPECIFIED_VERSION, UNSPECIFIED_VERSION, dummyEntityIdentifier,
                            new Dictionary<string, string> { [ "default" ] = $"{playerFolderRoot}{SP}{modelFolder.Name}" },
                            new Dictionary<string, string> { [ "default" ] = "ysm_custom_player" },
                            new Dictionary<string, string> { [ "default" ] = geoName },
                            new Dictionary<string, string> { }
                    ));
                }
            }
            
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

                if (EntityRenderDefinitions.ContainsKey(entityType)) // Check version
                {
                    var prev = EntityRenderDefinitions[entityType];

                    if (ReplaceCheck(prev.FormatVersion, entityDef.FormatVersion)
                            || ReplaceCheck(prev.MinEngineVersion, entityDef.MinEngineVersion)) // Update this entry
                    {
                        EntityRenderDefinitions[entityType] = entityDef;
                        //Debug.Log($"Updating entry: [{entityType}] {defFile} v{entityDef.MinEngineVersion}");
                    }
                }
                else // Just register
                {
                    EntityRenderDefinitions.Add(entityType, entityDef);
                    //Debug.Log($"Creating entry: [{entityType}] {defFile} v{entityDef.MinEngineVersion}");
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
                    foreach (var pair in EntityGeometry.TableFromJson(data)) // For each geometry in this file
                    {
                        var geoName = pair.Key;
                        var geometry = pair.Value;

                        if (EntityGeometries.ContainsKey(geoName)) // Check version
                        {
                            var prev = EntityGeometries[geoName];

                            if (ReplaceCheck(prev.FormatVersion, geometry.FormatVersion)
                                    || ReplaceCheck(prev.MinEngineVersion, geometry.MinEngineVersion)) // Update this entry
                            {
                                EntityGeometries[geoName] = geometry;
                                //Debug.Log($"Updating entry: [{geoName}] {geoFile} v{geometry.MinEngineVersion}");
                            }
                        }
                        else // Just register
                        {
                            EntityGeometries.Add(geoName, geometry);
                            //Debug.Log($"Creating entry: [{geoName}] {geoFile} v{geometry.MinEngineVersion}");
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
            var matDictionary = Json.ParseJson(File.ReadAllText(
                    PathHelper.GetExtraDataFile("entity_bedrock_model_render_type.json"))).Properties;
            
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