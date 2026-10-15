using UnityEditor;
using UnityEngine;

namespace CatDefense.EditorTools
{
    // Import settings for the packed game art: big sprite sheets must not be downscaled.
    public class GameAssetImporter : AssetPostprocessor
    {
        const string Root = "Assets/Game/Resources/Game/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
