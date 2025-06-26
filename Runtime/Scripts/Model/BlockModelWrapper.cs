using UnityEngine;
using Unity.Mathematics;

namespace CraftSharp.Resource
{
    public class BlockModelWrapper
    {
        public readonly JsonModel model;
        public readonly int2 zyRot;
        public readonly bool uvLock;
        public readonly ResourceLocation blockModelId;

        public BlockModelWrapper(JsonModel model, int2 zyRot, bool uvLock, ResourceLocation blockModelId)
        {
            this.model = model;
            this.zyRot = zyRot;
            this.uvLock = uvLock;
            this.blockModelId = blockModelId;
        }

        public static BlockModelWrapper FromJson(ResourcePackManager manager, Json.JSONData data)
        {
            if (data.Properties.TryGetValue("model", out var modelVal))
            {
                var blockModelId = ResourceLocation.FromString(modelVal.StringValue);

                bool modelFound;

                // Check if the model can be found...
                if (manager.BlockModelTable.TryGetValue(blockModelId, out JsonModel blockModel))
                {
                    modelFound = true;
                }
                else
                {
                    blockModelId = new ResourceLocation(blockModelId.Namespace, "block/" + blockModelId.Path);
                    modelFound = manager.BlockModelTable.TryGetValue(blockModelId, out blockModel);
                }

                if (modelFound)
                {
                    int zr = 0, yr = 0;
                    bool uvLock = false;

                    if (data.Properties.TryGetValue("x", out var val)) // Block z rotation
                    {
                        zr = val.StringValue switch
                        {
                            "90"  => 1,
                            "180" => 2,
                            "270" => 3,
                            _     => 0
                        };
                    }

                    if (data.Properties.TryGetValue("y", out val)) // Block y rotation
                    {
                        yr = val.StringValue switch
                        {
                            "90"  => 1,
                            "180" => 2,
                            "270" => 3,
                            _     => 0
                        };
                    }

                    if (data.Properties.TryGetValue("uvlock", out val))
                        bool.TryParse(val.StringValue, out uvLock);

                    return new BlockModelWrapper(blockModel, new int2(zr, yr), uvLock, blockModelId);
                }

                Debug.LogWarning($"Model {blockModelId} is not found!");
                return null;
            }

            Debug.LogWarning("Wrapper does not contain block model!");
            return null;
        }
    }
}
