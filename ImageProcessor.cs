using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Crop and compress image to square of specified size
/// </summary>
public class ImageProcessor : Editor
{
    static int textureMaxSize = 128;

    public static void TexturesCropAndCompress(string path)
    {
        List<string> pics = new List<string>();
        pics.AddRange(Directory.GetFiles(path, "*.png"));
        pics.AddRange(Directory.GetFiles(path, "*.jpg"));
        var objs = pics.ToArray();

        for (int i = 0; i < objs.Length; i++)
        {
            Debug.Log("Processing image: " + objs[i]);

            var imp = AssetImporter.GetAtPath(objs[i]) as TextureImporter;
            if (imp == null)
            {
                Debug.LogError("Importer not assigned");
                continue;
            }
            imp.isReadable = true;
            imp.SaveAndReimport();

            var tex = AssetDatabase.LoadMainAssetAtPath(objs[i]) as Texture2D;
            if (tex == null)
            {
                Debug.LogWarning("Texture not loaded, trying to access as readable Sprite");

                var spr = AssetDatabase.LoadMainAssetAtPath(objs[i]) as Sprite;
                if (tex == null)
                {
                    Debug.LogError("Texture not loaded twice");
                    continue;
                }
                else
                    tex = TextureFromSprite(spr);
            }

            /// Image cropping
            var width = tex.width;
            var height = tex.height;
            Color[] pixels;
            if (width != height)
            {
                Debug.Log("Cropping image: " + objs[i]);

                var center = new Vector2Int(width / 2, height / 2);
                int length = 0;
                if (width > height)
                    length = height;
                else
                    length = width;
                var offset = length / 2;

                pixels = tex.GetPixels(center.x - offset, center.y - offset, length, length);
                tex = new Texture2D(length, length);
                tex.SetPixels(pixels);
                tex.Apply();
            }

            /// Compressing texture
            EditorUtility.CompressTexture(tex, TextureFormat.RGBA32, TextureCompressionQuality.Normal);

            /// Creating sprite
            var rect = new Rect(0, 0, textureMaxSize, textureMaxSize);
            var sprite = Sprite.Create(tex, rect, Vector2.one * 0.5f);
            var newPath = path + "/" + (i + 1).ToString() + ".png";

            var saveSuccess = SaveSpriteToEditorPath(sprite, newPath);

            /// Resizing sprite
            var importer = AssetImporter.GetAtPath(saveSuccess ? newPath : objs[i]) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("Null sprite from " + objs[i]);
                continue;
            }
            importer.maxTextureSize = textureMaxSize;
            importer.SaveAndReimport();

            /// Deleting old texture
            if (saveSuccess)
            {
                File.Delete(objs[i]);
                File.Delete(objs[i] + ".meta");
            }
        }
    }

    public static Texture2D TextureFromSprite(Sprite sprite)
    {
        if (sprite.rect.width != sprite.texture.width)
        {
            Texture2D newText = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);
            Color[] newColors = sprite.texture.GetPixels((int)sprite.textureRect.x,
                                                         (int)sprite.textureRect.y,
                                                         (int)sprite.textureRect.width,
                                                         (int)sprite.textureRect.height);
            newText.SetPixels(newColors);
            newText.Apply();
            return newText;
        }
        else
            return sprite.texture;
    }

    static bool SaveSpriteToEditorPath(Sprite sp, string path)
    {
        string dir = Path.GetDirectoryName(path);

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var data = sp.texture.EncodeToPNG();
        if (data == null)
        {
            Debug.LogWarning("Couldn't create new sprite at path: " + path + "\nUsing default texture");
            return false;
        }
        File.WriteAllBytes(path, data);
        AssetDatabase.Refresh();
        AssetDatabase.AddObjectToAsset(sp, path);
        AssetDatabase.SaveAssets();

        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;

        ti.spritePixelsPerUnit = sp.pixelsPerUnit;
        ti.mipmapEnabled = false;
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();

        return true;
    }
}
