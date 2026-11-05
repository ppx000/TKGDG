using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FlowerEscape.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace FlowerEscape.EditorTools
{
    // Builds Assets/FlowerEscape/Scenes/Gameplay.unity from the layout exported from Figma.
    // The canvas reference resolution is the Figma frame (402x874), so every position and
    // font size in the spec is used as-is: 1 canvas unit = 1 Figma point.
    //
    // Responsiveness comes from how each group is anchored:
    //   HUD corners / ammo counters / watch-ad button -> screen corners (top)
    //   battlefield                                   -> top centre column
    //   boards, boosters                              -> bottom centre column
    //   shop bar, banner                              -> bottom, full width
    // On 9:21 the extra height opens up between the top and bottom groups (over the water);
    // on 3:4 the extra width becomes side margins while the background fills the screen.
    public static class GameplaySceneBuilder
    {
        const string Root = "Assets/FlowerEscape";
        const string LayoutPath = Root + "/Layout/gameplay_layout.json";
        const string SpriteFolder = Root + "/Art/Figma";
        const string FontPath = Root + "/Fonts/Lalezar-Regular.ttf";
        const string FontAssetPath = Root + "/Fonts/Lalezar SDF.asset";
        const string MaterialFolder = Root + "/Fonts/Materials";
        const string ScenePath = Root + "/Scenes/Gameplay.unity";
        const string ScreenshotFolder = "Screenshots";

        // SDF settings: the padding leaves room for the thickest Figma text stroke (~11% of the size).
        const int SamplingPointSize = 64;
        const int AtlasPadding = 16;
        const string Charset = " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,:;!?+-/%()'";

        enum Anchor { TopLeft, Top, TopRight, Bottom, BottomStretch }

        // Draw order inside each parent follows this list.
        static readonly (string state, string group, string objectName, Anchor anchor)[] Groups =
        {
            ("Combat", "Battlefield", "Battlefield", Anchor.Top),
            ("Combat", "Board", "Board", Anchor.Bottom),
            ("Combat", "Boosters", "Boosters", Anchor.Bottom),
            ("Combat", "SideResources", "AmmoCounters", Anchor.TopRight),
            ("BuyGear", "Board", "Board", Anchor.Bottom),
            ("BuyGear", "ShopBar", "ShopBar", Anchor.BottomStretch),
            ("BuyGear", "WatchAd", "WatchAdReward", Anchor.TopRight),
            ("Combat", "HUD_TopLeft", "TopLeft", Anchor.TopLeft),
            ("Combat", "HUD_TopCenter", "TopCenter", Anchor.Top),
            ("Combat", "HUD_TopRight", "TopRight", Anchor.TopRight),
            ("Combat", "Banner", "BannerAd", Anchor.BottomStretch),
        };

        static readonly (string label, int width, int height)[] CaptureSizes =
        {
            ("9x21", 1080, 2520),
            ("3x4", 1536, 2048),
            ("figma", 1206, 2622),
        };

        class BuildContext
        {
            public FigmaLayoutSpec Spec;
            public TMP_FontAsset Font;
            public readonly Dictionary<string, RectTransform> Objects = new Dictionary<string, RectTransform>();
            public readonly Dictionary<string, Rect> Rects = new Dictionary<string, Rect>();
            public readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        }

        [MenuItem("Tools/Flower Escape/Build Gameplay Scene", priority = 0)]
        public static void BuildScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var spec = JsonUtility.FromJson<FigmaLayoutSpec>(File.ReadAllText(LayoutPath));
            PrepareSprites(spec);

            // NewScene unloads unused assets, so load the font afterwards or the reference goes stale.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ctx = new BuildContext { Spec = spec, Font = EnsureFontAsset() };
            var controller = BuildHierarchy(ctx);
            controller.ApplyStateImmediate(GameplayState.BuyGear);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AddSceneToBuildSettings();
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            Selection.activeGameObject = controller.gameObject;
            Debug.Log($"[Flower Escape] Built {ScenePath} from {spec.nodes.Count} Figma layers.");
        }

        [MenuItem("Tools/Flower Escape/Preview State/Combat", priority = 20)]
        static void PreviewCombat() => Preview(GameplayState.Combat);

        [MenuItem("Tools/Flower Escape/Preview State/Buy Gear", priority = 21)]
        static void PreviewBuyGear() => Preview(GameplayState.BuyGear);

        // Renders both states at 9:21, 3:4 and the Figma aspect into <project>/Screenshots.
        [MenuItem("Tools/Flower Escape/Capture Screenshots", priority = 40)]
        public static void CaptureScreenshots()
        {
            var controller = Object.FindObjectOfType<GameplayUIController>(true);
            if (controller == null)
            {
                Debug.LogWarning("[Flower Escape] Open the Gameplay scene first (Tools > Flower Escape > Build Gameplay Scene).");
                return;
            }

            Directory.CreateDirectory(ScreenshotFolder);
            foreach (var state in new[] { GameplayState.BuyGear, GameplayState.Combat })
            foreach (var size in CaptureSizes)
                Capture(controller, state, size.width, size.height, $"{ScreenshotFolder}/{state}_{size.label}.png");

            Debug.Log($"[Flower Escape] Screenshots saved to {Path.GetFullPath(ScreenshotFolder)}");
            if (!Application.isBatchMode)
                EditorUtility.RevealInFinder(ScreenshotFolder);
        }

        // Command-line entry point:
        // Unity.exe -batchmode -projectPath <path> -executeMethod FlowerEscape.EditorTools.GameplaySceneBuilder.BuildAndCaptureBatch -quit
        public static void BuildAndCaptureBatch()
        {
            BuildScene();
            CaptureScreenshots();
        }

        static void PrepareSprites(FigmaLayoutSpec spec)
        {
            AssetDatabase.Refresh();
            var borders = spec.nodes
                .Where(n => n.kind == "image" && n.stretchX)
                .GroupBy(n => n.sprite)
                .ToDictionary(g => g.Key, g => g.First().sliceX * spec.exportScale);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    bool changed = FigmaSpriteImporter.Apply(importer);

                    var border = borders.TryGetValue(Path.GetFileNameWithoutExtension(path), out var px)
                        ? new Vector4(px, 0f, px, 0f)
                        : Vector4.zero;
                    if (importer.spriteBorder != border)
                    {
                        importer.spriteBorder = border;
                        changed = true;
                    }

                    if (changed)
                        importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        static TMP_FontAsset EnsureFontAsset()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset != null)
                return fontAsset;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            fontAsset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA, 1024, 1024);
            fontAsset.name = "Lalezar SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            fontAsset.atlasTextures[0].name = "Lalezar SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            fontAsset.material.name = "Lalezar SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            fontAsset.TryAddCharacters(Charset);
            EditorUtility.SetDirty(fontAsset.atlasTextures[0]);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        static GameplayUIController BuildHierarchy(BuildContext ctx)
        {
            var spec = ctx.Spec;
            float frameW = spec.frameWidth, frameH = spec.frameHeight;

            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x1B, 0x3A, 0x5C, 0xFF);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas_Gameplay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(frameW, frameH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referencePixelsPerUnit = 100f;
            canvasGo.AddComponent<AdaptiveCanvasScaler>();
            var controller = canvasGo.AddComponent<GameplayUIController>();

            // The background sits outside the safe area so it also fills the notch and the margins.
            var background = Stretch(NewRect("Background", canvasGo.transform));
            var safeArea = Stretch(NewRect("SafeArea", canvasGo.transform));
            safeArea.gameObject.AddComponent<SafeArea>();
            var combat = Stretch(NewRect("State_Combat", safeArea));
            var buyGear = Stretch(NewRect("State_BuyGear", safeArea));
            var hud = Stretch(NewRect("HUD", safeArea));
            combat.gameObject.AddComponent<CanvasGroup>();
            buyGear.gameObject.AddComponent<CanvasGroup>();

            var bgNode = spec.nodes.First(n => n.group == "Background");
            var environment = NewImage("Environment", background, LoadSprite(bgNode.sprite), false);
            environment.rectTransform.sizeDelta = new Vector2(bgNode.w, bgNode.h);
            var fitter = environment.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = bgNode.w / bgNode.h;

            foreach (var g in Groups)
            {
                var nodes = spec.nodes.Where(n => n.state == g.state && n.group == g.group).ToList();
                if (nodes.Count == 0)
                {
                    Debug.LogWarning($"[Flower Escape] Layout has no layers for {g.state}/{g.group}.");
                    continue;
                }

                Transform parent = g.group.StartsWith("HUD_") ? hud
                    : g.group == "Banner" ? safeArea
                    : g.state == "Combat" ? combat : buyGear;

                var bounds = Union(nodes);
                if (g.anchor == Anchor.BottomStretch)
                    bounds = new Rect(0f, bounds.y, frameW, bounds.height);

                var groupRt = NewRect(g.objectName, parent);
                PlaceGroup(groupRt, bounds, g.anchor, frameW, frameH);
                if (g.group == "Banner")
                    Stretch(NewImage("Backdrop", groupRt, null, false).rectTransform).GetComponent<Image>().color = Color.black;

                foreach (var node in nodes)
                    CreateNode(node, groupRt, bounds, ctx);
            }

            var so = new SerializedObject(controller);
            so.FindProperty("combatRoot").objectReferenceValue = combat.GetComponent<CanvasGroup>();
            so.FindProperty("buyGearRoot").objectReferenceValue = buyGear.GetComponent<CanvasGroup>();
            var start = ctx.Spec.nodes.FirstOrDefault(n => n.uiName == "Btn_Start");
            if (start != null)
                so.FindProperty("startButton").objectReferenceValue = ctx.Objects[start.id].GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        static void CreateNode(FigmaLayoutNode node, RectTransform groupRt, Rect groupRect, BuildContext ctx)
        {
            bool nested = !string.IsNullOrEmpty(node.parent);
            var parentRt = nested ? ctx.Objects[node.parent] : groupRt;
            var parentRect = nested ? ctx.Rects[node.parent] : groupRect;
            var rect = new Rect(node.x, node.y, node.w, node.h);

            RectTransform rt;
            if (node.kind == "text")
            {
                bool stroked = !string.IsNullOrEmpty(node.strokeColor) && node.strokeWidth > 0f;
                var stroke = stroked ? CreateText(node, parentRt, ctx, "_Stroke") : null;
                var label = CreateText(node, parentRt, ctx, "");
                if (stroke != null)
                {
                    stroke.fontSharedMaterial = StrokeMaterial(ctx, node.strokeWidth / node.fontSize);
                    stroke.color = ParseColor(node.strokeColor);
                    Place(stroke.rectTransform, rect, parentRect, false);
                    var so = new SerializedObject(stroke.gameObject.AddComponent<TextStroke>());
                    so.FindProperty("label").objectReferenceValue = label;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                rt = label.rectTransform;
            }
            else
            {
                var name = string.IsNullOrEmpty(node.uiName) ? node.name : node.uiName;
                var image = NewImage(name, parentRt, LoadSprite(node.sprite), node.button);
                if (node.stretchX)
                    image.type = Image.Type.Sliced;
                if (node.button)
                    image.gameObject.AddComponent<Button>().targetGraphic = image;
                rt = image.rectTransform;
            }

            Place(rt, rect, parentRect, node.stretchX);
            ctx.Objects[node.id] = rt;
            ctx.Rects[node.id] = rect;
        }

        static TextMeshProUGUI CreateText(FigmaLayoutNode node, RectTransform parent, BuildContext ctx, string suffix)
        {
            var go = new GameObject("Txt_" + node.text.Replace("\n", " ") + suffix, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = ctx.Font;
            text.text = node.text;
            text.fontSize = node.fontSize;
            text.color = ParseColor(node.color);
            text.alignment = Alignment(node.alignH, node.alignV);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = Vector4.zero;
            text.raycastTarget = false;
            return text;
        }

        // Material for a stroke layer: the font material with the face dilated by the stroke width.
        // The SDF maps +-padding atlas pixels onto alpha 0..1 and the shader moves the edge by
        // dilate * ratioA / 2 in alpha, i.e. dilate * ratioA * padding atlas pixels, where
        // ratioA = padding / (padding + 1). A stroke of `strokeRatio` x font size spans
        // strokeRatio * samplingPointSize atlas pixels.
        static Material StrokeMaterial(BuildContext ctx, float strokeRatio)
        {
            float ratioA = AtlasPadding / (AtlasPadding + 1f);
            float dilate = Mathf.Round(strokeRatio * SamplingPointSize / (AtlasPadding * ratioA) * 50f) / 50f;
            string key = dilate.ToString("0.00", CultureInfo.InvariantCulture);
            if (ctx.Materials.TryGetValue(key, out var cached))
                return cached;

            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/Lalezar SDF Stroke {key}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(ctx.Font.material);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.CopyPropertiesFromMaterial(ctx.Font.material);
            }

            material.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);

            ctx.Materials[key] = material;
            return material;
        }

        static void Preview(GameplayState state)
        {
            var controller = Object.FindObjectOfType<GameplayUIController>(true);
            if (controller == null)
            {
                Debug.LogWarning("[Flower Escape] No GameplayUIController in the open scene.");
                return;
            }

            Undo.RecordObjects(new Object[] { controller.CombatRoot.gameObject, controller.BuyGearRoot.gameObject, controller.CombatRoot, controller.BuyGearRoot }, "Preview Gameplay State");
            controller.ApplyStateImmediate(state);

            // Play mode starts in the state being previewed.
            var so = new SerializedObject(controller);
            so.FindProperty("initialState").enumValueIndex = (int)state;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        // Renders a copy of the canvas as a world-space canvas sized like a device screen,
        // using the same width/height matching as AdaptiveCanvasScaler.
        static void Capture(GameplayUIController source, GameplayState state, int width, int height, string path)
        {
            var scaler = source.GetComponent<CanvasScaler>();
            var reference = scaler.referenceResolution;
            float pixelsPerUnit = (float)width / height < reference.x / reference.y ? width / reference.x : height / reference.y;

            var clone = Object.Instantiate(source.gameObject);
            var cameraGo = new GameObject("CaptureCamera");
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                Object.DestroyImmediate(clone.GetComponent<AdaptiveCanvasScaler>());
                clone.GetComponent<GameplayUIController>().ApplyStateImmediate(state);

                var camera = cameraGo.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = height / pixelsPerUnit / 2f;
                camera.transform.position = new Vector3(0f, 0f, -100f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;

                var canvas = clone.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                var rt = (RectTransform)clone.transform;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(width / pixelsPerUnit, height / pixelsPerUnit);
                rt.position = Vector3.zero;
                rt.localScale = Vector3.one;

                foreach (var fitter in clone.GetComponentsInChildren<AspectRatioFitter>(true))
                {
                    fitter.enabled = false;
                    fitter.enabled = true;
                }
                Canvas.ForceUpdateCanvases();
                // The SDF edge sharpness depends on the canvas scale, which changed with the render mode.
                foreach (var text in clone.GetComponentsInChildren<TMP_Text>())
                    text.ForceMeshUpdate();

                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(clone);
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
            }
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void PlaceGroup(RectTransform rt, Rect bounds, Anchor anchor, float frameW, float frameH)
        {
            if (anchor == Anchor.BottomStretch)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(0f, bounds.height);
                rt.anchoredPosition = new Vector2(0f, frameH - bounds.yMax);
                return;
            }

            var a = anchor switch
            {
                Anchor.TopLeft => new Vector2(0f, 1f),
                Anchor.Top => new Vector2(0.5f, 1f),
                Anchor.TopRight => new Vector2(1f, 1f),
                _ => new Vector2(0.5f, 0f),
            };
            rt.anchorMin = rt.anchorMax = rt.pivot = a;
            rt.sizeDelta = bounds.size;
            // Pivot position in the frame (Unity y up) minus the anchor position in the frame.
            rt.anchoredPosition = new Vector2(
                bounds.x + a.x * bounds.width - a.x * frameW,
                frameH - bounds.yMax + a.y * bounds.height - a.y * frameH);
        }

        // Positions a layer relative to the centre of its parent; Figma y grows downwards.
        static void Place(RectTransform rt, Rect rect, Rect parent, bool stretchX)
        {
            float y = -(rect.center.y - parent.center.y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (stretchX)
            {
                float left = rect.xMin - parent.xMin;
                float right = rect.xMax - parent.xMax;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(right - left, rect.height);
                rt.anchoredPosition = new Vector2((left + right) / 2f, y);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = rect.size;
                rt.anchoredPosition = new Vector2(rect.center.x - parent.center.x, y);
            }
        }

        static Rect Union(IEnumerable<FigmaLayoutNode> nodes)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (var n in nodes)
            {
                xMin = Mathf.Min(xMin, n.x);
                yMin = Mathf.Min(yMin, n.y);
                xMax = Mathf.Max(xMax, n.x + n.w);
                yMax = Mathf.Max(yMax, n.y + n.h);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, bool raycastTarget)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = raycastTarget;
            return image;
        }

        static Sprite LoadSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{name}.png");
            if (sprite == null)
                Debug.LogError($"[Flower Escape] Missing sprite {SpriteFolder}/{name}.png");
            return sprite;
        }

        static Color ParseColor(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

        static TextAlignmentOptions Alignment(string horizontal, string vertical)
        {
            int h = horizontal == "LEFT" ? 0 : horizontal == "RIGHT" ? 2 : 1;
            int v = vertical == "TOP" ? 0 : vertical == "BOTTOM" ? 2 : 1;
            var table = new[,]
            {
                { TextAlignmentOptions.TopLeft, TextAlignmentOptions.Top, TextAlignmentOptions.TopRight },
                { TextAlignmentOptions.Left, TextAlignmentOptions.Center, TextAlignmentOptions.Right },
                { TextAlignmentOptions.BottomLeft, TextAlignmentOptions.Bottom, TextAlignmentOptions.BottomRight },
            };
            return table[v, h];
        }
    }
}
