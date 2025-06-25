using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Mathematics;

namespace CraftSharp.Resource
{
    public class ItemModelLoader
    {
        private const string GENERATED = "builtin/generated";
        private const string ENTITY = "builtin/entity";

        private static readonly JsonModel INVALID_MODEL = new();

        private readonly ResourcePackManager manager;

        public ItemModelLoader(ResourcePackManager manager)
        {
            this.manager = manager;
        }

        // Cached generated models (layerCount, precision, thickness, useItemColor) => model
        private static readonly Dictionary<int4, List<JsonModelElement>> generatedModels = new();

        public static List<JsonModelElement> GetGeneratedItemModelElements(int layerCount, int precision, int thickness, bool useItemColor)
        {
            int4 modelKey = new(layerCount, precision, thickness, useItemColor ? 1 : 0);

            if (!generatedModels.ContainsKey(modelKey)) // Not present yet, generate it
            {
                //Debug.Log($"Generating item model... Layer count: {layerCount} Precision: {precision}");
                var model = new List<JsonModelElement>();
                var stepLength = 16F / precision;
                var halfThick  = thickness / 2F;

                for (int layer = 0;layer < layerCount;layer++)
                {
                    var elem = new JsonModelElement();
                    var layerTexName = $"layer{layer}";

                    elem.from = new(8F - halfThick,  0F,  0F);
                    elem.to   = new(8F + halfThick, 16F, 16F);

                    elem.faces.Add(FaceDir.NORTH, new() {
                        uv = new(16F, 0F, 0F, 16F),
                        texName = layerTexName,
                        tintIndex = useItemColor ? layer : -1
                    });

                    elem.faces.Add(FaceDir.SOUTH, new() {
                        uv = new(0F, 0F, 16F, 16F),
                        texName = layerTexName,
                        tintIndex = useItemColor ? layer : -1
                    });

                    for (int i = 0;i < precision;i++)
                    {
                        var fracL1 =       i * stepLength;
                        var fracR1 = (i + 1) * stepLength;
                        var fracL2 = (precision -       i) * stepLength;
                        var fracR2 = (precision - (i + 1)) * stepLength;

                        var vertStripe = new JsonModelElement();
                        var horzStripe = new JsonModelElement();

                        vertStripe.from = new(8F - halfThick,  0F, fracL2);
                        vertStripe.to   = new(8F + halfThick, 16F, fracR2);
                        horzStripe.from = new(8F - halfThick, fracL2,  0F);
                        horzStripe.to   = new(8F + halfThick, fracR2, 16F);

                        // Left faces
                        vertStripe.faces.Add(FaceDir.EAST, new() {
                            uv = new(16F - fracR1, 0F, 16F - fracL1, 16F),
                            texName = layerTexName,
                            tintIndex = useItemColor ? layer : -1
                        });
                        // Right faces
                        vertStripe.faces.Add(FaceDir.WEST, new() {
                            uv = new(      fracR2, 0F,       fracL2, 16F),
                            texName = layerTexName,
                            tintIndex = useItemColor ? layer : -1
                        });
                        // Top faces
                        horzStripe.faces.Add(FaceDir.UP, new() {
                            uv = new(0F,       fracL1, 16F,       fracR1),
                            texName = layerTexName,
                            tintIndex = useItemColor ? layer : -1
                        });
                        // Bottom faces
                        horzStripe.faces.Add(FaceDir.DOWN, new() {
                            uv = new(0F, 16F - fracL2, 16F, 16F - fracR2),
                            texName = layerTexName,
                            tintIndex = useItemColor ? layer : -1
                        });

                        model.Add(vertStripe);
                        model.Add(horzStripe);
                    }

                    model.Add(elem);
                }

                // Generation complete, add it into the dictionary
                generatedModels.Add(modelKey, model);
            }

            return generatedModels[modelKey];
        }

        /// <summary>
        /// Load an item model with given identifier.
        /// <br/>
        /// Model file table should be loaded before calling this.
        /// </summary>
        public JsonModel LoadItemModel(ResourceLocation identifier)
        {
            // Check if this model is loaded already...
            if (manager.RawItemModelTable.TryGetValue(identifier, out var itemModel))
                return itemModel;
                 
            if (manager.ItemModelFileTable.TryGetValue(identifier, out string modelFilePath))
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
                    
                    switch (parentIdentifier.Path) {
                        case GENERATED:
                            if (manager.GeneratedItemModels.Add(identifier))
                            {
                                //Debug.Log($"Marked item model {identifier} as generated (Direct)");
                            }
                            model = new(); // Return a placeholder model
                            break;
                        case ENTITY:
                            if (manager.BuiltinEntityModels.Add(identifier))
                            {
                                Debug.Log($"Marked item model {identifier} as builtin entity (Direct)");
                            }
                            model = new(); // Return a placeholder model
                            break;
                        default:
                            if ((manager.RawItemModelTable.TryGetValue(parentIdentifier, out var parentModel)
                                && !manager.GeneratedItemModels.Contains(parentIdentifier)) ||
                                manager.BlockModelTable.TryGetValue(parentIdentifier, out parentModel))
                            {
                                // This parent is not generated and is already loaded as an item model
                                // - or -
                                // This parent is already loaded an a block model, get it...
                                if (manager.BuiltinEntityModels.Contains(parentIdentifier)) // Also mark self as builtin entity
                                    if (manager.BuiltinEntityModels.Add(identifier))
                                    {
                                        Debug.Log($"Marked item model {identifier} as builtin entity (Inherited from {parentIdentifier})");
                                    }
                            }
                            else
                            {
                                // This parent is not yet loaded or is a generated model, load it...
                                parentModel = LoadItemModel(parentIdentifier);
                                
                                if (manager.GeneratedItemModels.Contains(parentIdentifier)) // Also mark self as generated
                                    if (manager.GeneratedItemModels.Add(identifier))
                                    {
                                        //Debug.Log($"Marked item model {identifier} as generated (Inherited from {parentIdentifier})");
                                    }
                                
                                if (manager.BuiltinEntityModels.Contains(parentIdentifier)) // Also mark self as builtin entity
                                    if (manager.BuiltinEntityModels.Add(identifier))
                                    {
                                        Debug.Log($"Marked item model {identifier} as builtin entity (Inherited from {parentIdentifier})");
                                    }

                                if (parentModel == INVALID_MODEL)
                                    Debug.LogWarning($"Failed to load parent of {identifier}");
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

                            break;
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
                if (manager.RawItemModelTable.TryAdd(identifier, model))
                {
                    //Debug.Log("Model loaded: " + identifier);
                }
                else
                {
                    Debug.LogWarning($"Trying to add model twice: {identifier}");
                }

                return model;
            }

            Debug.LogWarning($"Item model file not found: {identifier}");
            return INVALID_MODEL;
        }
    }
}