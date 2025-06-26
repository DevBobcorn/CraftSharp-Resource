using System.IO;
using UnityEngine;

namespace CraftSharp.Resource
{
    public class BlockModelLoader
    {
        private static readonly JsonModel INVALID_MODEL = new();

        private readonly ResourcePackManager manager;

        public BlockModelLoader(ResourcePackManager manager)
        {
            this.manager = manager;
        }

        /// <summary>
        /// Load a block model with given identifier.
        /// <br/>
        /// Model file table should be loaded before calling this.
        /// </summary>
        public JsonModel LoadBlockModel(ResourceLocation identifier)
        {
            // Check if this model is loaded already...
            if (manager.BlockModelTable.TryGetValue(identifier, out var blockModel))
                return blockModel;
            
            if (manager.BlockModelFileTable.TryGetValue(identifier, out string modelFilePath))
            {
                JsonModel model = new JsonModel();

                string modelText = File.ReadAllText(modelFilePath);
                Json.JSONData modelData = Json.ParseJson(modelText);

                bool containsTextures = modelData.Properties.ContainsKey("textures");
                bool containsElements = modelData.Properties.ContainsKey("elements");
                bool containsDisplay  = modelData.Properties.ContainsKey("display");

                if (modelData.Properties.TryGetValue("parent", out var parentData))
                {
                    ResourceLocation parentIdentifier = ResourceLocation.FromString(parentData.StringValue.Replace('\\', '/'));

                    if (manager.BlockModelTable.TryGetValue(parentIdentifier, out var parentModel))
                    {
                        // This parent is already loaded, get it...
                    }
                    else
                    {
                        // This parent is not yet loaded, load it...
                        parentModel = LoadBlockModel(parentIdentifier);
                    }

                    // Inherit parent textures...
                    foreach (var tex in parentModel.Textures)
                    {
                        model.Textures.Add(tex.Key, tex.Value);
                    }

                    // Inherit parent elements only if itself doesn't have those defined...
                    if (!containsElements)
                    {
                        foreach (var elem in parentModel.Elements)
                        {
                            model.Elements.Add(elem);
                        }
                    }

                    // Inherit parent display transforms...
                    foreach (var trs in parentModel.DisplayTransforms)
                    {
                        model.DisplayTransforms.Add(trs.Key, trs.Value);
                    }
                }

                if (containsTextures) // Add / Override texture references
                {
                    var texData = modelData.Properties["textures"].Properties;
                    foreach (var texItem in texData)
                    {
                        TextureReference texRef;
                        if (texItem.Value.StringValue.StartsWith('#'))
                        {
                            texRef = new TextureReference(true, texItem.Value.StringValue[1..]); // Remove the leading '#'...
                        }
                        else
                        {
                            texRef = new TextureReference(false, texItem.Value.StringValue);
                        }

                        model.Textures[texItem.Key] = texRef;
                    }
                }

                if (containsElements) // Discard parent elements and use own ones
                {
                    var elemData = modelData.Properties["elements"].DataArray;
                    foreach (var elemItem in elemData)
                    {
                        model.Elements.Add(JsonModelElement.FromJson(elemItem));
                    }
                }

                if (containsDisplay) // Add / Override display transforms...
                {
                    var trsData = modelData.Properties["display"].Properties;
                    foreach (var trsItem in trsData)
                    {
                        var displayPos = DisplayPositionHelper.FromString(trsItem.Key);
                        if (displayPos == DisplayPosition.Unknown)
                        {
                            Debug.LogWarning($"Unknown display position: {trsItem.Key}, skipping...");
                            continue;
                        }

                        var displayTransform = VectorUtil.Json2DisplayTransform(trsItem.Value);

                        model.DisplayTransforms[displayPos] = displayTransform;
                    }
                }

                // It's also possible that this model is added somewhere before
                // during parent loading process (though it shouldn't happen)
                if (manager.BlockModelTable.TryAdd(identifier, model))
                {
                    //Debug.Log("Model loaded: " + identifier);
                }
                else
                {
                    Debug.LogWarning($"Trying to add model twice: {identifier}");
                }

                return model;
            }

            Debug.LogWarning($"Block model file not found: {identifier}");
            return INVALID_MODEL;
        }
    }
}