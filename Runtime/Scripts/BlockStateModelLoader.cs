using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CraftSharp.Resource
{
    public class BlockStateModelLoader
    {
        private const string PARTICLE_TEXTURE_NAME = "particle";
        private readonly ResourcePackManager manager;

        public BlockStateModelLoader(ResourcePackManager manager)
        {
            this.manager = manager;
        }

        public void LoadBlockStateModel(ResourceLocation blockId, string path, RenderType renderType, OffsetType offsetType)
        {
            if (File.Exists(path))
            {
                Json.JSONData stateData = Json.ParseJson(File.ReadAllText(path));

                if (stateData.Properties.TryGetValue("variants", out var val))
                {
                    //Debug.Log("Load variants state model: " + blockId.ToString());
                    LoadVariantsFormat(val.Properties, blockId, renderType, offsetType, manager);
                }
                else if (stateData.Properties.TryGetValue("multipart", out val))
                {
                    //Debug.Log("Load multipart state model: " + blockId.ToString());
                    LoadMultipartFormat(val.DataArray, blockId, renderType, offsetType, manager);
                }
                else
                    Debug.LogWarning("Invalid state model file: " + path);
            }
            else
                Debug.LogWarning("Cannot find block state model file: " + path);
        }

        private static void LoadVariantsFormat(Dictionary<string, Json.JSONData> variants, ResourceLocation blockId,
                RenderType renderType, OffsetType offsetType, ResourcePackManager manager)
        {
            foreach (var variant in variants)
            {
                var conditions = BlockStatePredicate.FromString(variant.Key);
                var particleTexture = ResourceLocation.INVALID;

                // Block states can contain properties don't make a difference to their block geometry list
                // In this way they can share a single copy of geometry list...
                var geometries = new List<BlockGeometry>();
                
                if (variant.Value.Type == Json.JSONData.DataType.Array) // A list...
                {
                    foreach (var wrapperData in variant.Value.DataArray)
                    {
                        var variantWrapper = BlockModelWrapper.FromJson(manager, wrapperData);
                        particleTexture = variantWrapper.model.ResolveTextureName(PARTICLE_TEXTURE_NAME);
                        geometries.Add(new BlockGeometryBuilder(variantWrapper).Build());
                    }
                }
                else // Only a single item...
                {
                    var variantWrapper = BlockModelWrapper.FromJson(manager, variant.Value);
                    particleTexture = variantWrapper.model.ResolveTextureName(PARTICLE_TEXTURE_NAME);
                    geometries.Add(new BlockGeometryBuilder(variantWrapper).Build());
                }

                foreach (var stateId in BlockStatePalette.INSTANCE.GetAllNumIds(blockId))
                {
                    // For every possible state of this block, select the states that belong
                    // to this variant and give them this geometry list to use...
                    if (!manager.StateModelTable.ContainsKey(stateId) && conditions.Check(BlockStatePalette.INSTANCE.GetByNumId(stateId)))
                    {
                        // Then this block state belongs to the current variant...
                        manager.StateModelTable.Add(stateId, new(geometries, renderType, offsetType, particleTexture));
                    }
                }
            }
        }

        private static void LoadMultipartFormat(List<Json.JSONData> parts, ResourceLocation blockId, RenderType renderType, OffsetType offsetType, ResourcePackManager manager)
        {
            var buildersList = BlockStatePalette.INSTANCE
                .GetAllNumIds(blockId).ToDictionary(stateId => stateId, _ => new BlockGeometryBuilder());

            var particleTexture = ResourceLocation.INVALID;

            foreach (var part in parts)
            {
                // Check part validity...
                if (part.Properties.TryGetValue("apply", out var applyData))
                {
                    // Prepare the part wrapper...
                    BlockModelWrapper partWrapper;
                    if (applyData.Type == Json.JSONData.DataType.Array)
                    {
                        // Don't really support a list here, just use the first value instead...
                        partWrapper = BlockModelWrapper.FromJson(manager, applyData.DataArray[0]);
                    }
                    else
                    {
                        partWrapper = BlockModelWrapper.FromJson(manager, applyData);
                    }

                    particleTexture = partWrapper.model.ResolveTextureName(PARTICLE_TEXTURE_NAME);

                    if (part.Properties.TryGetValue("when", out var whenData))
                    {
                        if (whenData.Properties.TryGetValue("OR", out var orData))
                        {   // 'when.OR' contains multiple predicates...
                            foreach (var stateItem in buildersList) // For each state
                            {
                                int stateId = stateItem.Key;
                                // Check and apply...
                                bool apply = false;
                                // An array of predicates in the value of 'OR'
                                foreach (var conditionData in orData.DataArray)
                                {
                                    if (BlockStatePredicate.FromJson(conditionData).Check(BlockStatePalette.INSTANCE.GetByNumId(stateId)))
                                    {
                                        apply = true;
                                        break;
                                    }
                                }

                                if (apply) // Apply this part to the current state
                                    buildersList[stateId].AppendWrapper(partWrapper);
                            }
                        }
                        else // 'when' is only a single predicate...
                        {
                            foreach (var stateItem in buildersList) // For each state
                            {
                                int stateId = stateItem.Key;
                                // Check and apply...
                                if (BlockStatePredicate.FromJson(whenData).Check(BlockStatePalette.INSTANCE.GetByNumId(stateId)))
                                    buildersList[stateId].AppendWrapper(partWrapper);
                            }
                        }
                    }
                    else // No predicate at all, apply anyway...
                    {
                        foreach (var stateItem in buildersList) // For each state
                            buildersList[stateItem.Key].AppendWrapper(partWrapper);
                    }
                }
            }

            // Get the table into manager...
            foreach (var resultItem in buildersList)
            {
                var geometries = new List<BlockGeometry> { resultItem.Value.Build() };
                manager.StateModelTable.Add(resultItem.Key, new(geometries, renderType, offsetType, particleTexture));
            }
        }
    }
}