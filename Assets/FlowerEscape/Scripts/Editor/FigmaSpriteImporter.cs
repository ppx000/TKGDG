using UnityEditor;
using UnityEngine;

namespace FlowerEscape.EditorTools
{
    // Import rules for the images exported from Figma at 3x.
    // The canvas uses 1 unit = 1 Figma point with a reference PPU of 100,
    // so 300 PPU makes "Set Native Size" match the Figma size exactly.
    public class FigmaSpriteImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/FlowerEscape/Art/Figma/";
        public const float PixelsPerUnit = 300f;
        const int MaxSize = 4096;

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(Folder))
                Apply((TextureImporter)assetImporter);
        }

        // Returns true when a setting had to change (the caller must reimport).
        public static bool Apply(TextureImporter importer)
        {
            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit)
                || importer.mipmapEnabled
                || !importer.alphaIsTransparency
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.maxTextureSize != MaxSize;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = MaxSize;
            return changed;
        }
    }
}
