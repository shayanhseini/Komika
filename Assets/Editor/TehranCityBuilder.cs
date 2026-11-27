using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TehranCityBuilder
{
    private const string BuildVersionKey = "Komika_TehranCity_Version";
    private const string CurrentBuildVersion = "v16_peugeot_206_front_only";

    static TehranCityBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            EditorApplication.delayCall += TryAutoBuildOnce;
        };
    }

    private static void TryAutoBuildOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
            return;

        bool alreadyBuilt = EditorPrefs.GetString(BuildVersionKey, "") == CurrentBuildVersion
            && File.Exists($"{CityPrefabDir}/Tehran_Alborz_Mountains.prefab")
            && GameObject.Find("Tehran_Horizon_Alborz") != null;

        if (alreadyBuilt)
            return;

        EditorPrefs.SetString(BuildVersionKey, CurrentBuildVersion);
        try
        {
            BuildAllAndVerify();
        }
        catch
        {
            EditorPrefs.DeleteKey(BuildVersionKey);
            throw;
        }
    }
    private const string CityModelDir = "Assets/Models/City";
    private const string CityTexDir = "Assets/Models/City/Textures";
    private const string CityMatDir = "Assets/Models/City/Materials";
    private const string CityPrefabDir = "Assets/Prefabs/City";

    private const string ChunkPrefabPath = "Assets/Prefabs/Game/Chunk.prefab";
    private const string CarPrefabPath = "Assets/Prefabs/Game/Vehicles/Car.prefab";
    private const string Car206ObstaclePrefabPath = "Assets/Prefabs/Game/Vehicles/Car_206_Obstacle.prefab";
    private const string TruckPrefabPath = "Assets/Prefabs/Game/Vehicles/TruckObstacle.prefab";
    private const string JumpObsPrefabPath = "Assets/Prefabs/Game/Obstacles/JumpObstacle.prefab";
    private const string SlideObsPrefabPath = "Assets/Prefabs/Game/Obstacles/SlideObstacle.prefab";
    private const string ChoiceObsPrefabPath = "Assets/Prefabs/Game/Obstacles/JumpOrSlideObstacle.prefab";

    private const string MiladFbxPath = "Assets/Models/Player/Milad_Tower/source/Milad tower.fbx";
    private const string MiladBaseTexPath = "Assets/Models/Player/Milad_Tower/textures/Milad_Base_color.png";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";

    private const float SidewalkSurfaceY = -0.82f;
    private const float RoadSurfaceY = -1.00f;

    [MenuItem("Komika/City/Build Full Tehran City & Verify")]
    public static void BuildAllAndVerify()
    {
        try
        {
            EnsureDir(CityMatDir);
            EnsureDir(CityPrefabDir);

            ConfigureTextureImporters();
            ConfigureModelImporters();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var mats = CreateCityMaterials();
            var cityPrefabs = CreateCityPrefabs(mats);

            UpgradeVehiclePrefabs(mats);
            UpgradeObstaclePrefabs(mats);
            RebuildChunkPrefab(cityPrefabs, mats);

            if (SceneManager.GetActiveScene().path != GameScenePath)
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            ConfigureLightingFogAndCamera();
            AlignSceneChunksForEditPreview();
            PlaceHorizonMiladAndAlborz();
            BuildMenuStreetBackdrop(cityPrefabs);

            string uiMsg = BuildKomikaUI.Main();
            Debug.Log("[TehranCityBuilder] Landscape UI result: " + uiMsg);

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            EditorPrefs.SetString(BuildVersionKey, CurrentBuildVersion);

            CaptureVerificationScreenshots();
            Debug.Log("[TehranCityBuilder] SUCCESS: Full Tehran City Build & Verification Complete.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[TehranCityBuilder] FAILED: " + ex);
            throw;
        }
    }

    private static void EnsureDir(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder)) return;
        string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
        string name = Path.GetFileName(assetFolder);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void ConfigureTextureImporters()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { CityTexDir });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            string file = Path.GetFileNameWithoutExtension(path);
            bool isNormal = file.EndsWith("_Normal", StringComparison.OrdinalIgnoreCase);
            bool isLeaf = file.Contains("Chinar_Leaves", StringComparison.OrdinalIgnoreCase);
            bool isBuildingAlb = (file.StartsWith("Tehran_Building", StringComparison.OrdinalIgnoreCase)
                || file.StartsWith("Tehran_Peugeot206", StringComparison.OrdinalIgnoreCase)) && !isNormal;

            var targetType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            int targetMax = isBuildingAlb ? 1024 : 512;
            var targetWrap = (file.Contains("Asphalt") || file.Contains("Sidewalk") || file.Contains("CityBackdrop"))
                ? TextureWrapMode.Repeat
                : TextureWrapMode.Clamp;

            if (importer.textureType == targetType &&
                importer.sRGBTexture == !isNormal &&
                importer.maxTextureSize == targetMax &&
                importer.alphaIsTransparency == isLeaf &&
                importer.mipMapsPreserveCoverage == isLeaf &&
                importer.textureCompression == TextureImporterCompression.Compressed &&
                importer.crunchedCompression &&
                importer.compressionQuality == 75 &&
                importer.wrapMode == targetWrap)
                continue;

            importer.textureType = targetType;
            importer.sRGBTexture = !isNormal;
            importer.maxTextureSize = targetMax;
            importer.alphaIsTransparency = isLeaf;
            importer.mipMapsPreserveCoverage = isLeaf;
            if (isLeaf) importer.alphaTestReferenceValue = 0.36f;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = 75;
            importer.wrapMode = targetWrap;
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureModelImporters()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { CityModelDir });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            if (Mathf.Approximately(importer.globalScale, 1f) &&
                importer.useFileScale &&
                !importer.importBlendShapes &&
                !importer.importCameras &&
                !importer.importLights &&
                !importer.importAnimation &&
                !importer.isReadable &&
                importer.meshCompression == ModelImporterMeshCompression.Medium &&
                importer.materialImportMode == ModelImporterMaterialImportMode.ImportStandard &&
                importer.materialLocation == ModelImporterMaterialLocation.InPrefab)
                continue;

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
        }
    }

    private class CityMaterials
    {
        public Material Building01, Building02, Building03;
        public Material Dake, BusStop, MetroStop, Bus, Taxi, Peugeot206;
        public Material Asphalt, RoadMarkings, CurbWhite, CurbBlack, Sidewalk;
        public Material Nature, ChinarLeaves, Signage, CityBackdrop, SignSteel;
        public Material SignBakery, SignMetro, SignMetroAccent, SignCafe, SignShop, SignBRT, SignDake;
        public Material SignHighwayBlue, SignHighwayGreen, SignGoldTrim, SignCreamTrim;
        public TMP_FontAsset WorldSignFont;
        public Material WorldSignText;
    }

    private static CityMaterials CreateCityMaterials()
    {
        Shader curvedShader = Shader.Find("Komika/Curved World Lit");
        if (curvedShader == null)
            throw new InvalidOperationException("Shader 'Komika/Curved World Lit' not found!");

        Material MakeTexturedMat(string matName, string albPath, string nrmPath, float smoothness, float metallic, float wrap, float rim, Color shadowTint, bool alphaClip = false, bool doubleSided = false)
        {
            string path = $"{CityMatDir}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(curvedShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = curvedShader;
            }

            Texture2D alb = !string.IsNullOrEmpty(albPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(albPath) : null;
            Texture2D nrm = !string.IsNullOrEmpty(nrmPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath) : null;

            mat.SetTexture("_BaseMap", alb);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            mat.SetColor("_ShadowTint", shadowTint);
            mat.SetFloat("_LightingWrap", wrap);
            mat.SetFloat("_RimStrength", rim);
            mat.SetColor("_EmissionColor", Color.black);
            mat.SetFloat("_ReceiveShadows", 1f);
            mat.SetFloat("_Cull", doubleSided ? 0f : 2f);
            if (alphaClip)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.36f);
                mat.EnableKeyword("_ALPHACLIP");
            }
            else
            {
                mat.SetFloat("_AlphaClip", 0f);
                mat.DisableKeyword("_ALPHACLIP");
            }

            if (nrm != null)
            {
                mat.SetTexture("_BumpMap", nrm);
                mat.SetFloat("_BumpScale", 0.85f);
                mat.SetFloat("_EnableNormalMap", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.SetFloat("_EnableNormalMap", 0f);
                mat.DisableKeyword("_NORMALMAP");
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        Material MakeAssetMat(string matName, string assetKey, float smoothness, float metallic, float wrap, float rim)
        {
            return MakeTexturedMat(
                matName,
                $"{CityTexDir}/{assetKey}_Albedo.png",
                $"{CityTexDir}/{assetKey}_Normal.png",
                smoothness,
                metallic,
                wrap,
                rim,
                new Color(0.45f, 0.52f, 0.64f, 1f));
        }

        Material MakeSolidMat(string matName, Color col, float smoothness, float wrap, float rim, Color emission, string texName = null)
        {
            Material mat = MakeTexturedMat(
                matName,
                !string.IsNullOrEmpty(texName) ? $"{CityTexDir}/{texName}" : null,
                null,
                smoothness,
                0f,
                wrap,
                rim,
                new Color(0.44f, 0.50f, 0.60f, 1f));
            mat.SetColor("_BaseColor", col);
            mat.SetColor("_EmissionColor", emission);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        TMP_FontAsset worldFont = BuildKomikaUI.EnsureIranSansFont();
        string worldSignMatPath = "Assets/Font/IRANSans/IRANSans_Curved3D_Mat.mat";
        Material worldSignMat = AssetDatabase.LoadAssetAtPath<Material>(worldSignMatPath);
        Shader curvedTmpShader = Shader.Find("Komika/Curved TMP SDF") ?? worldFont.material.shader;
        if (worldSignMat == null)
        {
            worldSignMat = new Material(worldFont.material);
            AssetDatabase.CreateAsset(worldSignMat, worldSignMatPath);
        }
        if (worldFont != null && worldFont.material != null)
            worldSignMat.CopyPropertiesFromMaterial(worldFont.material);
        worldSignMat.shader = curvedTmpShader;
        worldSignMat.SetFloat("_Sharpness", 0.22f);
        worldSignMat.SetFloat("_CullMode", 2f);
        worldSignMat.SetColor("_FaceColor", Color.white);
        worldSignMat.enableInstancing = true;
        EditorUtility.SetDirty(worldSignMat);

        Material chinarLeavesMat = MakeTexturedMat("Mat_Tehran_ChinarLeaves", $"{CityTexDir}/Tehran_Chinar_Leaves.png", null, 0.08f, 0.0f, 0.28f, 0.14f, new Color(0.20f, 0.30f, 0.22f, 1f), alphaClip: true, doubleSided: true);
        chinarLeavesMat.SetColor("_BaseColor", new Color(0.80f, 0.90f, 0.74f, 1f));
        chinarLeavesMat.enableInstancing = true;
        EditorUtility.SetDirty(chinarLeavesMat);

        return new CityMaterials
        {
            Building01   = MakeAssetMat("Mat_Tehran_Building01", "Tehran_Building01", 0.24f, 0.05f, 0.30f, 0.12f),
            Building02   = MakeAssetMat("Mat_Tehran_Building02", "Tehran_Building02", 0.36f, 0.12f, 0.28f, 0.14f),
            Building03   = MakeAssetMat("Mat_Tehran_Building03", "Tehran_Building03", 0.26f, 0.05f, 0.30f, 0.12f),
            Dake         = MakeAssetMat("Mat_Tehran_Dake",       "Tehran_Dake",       0.32f, 0.08f, 0.34f, 0.16f),
            BusStop      = MakeAssetMat("Mat_Tehran_BusStop",    "Tehran_BusStop",    0.45f, 0.20f, 0.34f, 0.18f),
            MetroStop    = MakeAssetMat("Mat_Tehran_MetroStop",  "Tehran_MetroStop",  0.34f, 0.10f, 0.32f, 0.15f),
            Bus          = MakeAssetMat("Mat_Tehran_Bus",        "Tehran_Bus",        0.55f, 0.18f, 0.44f, 0.25f),
            Taxi         = MakeAssetMat("Mat_Tehran_Taxi",       "Tehran_Taxi",       0.58f, 0.22f, 0.42f, 0.24f),

            Asphalt      = MakeSolidMat("Mat_Tehran_Asphalt",      Color.white,                             0.20f, 0.22f, 0.03f, Color.black, "Tehran_Asphalt_Albedo.png"),
            RoadMarkings = MakeSolidMat("Mat_Tehran_RoadMarkings", new Color(0.97f, 0.97f, 0.95f, 1f),      0.20f, 0.26f, 0.04f, new Color(0.05f, 0.05f, 0.05f, 1f)),
            CurbWhite    = MakeSolidMat("Mat_Tehran_CurbWhite",    new Color(0.94f, 0.92f, 0.88f, 1f),      0.18f, 0.28f, 0.08f, Color.black),
            CurbBlack    = MakeSolidMat("Mat_Tehran_CurbBlack",    new Color(0.22f, 0.24f, 0.28f, 1f),      0.22f, 0.22f, 0.08f, Color.black),
            Sidewalk     = MakeSolidMat("Mat_Tehran_Sidewalk",     new Color(1.00f, 0.98f, 0.95f, 1f),      0.20f, 0.26f, 0.06f, Color.black, "Tehran_Sidewalk_Albedo.png"),

            Nature           = MakeTexturedMat("Mat_Tehran_Nature",       $"{CityTexDir}/Tehran_Nature_Atlas.png",       null, 0.14f, 0.0f,  0.38f, 0.18f, new Color(0.42f, 0.50f, 0.48f, 1f)),
            ChinarLeaves     = chinarLeavesMat,
            Signage          = MakeTexturedMat("Mat_Tehran_Signage",      $"{CityTexDir}/Tehran_Signage_Atlas.png",      null, 0.35f, 0.12f, 0.38f, 0.16f, new Color(0.46f, 0.52f, 0.64f, 1f)),
            CityBackdrop     = MakeTexturedMat("Mat_Tehran_CityBackdrop", $"{CityTexDir}/Tehran_CityBackdrop_Albedo.png",null, 0.12f, 0.02f, 0.36f, 0.08f, new Color(0.54f, 0.60f, 0.70f, 1f)),
            SignSteel        = MakeSolidMat("Mat_Tehran_SignSteel",       new Color(0.24f, 0.27f, 0.31f, 1f),            0.40f, 0.28f, 0.12f, Color.black),

            SignBakery       = MakeSolidMat("Mat_Sign_Bakery",       new Color(0.23f, 0.11f, 0.07f, 1f), 0.32f, 0.28f, 0.12f, new Color(0.05f, 0.02f, 0.01f, 1f)),
            SignMetro        = MakeSolidMat("Mat_Sign_Metro",        new Color(0.05f, 0.17f, 0.38f, 1f), 0.38f, 0.30f, 0.14f, new Color(0.02f, 0.06f, 0.14f, 1f)),
            SignMetroAccent  = MakeSolidMat("Mat_Sign_MetroAccent",  new Color(0.90f, 0.25f, 0.12f, 1f), 0.35f, 0.32f, 0.16f, new Color(0.18f, 0.04f, 0.02f, 1f)),
            SignCafe         = MakeSolidMat("Mat_Sign_Cafe",         new Color(0.06f, 0.22f, 0.26f, 1f), 0.34f, 0.30f, 0.14f, new Color(0.01f, 0.05f, 0.06f, 1f)),
            SignShop         = MakeSolidMat("Mat_Sign_Shop",         new Color(0.10f, 0.16f, 0.23f, 1f), 0.36f, 0.28f, 0.12f, new Color(0.02f, 0.03f, 0.06f, 1f)),
            SignBRT          = MakeSolidMat("Mat_Sign_BRT",          new Color(0.05f, 0.38f, 0.62f, 1f), 0.38f, 0.32f, 0.16f, new Color(0.02f, 0.10f, 0.16f, 1f)),
            SignDake         = MakeSolidMat("Mat_Sign_Dake",         new Color(0.08f, 0.31f, 0.22f, 1f), 0.34f, 0.30f, 0.14f, new Color(0.02f, 0.07f, 0.04f, 1f)),
            SignHighwayBlue  = MakeSolidMat("Mat_Sign_HighwayBlue",  new Color(0.06f, 0.35f, 0.66f, 1f), 0.34f, 0.32f, 0.14f, new Color(0.02f, 0.08f, 0.16f, 1f)),
            SignHighwayGreen = MakeSolidMat("Mat_Sign_HighwayGreen", new Color(0.07f, 0.42f, 0.25f, 1f), 0.34f, 0.32f, 0.14f, new Color(0.02f, 0.10f, 0.06f, 1f)),
            SignGoldTrim     = MakeSolidMat("Mat_Sign_GoldTrim",     new Color(0.92f, 0.76f, 0.38f, 1f), 0.45f, 0.34f, 0.18f, new Color(0.14f, 0.10f, 0.03f, 1f)),
            SignCreamTrim    = MakeSolidMat("Mat_Sign_CreamTrim",    new Color(0.96f, 0.91f, 0.82f, 1f), 0.30f, 0.32f, 0.14f, new Color(0.08f, 0.07f, 0.05f, 1f)),
            Peugeot206       = MakeAssetMat("Mat_Tehran_Peugeot206",   "Tehran_Peugeot206", 0.62f, 0.28f, 0.40f, 0.25f),
            WorldSignFont    = worldFont,
            WorldSignText    = worldSignMat,
        };
    }

    private class CityPrefabs
    {
        public GameObject Building01, Building02, Building03;
        public GameObject Dake, BusStop, MetroStop, Peugeot206;
        public GameObject ChinarTree, StreetLamp, HighwayGantry, StreetSignPole, CityBackdropBlock;
        public GameObject Road20m, Crosswalk20m;
    }

    private static void AttachSignboard(
        GameObject root,
        CityMaterials mats,
        string name,
        Vector3 localPos,
        Vector3 localEuler,
        Vector2 boardSize,
        Material boardMat,
        Material trimMat,
        string persianText,
        Color textColor,
        float fontSize,
        bool doubleSided = false,
        float strutDepth = 0f)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(root.transform, false);
        holder.transform.localPosition = localPos;
        holder.transform.localRotation = Quaternion.Euler(localEuler);

        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "Board";
        UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>());
        plate.transform.SetParent(holder.transform, false);
        plate.transform.localScale = new Vector3(boardSize.x, boardSize.y, 0.07f);
        var pr = plate.GetComponent<Renderer>();
        pr.sharedMaterial = boardMat;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = true;

        GameObject topTrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topTrim.name = "TopTrim";
        UnityEngine.Object.DestroyImmediate(topTrim.GetComponent<Collider>());
        topTrim.transform.SetParent(holder.transform, false);
        topTrim.transform.localPosition = new Vector3(0f, boardSize.y * 0.5f, 0f);
        topTrim.transform.localScale = new Vector3(boardSize.x + 0.04f, 0.05f, 0.085f);
        var tr = topTrim.GetComponent<Renderer>();
        tr.sharedMaterial = trimMat;
        tr.shadowCastingMode = ShadowCastingMode.Off;
        tr.receiveShadows = true;

        if (strutDepth > 0.05f)
        {
            GameObject strut = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strut.name = "Bracket";
            UnityEngine.Object.DestroyImmediate(strut.GetComponent<Collider>());
            strut.transform.SetParent(holder.transform, false);
            strut.transform.localPosition = new Vector3(boardSize.x * 0.5f + strutDepth * 0.5f, 0f, 0f);
            strut.transform.localScale = new Vector3(strutDepth, 0.07f, 0.06f);
            var sr = strut.GetComponent<Renderer>();
            sr.sharedMaterial = mats.SignSteel;
            sr.shadowCastingMode = ShadowCastingMode.Off;
        }

        void AddTextFace(string faceName, float localZ, float yaw)
        {
            GameObject tgo = new GameObject(faceName);
            tgo.transform.SetParent(holder.transform, false);
            tgo.transform.localPosition = new Vector3(0f, 0f, localZ);
            tgo.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var tmp = tgo.AddComponent<TextMeshPro>();
            tmp.font = mats.WorldSignFont;
            tmp.fontSharedMaterial = mats.WorldSignText;
            tmp.fontSize = fontSize;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.richText = false;
            tmp.rectTransform.sizeDelta = new Vector2(boardSize.x * 0.94f, boardSize.y * 0.88f);
            var mr = tgo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            var rtl = tgo.AddComponent<RTLTextMeshPro>();
            rtl.SetText(persianText, persianDigits: true);
        }

        AddTextFace("RTL_Front", -0.044f, 0f);
        if (doubleSided)
            AddTextFace("RTL_Back", +0.044f, 180f);
    }

    private static CityPrefabs CreateCityPrefabs(CityMaterials mats)
    {
        GameObject BuildSingleMatPrefab(string assetName, Material mat, bool castShadows = true, Action<GameObject> onCustomize = null)
        {
            string fbxPath = $"{CityModelDir}/{assetName}.fbx";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) throw new InvalidOperationException($"Missing FBX: {fbxPath}");

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                int count = Mathf.Max(1, r.sharedMaterials.Length);
                var arr = new Material[count];
                for (int i = 0; i < count; i++) arr[i] = mat;
                r.sharedMaterials = arr;
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
            }

            onCustomize?.Invoke(inst);

            string outPrefabPath = $"{CityPrefabDir}/{assetName}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(inst, outPrefabPath);
            UnityEngine.Object.DestroyImmediate(inst);
            return saved;
        }

        GameObject BuildChinarTreePrefab()
        {
            string fbxPath = $"{CityModelDir}/Tehran_Chinar_Tree.fbx";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) throw new InvalidOperationException($"Missing FBX: {fbxPath}");

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                Material[] orig = r.sharedMaterials;
                Material[] mapped = new Material[Mathf.Max(1, orig.Length)];
                for (int i = 0; i < mapped.Length; i++)
                {
                    string n = (i < orig.Length && orig[i] != null) ? orig[i].name : "";
                    mapped[i] = (n.Contains("Leave", StringComparison.OrdinalIgnoreCase) || n.Contains("Foliage", StringComparison.OrdinalIgnoreCase))
                        ? mats.ChinarLeaves
                        : mats.Nature;
                }
                r.sharedMaterials = mapped;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            string outPrefabPath = $"{CityPrefabDir}/Tehran_Chinar_Tree.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(inst, outPrefabPath);
            UnityEngine.Object.DestroyImmediate(inst);
            return saved;
        }

        GameObject BuildRoadPrefab(string assetName)
        {
            string fbxPath = $"{CityModelDir}/{assetName}.fbx";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) throw new InvalidOperationException($"Missing Road FBX: {fbxPath}");

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                Material[] orig = r.sharedMaterials;
                Material[] mapped = new Material[orig.Length];
                for (int i = 0; i < orig.Length; i++)
                {
                    string n = orig[i] != null ? orig[i].name : "";
                    if (n.Contains("Asphalt")) mapped[i] = mats.Asphalt;
                    else if (n.Contains("RoadMarkings")) mapped[i] = mats.RoadMarkings;
                    else if (n.Contains("CurbWhite")) mapped[i] = mats.CurbWhite;
                    else if (n.Contains("CurbBlack")) mapped[i] = mats.CurbBlack;
                    else mapped[i] = mats.Sidewalk;
                }
                r.sharedMaterials = mapped;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
            }

            string outPrefabPath = $"{CityPrefabDir}/{assetName}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(inst, outPrefabPath);
            UnityEngine.Object.DestroyImmediate(inst);
            return saved;
        }

        return new CityPrefabs
        {
            Building01 = BuildSingleMatPrefab("Tehran_Building01", mats.Building01, true, go =>
            {
                AttachSignboard(go, mats, "Sign_CafeFascia", new Vector3(0f, 4.18f, 3.68f), new Vector3(0f, 180f, 0f),
                    new Vector2(4.9f, 0.86f), mats.SignCafe, mats.SignGoldTrim, "کافه و کتابفروشی تهران", new Color(0.98f, 0.94f, 0.84f), 4.0f);
                AttachSignboard(go, mats, "Sign_CafeBlade", new Vector3(0f, 3.10f, 4.75f), new Vector3(0f, 90f, 0f),
                    new Vector2(1.70f, 0.68f), mats.SignCafe, mats.SignGoldTrim, "کافه ولیعصر", new Color(1f, 0.86f, 0.54f), 3.1f, doubleSided: true, strutDepth: 0.45f);
            }),
            Building02 = BuildSingleMatPrefab("Tehran_Building02", mats.Building02, true, go =>
            {
                AttachSignboard(go, mats, "Sign_ShopFascia", new Vector3(0f, 4.22f, 6.12f), new Vector3(0f, 180f, 0f),
                    new Vector2(5.1f, 0.88f), mats.SignShop, mats.SignGoldTrim, "فروشگاه زنجیره‌ای شهر", new Color(0.98f, 0.86f, 0.45f), 4.0f);
                AttachSignboard(go, mats, "Sign_ShopBlade", new Vector3(0f, 3.10f, 7.12f), new Vector3(0f, 90f, 0f),
                    new Vector2(1.70f, 0.68f), mats.SignShop, mats.SignGoldTrim, "سوپرمارکت", new Color(0.95f, 0.98f, 1f), 3.1f, doubleSided: true, strutDepth: 0.45f);
            }),
            Building03 = BuildSingleMatPrefab("Tehran_Building03", mats.Building03, true, go =>
            {
                AttachSignboard(go, mats, "Sign_BakeryFascia", new Vector3(0f, 4.44f, 4.06f), new Vector3(0f, 180f, 0f),
                    new Vector2(4.7f, 0.92f), mats.SignBakery, mats.SignCreamTrim, "شیرینی‌سرای ولیعصر", new Color(1f, 0.92f, 0.72f), 4.4f);
                AttachSignboard(go, mats, "Sign_BakeryBlade", new Vector3(0f, 3.24f, 5.42f), new Vector3(0f, 90f, 0f),
                    new Vector2(1.85f, 0.74f), mats.SignBakery, mats.SignGoldTrim, "شیرینی‌فروشی", new Color(1f, 0.95f, 0.82f), 3.3f, doubleSided: true, strutDepth: 0.50f);
            }),
            Dake = BuildSingleMatPrefab("Tehran_Dake", mats.Dake, true, go =>
            {
                AttachSignboard(go, mats, "Sign_DakeFascia", new Vector3(0f, 2.94f, 1.15f), new Vector3(0f, 180f, 0f),
                    new Vector2(2.10f, 0.50f), mats.SignDake, mats.SignGoldTrim, "دکه مطبوعات", new Color(1f, 0.96f, 0.80f), 2.7f);
                AttachSignboard(go, mats, "Sign_DakeBlade", new Vector3(0f, 2.15f, 1.62f), new Vector3(0f, 90f, 0f),
                    new Vector2(1.18f, 0.46f), mats.SignDake, mats.SignGoldTrim, "دکه مطبوعات", new Color(1f, 0.88f, 0.42f), 2.3f, doubleSided: true, strutDepth: 0.25f);
            }),
            BusStop = BuildSingleMatPrefab("Tehran_BusStop", mats.BusStop, true, go =>
            {
                AttachSignboard(go, mats, "Sign_BusFascia", new Vector3(0f, 2.86f, 1.05f), new Vector3(0f, 180f, 0f),
                    new Vector2(3.15f, 0.56f), mats.SignBRT, mats.SignCreamTrim, "ایستگاه بی‌آرتی ولیعصر", new Color(0.97f, 0.99f, 1f), 2.8f);
                AttachSignboard(go, mats, "Sign_BusBlade", new Vector3(0f, 2.18f, 1.48f), new Vector3(0f, 90f, 0f),
                    new Vector2(1.38f, 0.52f), mats.SignBRT, mats.SignCreamTrim, "ایستگاه بی‌آرتی", new Color(1f, 1f, 1f), 2.4f, doubleSided: true, strutDepth: 0.28f);
            }),
            MetroStop = BuildSingleMatPrefab("Tehran_MetroStop", mats.MetroStop, true, go =>
            {
                AttachSignboard(go, mats, "Sign_MetroFascia", new Vector3(0f, 3.68f, 3.92f), new Vector3(0f, 180f, 0f),
                    new Vector2(4.45f, 0.96f), mats.SignMetro, mats.SignMetroAccent, "ایستگاه مترو تئاتر شهر", new Color(1f, 0.98f, 0.94f), 4.0f);
                AttachSignboard(go, mats, "Sign_MetroBlade", new Vector3(0f, 2.85f, 5.05f), new Vector3(0f, 90f, 0f),
                    new Vector2(2.25f, 0.92f), mats.SignMetro, mats.SignMetroAccent, "مترو ولیعصر\nخط ۴", new Color(1f, 0.96f, 0.90f), 3.6f, doubleSided: true, strutDepth: 0.45f);
            }),
            ChinarTree        = BuildChinarTreePrefab(),
            StreetLamp        = BuildSingleMatPrefab("Tehran_StreetLamp",      mats.Nature, castShadows: false),
            HighwayGantry     = BuildSingleMatPrefab("Tehran_Highway_Gantry",  mats.Signage, true, go =>
            {
                AttachSignboard(go, mats, "Sign_GantryBlue", new Vector3(-2.50f, 6.05f, -0.28f), Vector3.zero,
                    new Vector2(4.35f, 1.56f), mats.SignHighwayBlue, mats.SignCreamTrim, "میدان ولیعصر\nپارک ساعی • ونک", Color.white, 4.1f);
                AttachSignboard(go, mats, "Sign_GantryGreen", new Vector3(2.50f, 6.05f, -0.28f), Vector3.zero,
                    new Vector2(4.35f, 1.56f), mats.SignHighwayGreen, mats.SignCreamTrim, "بزرگراه مدرس\nمسیر برج میلاد", Color.white, 4.1f);
            }),
            StreetSignPole    = BuildSingleMatPrefab("Tehran_StreetSign_Pole", mats.Signage, castShadows: false, go =>
            {
                AttachSignboard(go, mats, "Sign_StreetName", new Vector3(0f, 2.50f, -0.09f), Vector3.zero,
                    new Vector2(1.32f, 0.66f), mats.SignHighwayBlue, mats.SignCreamTrim, "خیابان ولیعصر\nمنطقه ۶", Color.white, 2.1f, doubleSided: true);
            }),
            Peugeot206        = BuildSingleMatPrefab("Tehran_Peugeot206", mats.Peugeot206, castShadows: false),
            CityBackdropBlock = BuildSingleMatPrefab("Tehran_CityBackdrop_Block", mats.CityBackdrop, castShadows: false),
            Road20m           = BuildRoadPrefab("Tehran_Road_20m"),
            Crosswalk20m      = BuildRoadPrefab("Tehran_Crosswalk_20m"),
        };
    }

    private static Bounds ComputeRendererBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static void UpgradeVehiclePrefabs(CityMaterials mats)
    {
        void ReplaceVehicleVisual(string prefabPath, string fbxName, Material mat, float yawDeg)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{CityModelDir}/{fbxName}.fbx");
            if (fbx == null) throw new InvalidOperationException($"Missing Vehicle FBX: {fbxName}");

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            GameObject vis = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            vis.name = "Visual";
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            vis.transform.localScale = Vector3.one;

            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < arr.Length; k++) arr[k] = mat;
                r.sharedMaterials = arr;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(Car206ObstaclePrefabPath) == null)
        {
            GameObject stub = new GameObject("Car_206_Obstacle");
            var col = stub.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.96f, 1.44f, 3.68f);
            col.center = new Vector3(0f, 0.72f, 0f);
            stub.AddComponent<Obstacle>();
            PrefabUtility.SaveAsPrefabAsset(stub, Car206ObstaclePrefabPath);
            UnityEngine.Object.DestroyImmediate(stub);
        }

        ReplaceVehicleVisual(CarPrefabPath, "Tehran_Taxi", mats.Taxi, 0f);
        ReplaceVehicleVisual(Car206ObstaclePrefabPath, "Tehran_Peugeot206", mats.Peugeot206, 180f);
        ReplaceVehicleVisual(TruckPrefabPath, "Tehran_Bus", mats.Bus, 180f);
    }

    private static void UpgradeObstaclePrefabs(CityMaterials mats)
    {
        void ApplyBarrierVisual(string prefabPath, string fbxName, bool keepSteelSidePosts)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{CityModelDir}/{fbxName}.fbx");
            if (fbx == null) throw new InvalidOperationException($"Missing Barrier FBX: {fbxName}");

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            MeshRenderer rootMr = root.GetComponent<MeshRenderer>();
            if (rootMr != null) rootMr.enabled = false;

            Transform oldVis = root.transform.Find("Visual");
            if (oldVis != null) UnityEngine.Object.DestroyImmediate(oldVis.gameObject);

            if (keepSteelSidePosts)
            {
                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.gameObject == root) continue;
                    mr.sharedMaterial = mats.SignSteel;
                }
            }

            GameObject vis = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            vis.name = "Visual";
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;
            vis.transform.localScale = Vector3.one;

            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < arr.Length; k++) arr[k] = mats.Signage;
                r.sharedMaterials = arr;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        ApplyBarrierVisual(JumpObsPrefabPath, "Tehran_Barrier_Jump", false);
        ApplyBarrierVisual(SlideObsPrefabPath, "Tehran_Barrier_Slide", true);
        ApplyBarrierVisual(ChoiceObsPrefabPath, "Tehran_Barrier_Choice", false);
    }

    private static void RebuildChunkPrefab(CityPrefabs prefabs, CityMaterials mats)
    {
        GameObject chunkRoot = PrefabUtility.LoadPrefabContents(ChunkPrefabPath);
        Transform env = chunkRoot.transform.Find("Enviroment/Env");
        if (env == null) throw new InvalidOperationException("Enviroment/Env missing in Chunk.prefab");

        DestroyChildIfExists(env, "Asphalt Road");
        DestroyChildIfExists(env, "Lane Marker");

        Transform roadContainer = env.Find("Curved Road Segments");
        if (roadContainer == null)
        {
            var go = new GameObject("Curved Road Segments");
            roadContainer = go.transform;
            roadContainer.SetParent(env, false);
        }
        else
        {
            ClearChildren(roadContainer);
        }

        for (int i = 0; i < 10; i++)
        {
            float zCenter = -90f + i * 20f;
            bool isCrosswalk = Mathf.Approximately(zCenter, 70f);
            GameObject src = isCrosswalk ? prefabs.Crosswalk20m : prefabs.Road20m;
            GameObject seg = (GameObject)PrefabUtility.InstantiatePrefab(src, roadContainer);
            seg.name = isCrosswalk ? $"Crosswalk_Segment_{i}" : $"Road_Segment_{i}";
            seg.transform.localPosition = new Vector3(0f, 0f, zCenter);
            seg.transform.localRotation = Quaternion.identity;
            seg.transform.localScale = Vector3.one;
        }

        var wc = chunkRoot.GetComponent<WorldChunk>();
        if (wc != null)
        {
            var so = new SerializedObject(wc);
            SerializedProperty chanceProp = so.FindProperty("miladSpawnChance");
            if (chanceProp != null) chanceProp.floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Transform miladL = env.Find("Milad Tower Left");
        if (miladL != null) miladL.gameObject.SetActive(false);
        Transform miladR = env.Find("Milad Tower Right");
        if (miladR != null) miladR.gameObject.SetActive(false);

        for (int v = 1; v <= 4; v++)
        {
            Transform variant = env.Find($"Building {v}");
            if (variant == null) continue;
            variant.gameObject.SetActive(v == 1);

            for (int i = variant.childCount - 1; i >= 0; i--)
            {
                Transform child = variant.GetChild(i);
                if (child.name == "Gameplay Layout") continue;
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            Transform gameplay = variant.Find("Gameplay Layout");
            if (gameplay != null)
            {
                GameObject car206ObsAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Car206ObstaclePrefabPath);
                if (car206ObsAsset != null)
                {
                    var singleTrucks = new List<Transform>();
                    foreach (Transform t in gameplay.GetComponentsInChildren<Transform>(true))
                    {
                        if (t != null && t.name.StartsWith("Single Truck", StringComparison.OrdinalIgnoreCase))
                            singleTrucks.Add(t);
                    }
                    for (int st = 0; st < singleTrucks.Count; st++)
                    {
                        if (st % 2 != 0) continue;
                        Transform oldTr = singleTrucks[st];
                        Transform pTr = oldTr.parent;
                        int sib = oldTr.GetSiblingIndex();
                        Vector3 lp = oldTr.localPosition;
                        string newName = oldTr.name.Replace("Single Truck", "Single 206");
                        UnityEngine.Object.DestroyImmediate(oldTr.gameObject);
                        GameObject inst206 = (GameObject)PrefabUtility.InstantiatePrefab(car206ObsAsset, pTr);
                        inst206.name = newName;
                        inst206.transform.SetSiblingIndex(sib);
                        inst206.transform.localPosition = lp;
                        inst206.transform.localRotation = Quaternion.identity;
                        inst206.transform.localScale = Vector3.one;
                    }
                }

                Transform crossroad = gameplay.Find("Crossroad Traffic");
                if (crossroad != null)
                {
                    crossroad.localPosition = new Vector3(0f, 0f, 70f);
                    Transform horiz = crossroad.Find("Horizontal Road");
                    if (horiz != null) horiz.gameObject.SetActive(false);
                    Transform leftSp = crossroad.Find("Left Spawn Point");
                    Transform rightSp = crossroad.Find("Right Spawn Point");
                    if (leftSp != null) leftSp.localPosition = new Vector3(-16f, RoadSurfaceY, 0f);
                    if (rightSp != null) rightSp.localPosition = new Vector3(16f, RoadSurfaceY, 0f);
                }
            }

            PopulateBuildingVariant(variant, v, prefabs);
        }

        PrefabUtility.SaveAsPrefabAsset(chunkRoot, ChunkPrefabPath);
        PrefabUtility.UnloadPrefabContents(chunkRoot);
    }

    private static void PopulateBuildingVariant(Transform parent, int variantIndex, CityPrefabs p)
    {
        int[] leftSlots;
        int[] rightSlots;

        switch (variantIndex)
        {
            case 1:
                leftSlots  = new[] { 1, 2, 5, 3, 1, 4, 2, 6, 3, 1, 1 };
                rightSlots = new[] { 3, 1, 6, 2, 3, 1, 5, 2, 1, 3, 2 };
                break;
            case 2:
                leftSlots  = new[] { 2, 3, 6, 1, 2, 3, 5, 1, 4, 3, 2 };
                rightSlots = new[] { 1, 4, 3, 2, 5, 3, 1, 2, 6, 1, 3 };
                break;
            case 3:
                leftSlots  = new[] { 3, 1, 4, 2, 3, 6, 1, 2, 5, 3, 1 };
                rightSlots = new[] { 2, 3, 1, 6, 1, 4, 3, 1, 2, 3, 1 };
                break;
            default:
                leftSlots  = new[] { 1, 3, 2, 5, 1, 3, 6, 2, 1, 3, 2 };
                rightSlots = new[] { 3, 2, 4, 1, 3, 5, 2, 3, 1, 3, 1 };
                break;
        }

        float[] zCenters = { -91f, -75f, -59f, -43f, -26f, -10f, 7f, 23f, 40f, 54f, 89f };

        void PlaceSlot(int kind, int idx, float sideSign)
        {
            GameObject prefab;
            float setbackX;
            float yaw = sideSign < 0f ? 90f : -90f;

            switch (kind)
            {
                case 1: prefab = p.Building01; setbackX = 11.6f; break;
                case 2: prefab = p.Building02; setbackX = 14.1f; break;
                case 3: prefab = p.Building03; setbackX = 12.1f; break;
                case 4: prefab = p.MetroStop;  setbackX = 11.7f; break;
                case 5: prefab = p.Dake;       setbackX = 7.05f; break;
                default: prefab = p.BusStop;   setbackX = 7.05f; break;
            }

            if (kind <= 3 && ((idx + variantIndex) % 2 == 1))
                setbackX += 0.55f;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = $"{prefab.name}_{(sideSign < 0 ? "L" : "R")}_{idx}";
            inst.transform.localPosition = new Vector3(sideSign * setbackX, SidewalkSurfaceY, zCenters[idx]);
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            inst.transform.localScale = Vector3.one;

            // Parked Peugeot 206 facing straight/3-4 frontward (-Z) toward approaching runner (back never visible)
            if (p.Peugeot206 != null && kind <= 3 && (idx == 1 || idx == 8))
            {
                GameObject car206 = (GameObject)PrefabUtility.InstantiatePrefab(p.Peugeot206, parent);
                car206.name = $"Peugeot206_Parked_{(sideSign < 0 ? "L" : "R")}_{idx}";
                car206.transform.localPosition = new Vector3(sideSign * 6.96f, SidewalkSurfaceY, zCenters[idx]);
                car206.transform.localRotation = Quaternion.Euler(0f, sideSign < 0f ? 166f : -166f, 0f);
                car206.transform.localScale = Vector3.one;
            }
        }

        for (int i = 0; i < zCenters.Length; i++)
        {
            PlaceSlot(leftSlots[i], i, -1f);
            PlaceSlot(rightSlots[i], i, +1f);
        }

        // Authentic Tehran Chinar Plane Trees ("چنار ولیعصر") in alley gaps cleanly off buildings & intersection
        float[] treeZs = { -84f, -51f, -18f, 15f, 47f, 82f };
        for (int i = 0; i < treeZs.Length; i++)
        {
            for (float side = -1f; side <= 1f; side += 2f)
            {
                GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(p.ChinarTree, parent);
                tree.name = $"Chinar_Tree_{(side < 0 ? "L" : "R")}_{i}";
                tree.transform.localPosition = new Vector3(side * 6.10f, SidewalkSurfaceY, treeZs[i]);
                tree.transform.localRotation = Quaternion.Euler(0f, (i * 55f + (side < 0 ? 15f : 110f)) % 360f, 0f);
                float treeScale = 0.92f + ((i + variantIndex) % 3) * 0.08f;
                tree.transform.localScale = Vector3.one * treeScale;
            }
        }

        // Tall Curved Tehran Boulevard Streetlights
        float[] lampZs = { -67f, -34f, -2f, 31f, 96f };
        for (int i = 0; i < lampZs.Length; i++)
        {
            for (float side = -1f; side <= 1f; side += 2f)
            {
                GameObject lamp = (GameObject)PrefabUtility.InstantiatePrefab(p.StreetLamp, parent);
                lamp.name = $"StreetLamp_{(side < 0 ? "L" : "R")}_{i}";
                lamp.transform.localPosition = new Vector3(side * 5.82f, SidewalkSurfaceY, lampZs[i]);
                lamp.transform.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f);
            }
        }

        // Overhead Persian Highway Gantry ("میدان ولیعصر | بزرگراه مدرس - برج میلاد") arching high over road
        GameObject gantry = (GameObject)PrefabUtility.InstantiatePrefab(p.HighwayGantry, parent);
        gantry.name = "Tehran_Highway_Gantry";
        gantry.transform.localPosition = new Vector3(0f, RoadSurfaceY, 32f);
        gantry.transform.localRotation = Quaternion.identity;

        // Municipal Street Name Signs ("خیابان ولیعصر • منطقه ۶ شهرداری تهران")
        GameObject signR = (GameObject)PrefabUtility.InstantiatePrefab(p.StreetSignPole, parent);
        signR.name = "StreetSign_Crosswalk_R";
        signR.transform.localPosition = new Vector3(6.15f, SidewalkSurfaceY, 60.5f);
        signR.transform.localRotation = Quaternion.Euler(0f, -24f, 0f);

        GameObject signL = (GameObject)PrefabUtility.InstantiatePrefab(p.StreetSignPole, parent);
        signL.name = "StreetSign_Mid_L";
        signL.transform.localPosition = new Vector3(-6.15f, SidewalkSurfaceY, -28f);
        signL.transform.localRotation = Quaternion.Euler(0f, 24f, 0f);

        // Secondary urban skyline depth blocks behind main avenue (eliminates thin floating corridor look)
        float[] bgZs = { -72f, -22f, 28f, 84f };
        for (int i = 0; i < bgZs.Length; i++)
        {
            for (float side = -1f; side <= 1f; side += 2f)
            {
                GameObject bg = (GameObject)PrefabUtility.InstantiatePrefab(p.CityBackdropBlock, parent);
                bg.name = $"CityBackdrop_{(side < 0 ? "L" : "R")}_{i}";
                bg.transform.localPosition = new Vector3(side * 28.5f, SidewalkSurfaceY - 0.5f, bgZs[i]);
                bg.transform.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f);
                bg.transform.localScale = new Vector3(1.1f, 0.9f + ((i + variantIndex) % 2) * 0.25f, 1.3f);
            }
        }
    }

    private static void AlignSceneChunksForEditPreview()
    {
        var chunks = UnityEngine.Object.FindObjectsByType<WorldChunk>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Array.Sort(chunks, (a, b) => string.CompareOrdinal(a.name, b.name));
        for (int i = 0; i < chunks.Length; i++)
        {
            chunks[i].gameObject.SetActive(true);
            chunks[i].transform.position = new Vector3(0f, 0f, i * chunks[i].Length);
        }
    }

    [MenuItem("Komika/City/Fix Sun & Shadows In Open Scene")]
    public static void FixSunAndShadowsInOpenScene()
    {
        ConfigureLightingFogAndCamera();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[TehranCityBuilder] Updated Directional Light angle (60, 142, 0), soft shadow strength (0.44), and warm Tehran horizon fog.");
    }

    private static void ConfigureLightingFogAndCamera()
    {
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Light mainSun = null;
        foreach (Light l in lights)
        {
            if (l.type != LightType.Directional) continue;
            if (mainSun == null || l.shadows != LightShadows.None)
            {
                if (mainSun != null && mainSun.shadows == LightShadows.None && l.shadows != LightShadows.None)
                {
                    mainSun.gameObject.SetActive(false);
                    mainSun = l;
                }
                else if (mainSun == null)
                {
                    mainSun = l;
                }
                else
                {
                    l.gameObject.SetActive(false);
                }
            }
        }

        if (mainSun != null)
        {
            mainSun.gameObject.SetActive(true);
            mainSun.transform.rotation = Quaternion.Euler(60f, 142f, 0f);
            mainSun.color = new Color(1.0f, 0.955f, 0.875f, 1f);
            mainSun.intensity = 2.05f;
            mainSun.shadows = LightShadows.Soft;
            mainSun.shadowStrength = 0.44f;
            mainSun.shadowBias = 0.05f;
            mainSun.shadowNormalBias = 0.6f;
            RenderSettings.sun = mainSun;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.70f, 0.86f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.80f, 0.76f, 0.70f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.45f, 0.43f, 0.44f, 1f);
        RenderSettings.ambientIntensity = 1.05f;

        // Warm sunlit Tehran morning haze matching Alborz horizon backdrop
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 115f;
        RenderSettings.fogEndDistance = 248f;
        RenderSettings.fogColor = new Color(0.80f, 0.84f, 0.89f, 1f);

        var culling = UnityEngine.Object.FindFirstObjectByType<CameraDistanceCulling>(FindObjectsInactive.Include);
        if (culling != null)
        {
            var so = new SerializedObject(culling);
            SerializedProperty distProp = so.FindProperty("renderDistance");
            if (distProp != null) distProp.floatValue = 450f;
            SerializedProperty cityProp = so.FindProperty("cityCullDistance");
            if (cityProp != null) cityProp.floatValue = 262f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (Camera.main != null)
        {
            Camera.main.farClipPlane = 450f;
            var uac = Camera.main.GetComponent<UniversalAdditionalCameraData>();
            if (uac != null)
            {
                uac.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                uac.antialiasingQuality = AntialiasingQuality.Low;
                EditorUtility.SetDirty(uac);
            }
        }

        // Bring Gameplay Camera closer & slightly lower for a natural third-person runner framing (16:9 wide + jump clearance)
        GameObject gameplayPt = GameObject.Find("Camera Game Transform");
        if (gameplayPt != null)
        {
            gameplayPt.transform.position = new Vector3(0f, 2.55f, -98.40f);
            gameplayPt.transform.rotation = Quaternion.Euler(10.0f, 0f, 0f);
            EditorUtility.SetDirty(gameplayPt);
        }

        var camFollow = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        if (camFollow != null)
        {
            var camSo = new SerializedObject(camFollow);
            var followProp = camSo.FindProperty("horizontalFollowAmount"); if (followProp != null) followProp.floatValue = 0.52f;
            var fovProp = camSo.FindProperty("normalFOV"); if (fovProp != null) fovProp.floatValue = 62f;
            var speedFovProp = camSo.FindProperty("speedFOV"); if (speedFovProp != null) speedFovProp.floatValue = 70f;
            var pitchProp = camSo.FindProperty("pitchAmount"); if (pitchProp != null) pitchProp.floatValue = 3.5f;
            var rollProp = camSo.FindProperty("rollAmount"); if (rollProp != null) rollProp.floatValue = 2.5f;
            camSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(camFollow);
        }
    }

    private static void PlaceHorizonMiladAndAlborz()
    {
        Shader horizonShader = Shader.Find("Komika/Tehran Horizon") ?? Shader.Find("Universal Render Pipeline/Lit");
        Color hazeColor = new Color(0.80f, 0.84f, 0.89f, 1f);

        // 0. Solid urban horizon ground skirt under Alborz foothills & Milad Tower so nothing ever floats in skybox
        GameObject oldGround = GameObject.Find("Tehran_Horizon_Ground");
        if (oldGround != null) UnityEngine.Object.DestroyImmediate(oldGround);

        string albMatPath = $"{CityMatDir}/Mat_Alborz_Horizon.mat";
        Material albMat = AssetDatabase.LoadAssetAtPath<Material>(albMatPath);
        if (albMat == null)
        {
            albMat = new Material(horizonShader);
            AssetDatabase.CreateAsset(albMat, albMatPath);
        }
        albMat.shader = horizonShader;
        Texture2D albTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CityTexDir}/Tehran_Alborz_Albedo.png");
        albMat.SetTexture("_BaseMap", albTex);
        albMat.SetColor("_BaseColor", Color.white);
        albMat.SetColor("_HazeColor", hazeColor);
        albMat.SetFloat("_HazeBaseY", -36f);
        albMat.SetFloat("_HazeTopY", 24f);
        albMat.SetFloat("_HazeStrength", 0.34f);
        albMat.SetFloat("_MinAtmosphere", 0.06f);
        EditorUtility.SetDirty(albMat);

        GameObject groundFbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{CityModelDir}/Tehran_Horizon_Ground.fbx");
        if (groundFbx != null)
        {
            GameObject groundGo = (GameObject)PrefabUtility.InstantiatePrefab(groundFbx);
            groundGo.name = "Tehran_Horizon_Ground";
            groundGo.transform.position = new Vector3(0f, -21f, 205f);
            groundGo.transform.rotation = Quaternion.identity;
            groundGo.transform.localScale = new Vector3(1.6f, 1f, 1.35f);
            foreach (var r in groundGo.GetComponentsInChildren<Renderer>(true))
            {
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = albMat;
                r.sharedMaterials = arr;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        // 1. Snow-Capped Alborz / Tochal Mountain Range well behind Milad Tower (never overlapping Milad shaft!)
        GameObject oldAlb = GameObject.Find("Tehran_Horizon_Alborz");
        if (oldAlb != null) UnityEngine.Object.DestroyImmediate(oldAlb);

        GameObject alborzFbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{CityModelDir}/Tehran_Alborz_Mountains.fbx");
        if (alborzFbx != null)
        {
            GameObject alborz = (GameObject)PrefabUtility.InstantiatePrefab(alborzFbx);
            alborz.name = "Tehran_Horizon_Alborz";
            alborz.transform.position = new Vector3(-42f, -24f, 248f);
            alborz.transform.rotation = Quaternion.identity;
            alborz.transform.localScale = new Vector3(1.42f, 1.32f, 1.10f);
            foreach (var r in alborz.GetComponentsInChildren<Renderer>(true))
            {
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = albMat;
                r.sharedMaterials = arr;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        // 2. Milad Tower firmly anchored behind the curved boulevard horizon & clearly in front of Alborz foothills
        GameObject oldMilad = GameObject.Find("Tehran_Horizon_MiladTower");
        if (oldMilad != null) UnityEngine.Object.DestroyImmediate(oldMilad);

        GameObject miladFbx = AssetDatabase.LoadAssetAtPath<GameObject>(MiladFbxPath);
        if (miladFbx == null) return;

        string miladMatPath = $"{CityMatDir}/Mat_Milad_Horizon.mat";
        Material miladMat = AssetDatabase.LoadAssetAtPath<Material>(miladMatPath);
        if (miladMat == null)
        {
            miladMat = new Material(horizonShader);
            AssetDatabase.CreateAsset(miladMat, miladMatPath);
        }
        miladMat.shader = horizonShader;
        Texture2D mTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MiladBaseTexPath);
        if (mTex != null) miladMat.SetTexture("_BaseMap", mTex);
        miladMat.SetColor("_BaseColor", new Color(0.80f, 0.78f, 0.76f, 1f));
        miladMat.SetColor("_HazeColor", hazeColor);
        miladMat.SetFloat("_HazeBaseY", -60f);
        miladMat.SetFloat("_HazeTopY", 40f);
        miladMat.SetFloat("_HazeStrength", 0.14f);
        miladMat.SetFloat("_MinAtmosphere", 0.04f);
        EditorUtility.SetDirty(miladMat);

        GameObject horizonMilad = (GameObject)PrefabUtility.InstantiatePrefab(miladFbx);
        horizonMilad.name = "Tehran_Horizon_MiladTower";
        horizonMilad.transform.position = Vector3.zero;
        horizonMilad.transform.rotation = Quaternion.identity;
        horizonMilad.transform.localScale = Vector3.one;

        foreach (var r in horizonMilad.GetComponentsInChildren<Renderer>(true))
        {
            var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < arr.Length; i++) arr[i] = miladMat;
            r.sharedMaterials = arr;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        Bounds b = ComputeRendererBounds(horizonMilad);
        float targetHeight = 96f;
        float s = b.size.y > 0.01f ? targetHeight / b.size.y : 1f;
        horizonMilad.transform.localScale = Vector3.one * s;
        Bounds sb = ComputeRendererBounds(horizonMilad);
        // Anchor Milad base solidly into the horizon plateau (44m below 0, ~44m clear in front of Alborz foothills)
        horizonMilad.transform.position = new Vector3(6.5f, -44f - sb.min.y, 138f);

        int horizonLayer = LayerMask.NameToLayer("Horizon");
        if (horizonLayer >= 0)
        {
            void SetLayerRecursive(GameObject rootObj)
            {
                if (rootObj == null) return;
                foreach (Transform tr in rootObj.GetComponentsInChildren<Transform>(true))
                    tr.gameObject.layer = horizonLayer;
            }
            SetLayerRecursive(GameObject.Find("Tehran_Horizon_Ground"));
            SetLayerRecursive(GameObject.Find("Tehran_Horizon_Alborz"));
            SetLayerRecursive(horizonMilad);
        }
    }

    private static void BuildMenuStreetBackdrop(CityPrefabs p)
    {
        GameObject old = GameObject.Find("Menu_Street_Backdrop");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        GameObject backdrop = new GameObject("Menu_Street_Backdrop");
        backdrop.AddComponent<MenuStreetBackdrop>();

        for (int i = 0; i < 3; i++)
        {
            GameObject seg = (GameObject)PrefabUtility.InstantiatePrefab(p.Road20m, backdrop.transform);
            seg.name = $"Menu_Road_{-110 - i * 20}";
            seg.transform.localPosition = new Vector3(0f, 0f, -110f - i * 20f);
        }

        void PlaceMenuObj(GameObject src, string name, float x, float z, float yaw)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(src, backdrop.transform);
            go.name = name;
            go.transform.localPosition = new Vector3(x, SidewalkSurfaceY, z);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        PlaceMenuObj(p.Building03,        "Menu_B03_L1",     -12.1f, -108f,  90f);
        PlaceMenuObj(p.Building01,        "Menu_B01_R1",      11.6f, -108f, -90f);
        PlaceMenuObj(p.Dake,              "Menu_Dake_L",      -7.05f, -118f,  90f);
        PlaceMenuObj(p.BusStop,           "Menu_BusStop_R",    7.05f, -120f, -90f);
        if (p.Peugeot206 != null) PlaceMenuObj(p.Peugeot206, "Menu_Peugeot206_L", -7.02f, -126.5f, 16f);
        PlaceMenuObj(p.ChinarTree,        "Menu_Tree_L1",     -6.10f, -102f,  40f);
        PlaceMenuObj(p.ChinarTree,        "Menu_Tree_R1",      6.10f, -102f, 140f);
        PlaceMenuObj(p.StreetLamp,        "Menu_Lamp_L1",     -5.82f, -114f,  90f);
        PlaceMenuObj(p.StreetLamp,        "Menu_Lamp_R1",      5.82f, -114f, -90f);
        PlaceMenuObj(p.Building02,        "Menu_B02_L2",     -14.1f, -126f,  90f);
        PlaceMenuObj(p.Building03,        "Menu_B03_R2",      12.1f, -125f, -90f);
        PlaceMenuObj(p.Building01,        "Menu_B01_L3",     -11.6f, -143f,  90f);
        PlaceMenuObj(p.Building02,        "Menu_B02_R3",      14.1f, -142f, -90f);
        PlaceMenuObj(p.CityBackdropBlock, "Menu_Bg_L",       -28.5f, -126f,  90f);
        PlaceMenuObj(p.CityBackdropBlock, "Menu_Bg_R",        28.5f, -126f, -90f);
    }

    private static void CaptureVerificationScreenshots()
    {
        string outDir = @"C:\Users\Nima\.gemini\antigravity\brain\7b9ba8fe-181b-4459-b89e-1cb7eb5be3d3\scratch\unity_verify";
        Directory.CreateDirectory(outDir);

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 origPos = cam.transform.position;
        Quaternion origRot = cam.transform.rotation;

        void RenderShot(Vector3 pos, Quaternion rot, string fileName)
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            Shader.SetGlobalVector("_KomikaCurveOrigin", new Vector4(0f, 0f, -92.84f, 1f));
            Shader.SetGlobalVector("_KomikaCurveParams", new Vector4(0.0012f, 0f, 160f, 0f));

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();

            File.WriteAllBytes(Path.Combine(outDir, fileName), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = prevTarget;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        RenderShot(new Vector3(0f, 0.45f, -90.50f), Quaternion.Euler(4f, 180f, 0f), "verify_menu_idle.png");
        RenderShot(new Vector3(0f, 2.55f, -98.40f), Quaternion.Euler(10.0f, 0f, 0f), "verify_gameplay.png");
        RenderShot(new Vector3(-7f, 14f, -35f), Quaternion.Euler(18f, 12f, 0f), "verify_street_overview.png");

        // Capture Landscape UI screenshots using temporary ScreenSpaceCamera overlay (never saved back to scene)
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas != null)
        {
            RenderMode origMode = canvas.renderMode;
            Camera origCam = canvas.worldCamera;
            float origPlane = canvas.planeDistance;
            float origNear = cam.nearClipPlane;
            var uac = cam.GetComponent<UniversalAdditionalCameraData>();
            bool origPP = uac != null && uac.renderPostProcessing;
            AntialiasingMode origAA = uac != null ? uac.antialiasing : AntialiasingMode.None;
            if (uac != null)
            {
                uac.renderPostProcessing = false;
                uac.antialiasing = AntialiasingMode.None;
            }
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.8f;
            cam.nearClipPlane = 0.5f;

            Transform komikaUi = canvas.transform.Find("Komika UI");
            GameObject menuExt = komikaUi != null ? komikaUi.Find("Menu Extensions")?.gameObject : null;
            GameObject hudGo = komikaUi != null ? komikaUi.Find("Subway HUD")?.gameObject : null;
            GameObject overGo = komikaUi != null ? komikaUi.Find("Komika Game Over Panel")?.gameObject : null;
            GameObject helpGo = komikaUi != null ? komikaUi.Find("How To Play Panel")?.gameObject : null;
            GameObject charGo = komikaUi != null ? komikaUi.Find("Character Selection Panel")?.gameObject : null;

            void SetPanelShot(GameObject activeTarget)
            {
                foreach (var g in new[] { menuExt, hudGo, overGo, helpGo, charGo })
                {
                    if (g == null) continue;
                    g.SetActive(g == activeTarget);
                    var cg = g.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = (g == activeTarget) ? 1f : 0f;
                }
            }

            SetPanelShot(menuExt);
            RenderShot(new Vector3(0f, 0.45f, -90.50f), Quaternion.Euler(4f, 180f, 0f), "verify_ui_menu.png");

            SetPanelShot(hudGo);
            RenderShot(new Vector3(0f, 2.55f, -98.40f), Quaternion.Euler(10.0f, 0f, 0f), "verify_ui_hud.png");

            SetPanelShot(overGo);
            RenderShot(new Vector3(0f, 2.55f, -98.40f), Quaternion.Euler(10.0f, 0f, 0f), "verify_ui_gameover.png");

            SetPanelShot(helpGo);
            RenderShot(new Vector3(0f, 0.45f, -90.50f), Quaternion.Euler(4f, 180f, 0f), "verify_ui_controls.png");

            SetPanelShot(charGo);
            RenderShot(new Vector3(0f, 0.45f, -90.50f), Quaternion.Euler(4f, 180f, 0f), "verify_ui_characters.png");

            SetPanelShot(menuExt);
            if (uac != null)
            {
                uac.renderPostProcessing = origPP;
                uac.antialiasing = origAA;
            }
            canvas.renderMode = origMode;
            canvas.worldCamera = origCam;
            canvas.planeDistance = origPlane;
            cam.nearClipPlane = origNear;
        }

        GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
        GameObject car206ObsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Car206ObstaclePrefabPath);
        GameObject truckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPrefabPath);
        GameObject temp206 = car206ObsPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(car206ObsPrefab) : null;
        GameObject tempTaxi = carPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(carPrefab) : null;
        GameObject tempBus = truckPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(truckPrefab) : null;
        if (temp206 != null) { temp206.transform.position = new Vector3(-2.6f, -1.0f, -80f); temp206.transform.rotation = Quaternion.Euler(0f, -18f, 0f); }
        if (tempTaxi != null) { tempTaxi.transform.position = new Vector3(2.8f, -1.0f, -77f); tempTaxi.transform.rotation = Quaternion.Euler(0f, 158f, 0f); }
        if (tempBus != null) { tempBus.transform.position = new Vector3(0.1f, -1.0f, -66f); tempBus.transform.rotation = Quaternion.identity; }
        RenderShot(new Vector3(0.0f, 2.45f, -89f), Quaternion.Euler(10f, 0f, 0f), "verify_vehicles.png");
        if (temp206 != null) UnityEngine.Object.DestroyImmediate(temp206);
        if (tempTaxi != null) UnityEngine.Object.DestroyImmediate(tempTaxi);
        if (tempBus != null) UnityEngine.Object.DestroyImmediate(tempBus);

        // Close-up of realistic Valiasr Chinar trees + Bakery Persian RTL signs ("شیرینی‌سرای ولیعصر" / "شیرینی‌فروشی")
        RenderShot(new Vector3(0.8f, 2.3f, -101.5f), Quaternion.Euler(6f, 34f, 0f), "verify_bakery_trees.png");
        // Close-up approaching Metro Station ("ایستگاه مترو تئاتر شهر") + Highway Gantry & Chinar leaves
        RenderShot(new Vector3(0.5f, 2.4f, -25.5f), Quaternion.Euler(4f, -28f, 0f), "verify_metro_signs.png");

        cam.transform.SetPositionAndRotation(origPos, origRot);
    }

    private static void DestroyChildIfExists(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }
}
