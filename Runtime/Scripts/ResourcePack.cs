using System.IO;
using UnityEngine;

namespace CraftSharp.Resource
{
    public class ResourcePack
    {
        private bool isValid;
        public bool IsValid { get { return isValid; } }

        private string packName;

        public ResourcePack(string name)
        {
            isValid = false;
            packName = name;

            // Read meta file...
            var packDir = new DirectoryInfo(PathHelper.GetPackDirectoryNamed(packName));

            if (packDir.Exists)
            {
                string metaPath = packDir + "/pack.mcmeta";
                if (File.Exists(metaPath))
                {
                    string meta = File.ReadAllText(metaPath);
                    Json.JSONData metaData = Json.ParseJson(meta);

                    if (metaData.Properties.ContainsKey("pack"))
                    {
                        Json.JSONData packData = metaData.Properties["pack"];
                        if (packData.Properties.ContainsKey("pack_format"))
                        {
                            isValid = true;

                            string packFormat = packData.Properties["pack_format"].StringValue;
                            var description = string.Empty;

                            if (packData.Properties.ContainsKey("description"))
                            {
                                description = packData.Properties["description"].StringValue;
                            }

                            Debug.Log($"[{packName}] format: {packFormat}, description: {description}");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"No resource pack found at {packDir}");
            }
        }

        public void GatherResources(ResourcePackManager manager)
        {
            if (isValid)
            {
                // Assets folder...
                var assetsDir = new DirectoryInfo(PathHelper.GetPackDirectoryNamed($"{packName}/assets"));
                if (assetsDir.Exists)
                {
                    // Load textures and models
                    foreach (var nameSpaceDir in assetsDir.GetDirectories())
                    {
                        string nameSpace = nameSpaceDir.Name;

                        // Load and store all texture files...
                        var texturesDir = new DirectoryInfo($"{nameSpaceDir}/textures/");
                        int texDirLen = texturesDir.FullName.Length;

                        if (texturesDir.Exists)
                        {
                            foreach (var texFile in texturesDir.GetFiles("*.png", SearchOption.AllDirectories)) // Allow sub folders...
                            {
                                string texId = texFile.FullName.Replace('\\', '/');
                                texId = texId[texDirLen..texId.LastIndexOf('.')]; // e.g. 'block/grass_block_top'
                                var identifier = new ResourceLocation(nameSpace, texId);

                                // Add or update this entry
                                manager.TextureFileTable[identifier] = texFile.FullName.Replace('\\', '/');
                            }
                        }

                        // Load and store all model files...
                        var modelsDir = new DirectoryInfo($"{nameSpaceDir}/models/");
                        int modelDirLen = modelsDir.FullName.Length;

                        if (new DirectoryInfo($"{nameSpaceDir}/models/block").Exists)
                        {
                            foreach (var modelFile in modelsDir.GetFiles("block/*.json", SearchOption.AllDirectories)) // Allow sub folders...
                            {
                                string modelId = modelFile.FullName.Replace('\\', '/');
                                modelId = modelId[modelDirLen..]; // e.g. 'block/acacia_button.json'
                                modelId = modelId[..modelId.LastIndexOf('.')]; // e.g. 'block/acacia_button'
                                var identifier = new ResourceLocation(nameSpace, modelId);

                                // Add or update this entry
                                manager.BlockModelFileTable[identifier] = modelFile.FullName.Replace('\\', '/');
                            }
                        }

                        if (new DirectoryInfo($"{nameSpaceDir}/models/item").Exists)
                        {
                            foreach (var modelFile in modelsDir.GetFiles("item/*.json", SearchOption.AllDirectories)) // Allow sub folders...
                            {
                                string modelId = modelFile.FullName.Replace('\\', '/');
                                modelId = modelId[modelDirLen..]; // e.g. 'item/acacia_boat.json'
                                modelId = modelId[..modelId.LastIndexOf('.')]; // e.g. 'item/acacia_boat'
                                var identifier = new ResourceLocation(nameSpace, modelId);

                                // Add or update this entry
                                manager.ItemModelFileTable[identifier] = modelFile.FullName.Replace('\\', '/');
                            }
                        }

                        // Load and store all blockstate files...
                        var blockstatesDir = new DirectoryInfo($"{nameSpaceDir}/blockstates/");
                        int blockstateDirLen = blockstatesDir.FullName.Length;

                        if (blockstatesDir.Exists)
                        {
                            foreach (var statesFile in blockstatesDir.GetFiles("*.json", SearchOption.TopDirectoryOnly)) // No sub folders...
                            {
                                string blockId = statesFile.FullName.Replace('\\', '/');
                                blockId = blockId[blockstateDirLen..]; // e.g. 'grass_block.json'
                                blockId = blockId[..blockId.LastIndexOf('.')]; // e.g. 'grass_block'
                                var identifier = new ResourceLocation(nameSpace, blockId);

                                // Add or update this entry
                                manager.BlockStateFileTable[identifier] = statesFile.FullName.Replace('\\', '/');
                            }
                        }

                        // Load and store all particle files...
                        var particlesDir = new DirectoryInfo($"{nameSpaceDir}/particles/");
                        int particleDirLen = particlesDir.FullName.Length;

                        if (particlesDir.Exists)
                        {
                            foreach (var particleFile in particlesDir.GetFiles("*.json", SearchOption.TopDirectoryOnly)) // No sub folders...
                            {
                                string particleTypeId = particleFile.FullName.Replace('\\', '/');
                                particleTypeId = particleTypeId[particleDirLen..]; // e.g. 'campfire_cosy_smoke.json'
                                particleTypeId = particleTypeId[..particleTypeId.LastIndexOf('.')]; // e.g. 'campfire_cosy_smoke'
                                var identifier = new ResourceLocation(nameSpace, particleTypeId);

                                // Add or update this entry
                                manager.ParticleFileTable[identifier] = particleFile.FullName.Replace('\\', '/');
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"Cannot find path {assetsDir.FullName}");
                }
            }
            else
            {
                Debug.LogWarning("Trying to load resources from an invalid resource pack!");
            }
        }
    }
}