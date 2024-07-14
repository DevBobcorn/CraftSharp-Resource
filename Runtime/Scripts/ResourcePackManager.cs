#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Mathematics;

namespace CraftSharp.Resource
{
    public class ResourcePackManager
    {
        public static readonly ResourceLocation BLANK_TEXTURE = new("builtin", "blank");
        public static readonly ResourceLocation EMPTY_TEXTURE = new("builtin", "empty");
        public static readonly ResourceLocation FOLIAGE_COLORMAP = new("colormap/foliage");
        public static readonly ResourceLocation GRASS_COLORMAP = new("colormap/grass");

        // Identifier -> Texture file path (For atlas)
        public readonly Dictionary<ResourceLocation, string> TextureFileTable = new();
        // Identifier -> Entity texture
        public readonly Dictionary<ResourceLocation, Texture2D> EntityTexture2DTable = new();

        // Identidier -> Block json model file path
        public readonly Dictionary<ResourceLocation, string> BlockModelFileTable = new();

        // Identidier -> Item json model file path
        public readonly Dictionary<ResourceLocation, string> ItemModelFileTable = new();

        // Identidier -> BlockState json model file path
        public readonly Dictionary<ResourceLocation, string> BlockStateFileTable = new();

        // Identifier -> Block model
        public readonly Dictionary<ResourceLocation, JsonModel> BlockModelTable = new();

        // Block state numeral id -> Block state geometries (One single block state may have a list of models to use randomly)
        public readonly Dictionary<int, BlockStateModel> StateModelTable = new();

        // Identifier -> Raw item model
        public readonly Dictionary<ResourceLocation, JsonModel> RawItemModelTable = new();

        // Item numeral id -> Item model
        public readonly Dictionary<int, ItemModel> ItemModelTable = new();

        public readonly HashSet<ResourceLocation> GeneratedItemModels = new();

        public readonly BlockModelLoader BlockModelLoader;
        public readonly BlockStateModelLoader StateModelLoader;

        public readonly ItemModelLoader ItemModelLoader;

        public int GeneratedItemModelPrecision { get; set; } = 16;
        public int GeneratedItemModelThickness { get; set; } =  1;

        private readonly List<ResourcePack> packs = new();

        public static readonly ResourcePackManager Instance = new();

        private ResourcePackManager()
        {
            // Block model loaders
            BlockModelLoader = new BlockModelLoader(this);
            StateModelLoader = new BlockStateModelLoader(this);

            // Item model loader
            ItemModelLoader = new ItemModelLoader(this);
        }

        public void AddPack(ResourcePack pack) => packs.Add(pack);

        public void ClearPacks()
        {
            // Clear up pack list
            packs.Clear();

            // Clear up loaded tables
            TextureFileTable.Clear();
            BlockModelTable.Clear();
            StateModelTable.Clear();
            RawItemModelTable.Clear();
            ItemModelTable.Clear();
            GeneratedItemModels.Clear();
            EntityTexture2DTable.Clear();

            // And clear up colormap data
            World.ColormapSize = 0;
            World.GrassColormapPixels = new Color32[]{ };
            World.FoliageColormapPixels = new Color32[]{ };
        }

        public void LoadPacks(DataLoadFlag flag, Action<string> updateStatus, bool preloadEntityTextures = false)
        {
            // Gather all textures and model files
            updateStatus("resource.info.gather_resource");
            foreach (var pack in packs) pack.GatherResources(this);

            var textureFlag = new DataLoadFlag();

            // Load texture atlas (on main thread)...
            updateStatus("resource.info.create_texture");
            Loom.QueueOnMainThread(() => {
                Loom.Current.StartCoroutine(GenerateAtlas(textureFlag));
            });
            
            while (!textureFlag.Finished) { /* Wait */ }
            
            // Load block models...
            updateStatus("resource.info.load_block_model");
            foreach (var blockModelId in BlockModelFileTable.Keys)
            {
                // This model loader will load this model, its parent model(if not yet loaded),
                // and then add them to the manager's model dictionary
                BlockModelLoader.LoadBlockModel(blockModelId);
            }

            // Load item models...
            // NOTE: Some resource packs, like this one https://modrinth.com/resourcepack/3d-default
            // overrides the 'item/generated' model and doesn't use 'builtin/generated', so we add a
            // line here to handle this.
            GeneratedItemModels.Add(new ResourceLocation("item/generated"));
            foreach (var itemModelId in ItemModelFileTable.Keys)
            {
                // This model loader will load this model, its parent model(if not yet loaded),
                // and then add them to the manager's model dictionary
                ItemModelLoader.LoadItemModel(itemModelId);
            }

            updateStatus("resource.info.build_blockstate_geometry");
            BuildStateGeometries();

            updateStatus("resource.info.build_item_geometry");
            BuildItemGeometries();

            // Perform integrity check...
            /*
            var statesTable = BlockStatePalette.INSTANCE.StatesTable;
 
            foreach (var stateItem in statesTable)
            {
                if (!StateModelTable.ContainsKey(stateItem.Key))
                {
                    Debug.LogWarning($"Model for {stateItem.Value}(state Id {stateItem.Key}) not loaded!");
                }
            }
            */

            if (preloadEntityTextures) // Preload entity textures (Optional)
            {
                textureFlag.Finished = false; // Reset this flag for reuse

                // Load entity textures (on main thread)...
                updateStatus("resource.info.preload_entity_texture");
                Loom.QueueOnMainThread(() => {
                    Loom.Current.StartCoroutine(PreloadEntityTextures(textureFlag));
                });

                while (!textureFlag.Finished) { /* Wait */ }
            }

            updateStatus("resource.info.resource_loaded");

            flag.Finished = true;
        }

        public void BuildStateGeometries()
        {
            // Load all blockstate files and build their block meshes...
            foreach (var blockPair in BlockStatePalette.INSTANCE.StateListTable)
            {
                var blockId = blockPair.Key;
                
                if (BlockStateFileTable.ContainsKey(blockId)) // Load the state model definition of this block
                {
                    var renderType =
                        BlockStatePalette.INSTANCE.RenderTypeTable.GetValueOrDefault(blockId, RenderType.SOLID);

                    StateModelLoader.LoadBlockStateModel(blockId, BlockStateFileTable[blockId], renderType);
                }
                else
                {
                    Debug.LogWarning($"Block state model definition not assigned for {blockId}!");
                }
            }
        }

        public void BuildItemGeometries()
        {
            // Load all item model files and build their item meshes...
            foreach (var numId in ItemPalette.INSTANCE.ItemsTable.Keys)
            {
                var item = ItemPalette.INSTANCE.ItemsTable[numId];
                var itemId = item.ItemId;

                var itemModelId = new ResourceLocation(itemId.Namespace, $"item/{itemId.Path}");

                if (ItemModelFileTable.ContainsKey(itemModelId))
                {
                    if (RawItemModelTable.ContainsKey(itemModelId))
                    {
                        var rawModel = RawItemModelTable[itemModelId];
                        var tintable = ItemPalette.INSTANCE.IsTintable(itemId);
                        var generated = GeneratedItemModels.Contains(itemModelId);

                        if (generated) // This model should be generated
                        {
                            // Get layer count of this item model
                            int layerCount = rawModel.Textures.Count;

                            rawModel.Elements.AddRange(
                                    ItemModelLoader.GetGeneratedItemModelElements(
                                            layerCount, GeneratedItemModelPrecision,
                                                    GeneratedItemModelThickness, tintable).ToArray());
                            
                            //Debug.Log($"Generating item model for {itemModelId} tintable: {tintable}");
                        }

                        var itemGeometry = new ItemGeometryBuilder(rawModel).Build();

                        RenderType renderType;

                        if (GeneratedItemModels.Contains(itemModelId))
                            renderType = RenderType.CUTOUT; // Set render type to cutout for all generated item models
                        else
                            renderType = BlockStatePalette.INSTANCE.RenderTypeTable.GetValueOrDefault(itemId, RenderType.SOLID);

                        var itemModel = new ItemModel(itemGeometry, renderType);
                        

                        // Look for and append geometry overrides to the item model
                        Json.JSONData modelData = Json.ParseJson(File.ReadAllText(ItemModelFileTable[itemModelId]));

                        if (modelData.Properties.ContainsKey("overrides"))
                        {
                            var overrides = modelData.Properties["overrides"].DataArray;

                            foreach (var o in overrides)
                            {
                                var overrideModelId = ResourceLocation.FromString(o.Properties["model"].StringValue);

                                if (RawItemModelTable.ContainsKey(overrideModelId)) // Build this override
                                {
                                    var rawOverrideModel = RawItemModelTable[overrideModelId];
                                    var overrideGenerated = GeneratedItemModels.Contains(overrideModelId);

                                    if (overrideGenerated) // This model should be generated
                                    {
                                        // Get layer count of this item model
                                        int layerCount = rawModel.Textures.Count;

                                        rawOverrideModel.Elements.AddRange(
                                                ItemModelLoader.GetGeneratedItemModelElements(
                                                        layerCount, GeneratedItemModelPrecision,
                                                                GeneratedItemModelThickness, tintable).ToArray());
                                        
                                        //Debug.Log($"Generating item model (override) for {itemModelId} tintable: {tintable}");
                                    }

                                    var overrideGeometry = new ItemGeometryBuilder(rawOverrideModel).Build();
                                    var predicate = ItemModelPredicate.fromJson(o.Properties["predicate"]);
                                    
                                    itemModel.AddOverride(predicate, overrideGeometry);
                                }
                                
                            }
                        }

                        ItemModelTable.Add(numId, itemModel);
                    }
                    else
                    {
                        Debug.LogWarning($"Item model for {itemId} not found at {itemModelId}!");
                    }
                }
                else
                {
                    Debug.LogWarning($"Item model not assigned for {itemModelId}");
                }
            }
        }

        private readonly Dictionary<ResourceLocation, TextureInfo> texAtlasTable = new();

        /// <summary>
        /// Get texture uvs (x, y, depth in atlas array) and texture animation info (frame count, frame interval, frame offset)
        /// </summary>
        public (float3[] uvs, float4 anim) GetUVs(ResourceLocation identifier, Vector4 part, int areaRot)
        {
            var info = GetTextureInfo(identifier);
            if (info.frameCount > 1) // This texture is animated
            {
                float oneX = info.bounds.width / info.framePerRow; // Frame size on texture atlas array

                return (GetUVsAt(info.bounds, info.index, oneX, oneX, part, areaRot),
                        new(info.frameCount, info.frameInterval, oneX, info.framePerRow));
            }
            return (GetUVsAt(info.bounds, info.index, info.bounds.width, info.bounds.height, part, areaRot), float4.zero);
        }

        private float3[] GetUVsAt(Rect bounds, int index, float oneU, float oneV, Vector4 part, int areaRot)
        {
            // Get texture offset in atlas
            float3 o = new(bounds.xMin, bounds.yMax - oneV, index + 0.1F);

            // vect:  x,  y,  z,  w
            // vect: x1, y1, x2, y2
            float u1 = part.x * oneU, v1 = part.y * oneV;
            float u2 = part.z * oneU, v2 = part.w * oneV;

            return areaRot switch
            {
                0 => new float3[]{ new float3(       u1, oneV - v1, 0F) + o, new float3(       u2, oneV - v1, 0F) + o, new float3(       u1, oneV - v2, 0F) + o, new float3(       u2, oneV - v2, 0F) + o }, //   0 Deg
                1 => new float3[]{ new float3(       v1,        u1, 0F) + o, new float3(       v1,        u2, 0F) + o, new float3(       v2,        u1, 0F) + o, new float3(       v2,        u2, 0F) + o }, //  90 Deg
                2 => new float3[]{ new float3(oneU - u1,        v1, 0F) + o, new float3(oneU - u2,        v1, 0F) + o, new float3(oneU - u1,        v2, 0F) + o, new float3(oneU - u2,        v2, 0F) + o }, // 180 Deg
                3 => new float3[]{ new float3(oneV - v1, oneV - u1, 0F) + o, new float3(oneV - v1, oneU - u2, 0F) + o, new float3(oneV - v2, oneU - u1, 0F) + o, new float3(oneV - v2, oneU - u2, 0F) + o }, // 270 Deg

                _ => new float3[]{ new float3(       u1, oneV - v1, 0F) + o, new float3(       u2, oneV - v1, 0F) + o, new float3(       u1, oneV - v2, 0F) + o, new float3(       u2, oneV - v2, 0F) + o }  // Default
            };
        }

        private TextureInfo GetTextureInfo(ResourceLocation identifier)
        {
            if (texAtlasTable.ContainsKey(identifier))
                return texAtlasTable[identifier];
            
            Debug.Log($"Texture {identifier} is not in atlas!");
            
            // Return missing no texture
            return texAtlasTable[ResourceLocation.INVALID];
        }

        // atlasArrays[0]: Not mipped
        // atlasArrays[1]: Mipped
        private readonly Texture2DArray?[] atlasArrays = new Texture2DArray?[2];

        public Texture2DArray GetAtlasArray(bool mipped)
        {
            return mipped ? atlasArrays[1]! : atlasArrays[0]!;
        }

        public const int ATLAS_SIZE = 2048;

        private record TextureAnimationInfo
        {
            public int framePerRow;
            public int frameCount;
            public float frameInterval;
            public bool interpolate;
            public TextureAnimationInfo(int f, int fRow, float i, bool itpl)
            {
                frameCount = f;
                framePerRow = fRow;
                frameInterval = i;
                interpolate = itpl;
            }
        }

        private (Texture2D, TextureAnimationInfo?) LoadSingleTexture(ResourceLocation texId, string texFilePath)
        {
            Texture2D tex = new(2, 2);
            tex.LoadImage(File.ReadAllBytes(texFilePath));

            if (File.Exists($"{texFilePath}.mcmeta")) // Has animation info
            {
                int spriteCount = tex.height / tex.width;

                var animJson = Json.ParseJson(File.ReadAllText($"{texFilePath}.mcmeta")).Properties["animation"];

                int[] frames;
                
                if (animJson.Properties.ContainsKey("frames")) // Place the frames in specified order
                {
                    frames = animJson.Properties["frames"].DataArray.Select(x => int.Parse(x.StringValue)).ToArray();
                }
                else // Place the frames in ordinal order, from top to bottom
                {
                    frames = Enumerable.Range(0, spriteCount).ToArray();
                }
                
                int frameCount = frames.Length;

                if (frameCount > 1)
                {
                    float frameInterval;

                    if (animJson.Properties.ContainsKey("frametime")) // Use specified frame interval
                        frameInterval = float.Parse(animJson.Properties["frametime"].StringValue, CultureInfo.InvariantCulture.NumberFormat) * 0.05F;
                    else // Use default frame interval
                        frameInterval = 0.05F;
                    
                    bool interpolate;

                    if (animJson.Properties.ContainsKey("interpolate"))
                        interpolate = animJson.Properties["interpolate"].StringValue.ToLower().Equals("true");
                    else
                        interpolate = false;

                    int frameSize = tex.width;
                    
                    int framePerRow = Mathf.CeilToInt(math.sqrt(frameCount));
                    int framePerCol = Mathf.CeilToInt((float) frameCount / framePerRow);

                    // Re-arrange the texture
                    Texture2D rearranged = new(framePerRow * frameSize, framePerCol * frameSize);
                    //Debug.Log($"Animated texture {texId} (pr: {framePerRow} pc: {framePerCol} f: {frameCount})");
                    //Debug.Log($"Animated texture {texId} (frames: {string.Join(",", frames)})");

                    for (int fi = 0;fi < frameCount;fi++)
                    {
                        int framePos = frames[fi];

                        // Copy pixel data
                        Graphics.CopyTexture(tex, 0, 0, 0, (spriteCount - 1 - framePos) * frameSize, frameSize, frameSize,
                                rearranged, 0, 0, (fi % framePerRow) * frameSize, (framePerCol - 1 - fi / framePerRow) * frameSize);
                        
                    }

                    return (rearranged, new(frameCount, framePerRow, frameInterval, interpolate));
                }
            }

            return (tex, null);
        }
        
        private Texture2D GetBlankTexture()
        {
            Texture2D tex = new(16, 16);
            Color32 white = Color.white;

            var colors = Enumerable.Repeat(white, 16 * 16).ToArray();
            tex.SetPixels32(colors);
            // No need to update mipmap because it'll be
            // stitched into the atlas later
            tex.Apply(false);

            return tex;
        }

        private Texture2D GetEmptyTexture()
        {
            Texture2D tex = new(16, 16);
            Color32 empty = new(0, 0, 0, 0);

            var colors = Enumerable.Repeat(empty, 16 * 16).ToArray();
            tex.SetPixels32(colors);
            // No need to update mipmap because it'll be
            // stitched into the atlas later
            tex.Apply(false);

            return tex;
        }

        private Texture2D GetMissingTexture()
        {
            Texture2D tex = new(16, 16);
            Color32 black = Color.black;
            Color32 magenta = Color.magenta;

            var colors = new Color32[16 * 16];

            for (int i = 0;i < 8;i++)
            {
                for (int j = 0;j < 8;j++)
                    colors[(i << 4) + j] =   black;
                
                for (int j = 8;j < 16;j++)
                    colors[(i << 4) + j] = magenta;
            }

            for (int i = 8;i < 16;i++)
            {
                for (int j = 0;j < 8;j++)
                    colors[(i << 4) + j] = magenta;
                
                for (int j = 8;j < 16;j++)
                    colors[(i << 4) + j] =   black;
            }

            tex.SetPixels32(colors);
            // No need to update mipmap because it'll be
            // stitched into the atlas later
            tex.Apply(false);

            return tex;
        }

        private Texture2D GetMissingEntityTexture(int width, int height)
        {
            Texture2D tex = new(width, height)
            {
                filterMode = FilterMode.Point
            };
            Color32 magenta = Color.magenta;
            Color32 black = Color.black;

            var colors = Enumerable.Repeat(magenta, width * height).ToArray();

            for (int i = 0;i < height;i++)
            {
                for (int j = 0;j < width;j++)
                {
                    if ( ( (i >> 2) + (j >> 2) ) % 2 == 0 )
                    colors[i * width + j] = black;
                }     
            }

            tex.SetPixels32(colors);
            // Update texture and mipmaps
            tex.Apply(true);

            return tex;
        }

        private IEnumerator GenerateAtlas(DataLoadFlag atlasGenFlag)
        {
            texAtlasTable.Clear(); // Clear previously loaded table...

            var texDict = TextureFileTable;
            var textureIdSet = new HashSet<ResourceLocation>();

            // Collect referenced textures
            var modelFilePaths = BlockModelFileTable.Values.ToList();
            modelFilePaths.AddRange(ItemModelFileTable.Values);
            
            var gatherTexturesTask = Task.Run(() => {
                foreach (var modelFile in modelFilePaths)
                {
                    var model = Json.ParseJson(File.ReadAllText(modelFile));

                    if (model.Properties.ContainsKey("textures"))
                    {
                        var texData = model.Properties["textures"].Properties;
                        foreach (var texItem in texData)
                        {
                            if (!texItem.Value.StringValue.StartsWith('#'))
                            {
                                var texId = ResourceLocation.FromString(texItem.Value.StringValue);

                                if (texDict.ContainsKey(texId))
                                    textureIdSet.Add(texId);
                                //else
                                //    Debug.LogWarning($"Texture {texId} not found in dictionary! (Referenced in {modelFile})");
                            }
                        }
                    }
                }
            });

            while (!gatherTexturesTask.IsCompleted) yield return null;

            // Append liquid textures, which are not referenced in model files, but will be used by fluid mesh
            foreach (var texId in FluidGeometry.LiquidTextures)
            {
                if (texDict.ContainsKey(texId))
                {
                    textureIdSet.Add(texId);
                }
            }

            // Array for textures in collection, plus one blank texture, one empty texture and one missing texture
            var totalCount = 3 + textureIdSet.Count;
            var textureInfos = new (Texture2D, TextureAnimationInfo?)[totalCount];
            var ids = new ResourceLocation[totalCount];

            int count = 0;

            // Blank texture
            ids[count] = BLANK_TEXTURE;
            textureInfos[count] = (GetBlankTexture(), null);
            count++;

            // Empty texture
            ids[count] = EMPTY_TEXTURE;
            textureInfos[count] = (GetEmptyTexture(), null);
            count++;

            // Missing texture
            ids[count] = ResourceLocation.INVALID;
            textureInfos[count] = (GetMissingTexture(), null);
            count++;

            foreach (var texId in textureIdSet) // Load texture files...
            {
                var texFilePath = texDict[texId];
                ids[count] = texId;
                //Debug.Log($"Loading {texId} from {texFilePath}");
                textureInfos[count++] = LoadSingleTexture(texId, texFilePath);

                if (count % 5 == 0) yield return null;
            }
            
            int curTexIndex = 0, curAtlasIndex = 0;
            List<Texture2D> atlases = new();

            int totalVolume = ATLAS_SIZE * ATLAS_SIZE;
            int maxContentVolume = (int)(totalVolume * 0.97F);

            do
            {
                // First count all the textures to be stitched onto this atlas
                int lastTexIndex = curTexIndex - 1, curVolume = 0; // lastTexIndex is inclusive

                while (true)
                {
                    if (lastTexIndex >= totalCount - 1)
                        break;

                    (var nextTex, var nextAnimInfo) = textureInfos[lastTexIndex + 1];
                    curVolume += nextTex.width * nextTex.height;

                    if (curVolume < maxContentVolume)
                        lastTexIndex++;
                    else
                        break;
                }

                int consumedTexCount = lastTexIndex + 1 - curTexIndex;

                if (consumedTexCount == 0)
                {
                    // In this occasion the texture is too large and can only be scaled a bit and placed on a separate atlas
                    lastTexIndex = curTexIndex;
                    consumedTexCount = 1;
                }

                // Then we go stitch 'em        (inclusive)..(exclusive)
                var textureInfosConsumed = textureInfos[curTexIndex..(lastTexIndex + 1)];

                // First assign a placeholder
                var atlas = new Texture2D(ATLAS_SIZE, ATLAS_SIZE)
                {
                    filterMode = FilterMode.Point
                };

                var rects = atlas.PackTextures(textureInfosConsumed.Select(x => x.Item1).ToArray(), 0, ATLAS_SIZE, false);

                if (atlas.width != ATLAS_SIZE || atlas.height != ATLAS_SIZE)
                {
                    // Size not right, replace it (usually the last atlas in array which doesn't
                    // have enough textures to take up all the place and thus is smaller in size)
                    var newAtlas = new Texture2D(ATLAS_SIZE, ATLAS_SIZE);

                    Graphics.CopyTexture(atlas, 0, 0, 0, 0, atlas.width, atlas.height, newAtlas, 0, 0, 0, 0);

                    float scaleX = atlas.width  / (float) ATLAS_SIZE;
                    float scaleY = atlas.height / (float) ATLAS_SIZE;

                    // Rescale the texture boundaries
                    for (int i = 0;i < rects.Length;i++)
                    {
                        rects[i] = new Rect(
                            rects[i].x     * scaleX,    rects[i].y      * scaleY,
                            rects[i].width * scaleX,    rects[i].height * scaleY
                        );
                    }

                    atlas = newAtlas;
                }

                atlases.Add(atlas);

                yield return null;

                for (int i = 0;i < consumedTexCount;i++)
                {
                    //Debug.Log($"{ids[curTexIndex + i]} => ({curAtlasIndex}) {rects[i].xMin} {rects[i].xMax} {rects[i].yMin} {rects[i].yMax}");
                    var curAnimInfo = textureInfos[curTexIndex + i].Item2;
                    
                    if (curAnimInfo is null)
                        texAtlasTable.Add(ids[curTexIndex + i], new(rects[i], curAtlasIndex));
                    else
                        texAtlasTable.Add(ids[curTexIndex + i], new(rects[i], curAtlasIndex, curAnimInfo.frameCount,
                                rects[i].width / curAnimInfo.framePerRow, curAnimInfo.framePerRow, curAnimInfo.interpolate, curAnimInfo.frameInterval));
                }

                curTexIndex += consumedTexCount;
                curAtlasIndex++;

                yield return null;

            }
            while (curTexIndex < totalCount);

            var atlasArray0 = new Texture2DArray(ATLAS_SIZE, ATLAS_SIZE, curAtlasIndex, TextureFormat.RGBA32,  2, false);
            var atlasArray1 = new Texture2DArray(ATLAS_SIZE, ATLAS_SIZE, curAtlasIndex, TextureFormat.RGBA32,  4, false);

            atlasArray0.filterMode = FilterMode.Point;
            atlasArray1.filterMode = FilterMode.Point;

            for (int index = 0;index < atlases.Count;index++)
            {
                atlasArray0.SetPixels(atlases[index].GetPixels(), index, 0);
                atlasArray1.SetPixels(atlases[index].GetPixels(), index, 0);

                yield return null;
            }

            atlasArray0.Apply(true, false);
            atlasArray1.Apply(true, false);

            atlasArrays[0] = atlasArray0;
            atlasArrays[1] = atlasArray1;

            // Read biome colormaps from resource pack
            if (texDict.ContainsKey(FOLIAGE_COLORMAP))
            {
                //Debug.Log($"Loading foliage colormap from {texDict[FOLIAGE_COLORMAP]}");
                var mapTex = new Texture2D(2, 2);
                mapTex.LoadImage(File.ReadAllBytes(texDict[FOLIAGE_COLORMAP]));
                
                World.ColormapSize = mapTex.width;
                if (mapTex.height != World.ColormapSize)
                    Debug.LogWarning($"Colormap size inconsistency: expected {World.ColormapSize}, got {mapTex.height}");

                World.FoliageColormapPixels = mapTex.GetPixels32();
            }
            
            if (texDict.ContainsKey(GRASS_COLORMAP))
            {
                //Debug.Log($"Loading grass colormap from {texDict[GRASS_COLORMAP]}");
                var mapTex = new Texture2D(2, 2);
                mapTex.LoadImage(File.ReadAllBytes(texDict[GRASS_COLORMAP]));
                
                if (mapTex.width != World.ColormapSize)
                    Debug.LogWarning($"Colormap size inconsistency: expected {World.ColormapSize}, got {mapTex.width}");
                if (mapTex.height != World.ColormapSize)
                    Debug.LogWarning($"Colormap size inconsistency: expected {World.ColormapSize}, got {mapTex.height}");

                World.GrassColormapPixels = mapTex.GetPixels32();
            }

            atlasGenFlag.Finished = true;
        }

        /// <summary>
        /// Preload any texture whose id starts with "entity" into the entity texture table.
        /// </summary>
        private IEnumerator PreloadEntityTextures(DataLoadFlag texLoadFlag)
        {
            EntityTexture2DTable.Clear();

            // Collect entity textures
            foreach (var tex in TextureFileTable)
            {
                if (tex.Key.Path.StartsWith("entity"))
                {
                    //Debug.Log($"Entity texture: {tex.Key}");
                    LoadEntityTextureFromPacks(tex.Key);
                }

                yield return null;
            }

            texLoadFlag.Finished = true;
        }

        /// <summary>
        /// Load an entity texture from an image file in current texture file table.
        /// </summary>
        /// <param name="texId">Texture id</param>
        public Texture2D LoadEntityTextureFromPacks(ResourceLocation texId, int defWidth = 32, int defHeight = 32)
        {
            if (EntityTexture2DTable.ContainsKey(texId))
            {
                return EntityTexture2DTable[texId];
            }

            Texture2D tex;

            if (TextureFileTable.ContainsKey(texId))
            {
                tex = new Texture2D(2, 2) { filterMode = FilterMode.Point };
                tex.LoadImage(File.ReadAllBytes(TextureFileTable[texId]));
            }
            else
            {
                Debug.LogWarning($"Unable to find entity texture {texId} from resource packs!");
                tex = GetMissingEntityTexture(defWidth, defHeight);
            }

            // Register this texture
            EntityTexture2DTable.Add(texId, tex);

            return tex;
        }

        /// <summary>
        /// Load an entity texture from an image file at a given URL.
        /// </summary>
        /// <param name="texId">Texture id</param>
        /// <param name="url">URL to retrieve the image file from</param>
        public Texture2D LoadEntityTextureFromUrl(ResourceLocation texId, string url)
        {
            var tex = new Texture2D(2, 2) { filterMode = FilterMode.Point };
            
            // TODO: Implement texture file downloading
            //tex.LoadImage(bytes);

            // Register this texture
            EntityTexture2DTable.Add(texId, tex);

            return tex;
        }
    }
}