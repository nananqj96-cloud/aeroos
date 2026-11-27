using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRoad.Core;
using OpenRoad.Rendering;
using OpenRoad.Spike;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenRoad.Editor
{
    /// <summary>
    /// Reproducibly generates the ignored Phase 0 scene/settings from tracked source, and exposes
    /// the custom build entry point used by GameCI.
    /// </summary>
    [InitializeOnLoad]
    public static class SpikeProjectBuilder
    {
        private const string GeneratedRoot = "Assets/OpenRoad/Generated";
        private const string SettingsRoot = GeneratedRoot + "/Settings";
        private const string MaterialsRoot = GeneratedRoot + "/Materials";
        private const string MeshesRoot = GeneratedRoot + "/Meshes";
        private const string ScenePath = GeneratedRoot + "/OpenRoadPhase0.unity";
        private const string RendererDataPath = SettingsRoot + "/OpenRoadForwardRenderer.asset";
        private const string PipelineAssetPath = SettingsRoot + "/OpenRoadLowPipeline.asset";
        private const string MarkingsMeshPath = MeshesRoot + "/RoadMarkings.asset";
        private const string FisheyeShaderPath = "Assets/OpenRoad/Rendering/HorizontalFisheye.shader";
        private static bool s_BootstrapQueued;

        static SpikeProjectBuilder()
        {
            QueueBootstrap();
        }

        [InitializeOnLoadMethod]
        private static void QueueBootstrap()
        {
            if (s_BootstrapQueued) return;
            s_BootstrapQueued = true;
            EditorApplication.delayCall += BootstrapAfterImport;
        }

        private static void BootstrapAfterImport()
        {
            s_BootstrapQueued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueBootstrap();
                return;
            }

            try
            {
                EnsureProjectReady(openGeneratedScene: !Application.isBatchMode);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("OPEN ROAD/Generate or repair Phase 0 spike")]
        public static void GenerateOrRepairSpike()
        {
            EnsureProjectReady(openGeneratedScene: true);
        }

        [MenuItem("OPEN ROAD/Regenerate Phase 0 scene")]
        public static void RegenerateScene()
        {
            EnsureFolders();
            EnsureMaterials();
            EnsurePipelineAssets();
            AssetDatabase.DeleteAsset(ScenePath);
            CreateSpikeScene();
            ConfigureProjectSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("OPEN ROAD Phase 0 scene regenerated at " + ScenePath);
        }

        [MenuItem("OPEN ROAD/Build Windows x64 locally")]
        public static void BuildWindowsLocally()
        {
            EnsureProjectReady(openGeneratedScene: false);
            ConfigurePlayerSettings();
            string outputPath = Path.GetFullPath(Path.Combine("build", "StandaloneWindows64", "OpenRoad.exe"));
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                target = BuildTarget.StandaloneWindows64,
                locationPathName = outputPath,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("OPEN ROAD local Windows build failed: " + report.summary.result);
            }
            Debug.Log("OPEN ROAD local Windows player built: " + outputPath);
        }

        /// <summary>
        /// GameCI invokes this method with -buildTarget, -customBuildPath and -buildVersion. It
        /// mirrors the relevant default builder behavior while ensuring generated content exists.
        /// </summary>
        public static void BuildForGameCI()
        {
            try
            {
                EnsureProjectReady(openGeneratedScene: false);
                ConfigurePlayerSettings();

                string targetName = ReadCommandLineArgument("buildTarget", "StandaloneWindows64");
                string customBuildPath = ReadCommandLineArgument("customBuildPath", string.Empty);
                string buildVersion = ReadCommandLineArgument("buildVersion", string.Empty);

                if (!Enum.TryParse(targetName, out BuildTarget target))
                {
                    throw new InvalidOperationException("Unrecognized GameCI build target: " + targetName);
                }
                if (string.IsNullOrWhiteSpace(customBuildPath))
                {
                    throw new InvalidOperationException("GameCI did not supply -customBuildPath.");
                }
                if (!string.IsNullOrWhiteSpace(buildVersion))
                {
                    PlayerSettings.bundleVersion = buildVersion;
                }

                var options = new BuildPlayerOptions
                {
                    scenes = GetEnabledScenes(),
                    target = target,
                    locationPathName = customBuildPath,
                    options = BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log("OPEN ROAD GameCI build result: " + report.summary.result
                    + ", warnings=" + report.summary.totalWarnings
                    + ", errors=" + report.summary.totalErrors
                    + ", output=" + customBuildPath);

                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureProjectReady(bool openGeneratedScene)
        {
            EnsureFolders();
            EnsureMaterials();
            EnsurePipelineAssets();
            ConfigureProjectSettings();

            if (!File.Exists(ScenePath))
            {
                CreateSpikeScene();
            }

            SetBuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openGeneratedScene && !EditorApplication.isPlaying)
            {
                Scene active = SceneManager.GetActiveScene();
                if (active.path != ScenePath && !active.isDirty)
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/OpenRoad");
            EnsureFolder(GeneratedRoot);
            EnsureFolder(SettingsRoot);
            EnsureFolder(MaterialsRoot);
            EnsureFolder(MeshesRoot);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent)) AssetDatabase.CreateFolder(parent, name);
        }

        private static void EnsurePipelineAssets()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "OPEN ROAD Forward Renderer (Phase 0)";
                AssetDatabase.CreateAsset(rendererData, RendererDataPath);
            }

            var feature = FindFeature(rendererData);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<HorizontalFisheyeRendererFeature>();
                feature.name = "OPEN ROAD Horizontal Fisheye";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
            }

            feature.fisheyeShader = AssetDatabase.LoadAssetAtPath<Shader>(FisheyeShaderPath);
            if (feature.fisheyeShader == null)
            {
                feature.fisheyeShader = Shader.Find("Hidden/OpenRoad/HorizontalFisheye");
            }
            EditorUtility.SetDirty(feature);
            RegisterFeature(rendererData, feature);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.name = "OPEN ROAD Low URP Pipeline";
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }

            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            pipeline.renderScale = 1f;
            pipeline.upscalingFilter = UpscalingFilterSelection.FSR;
            pipeline.fsrOverrideSharpness = true;
            pipeline.fsrSharpness = 0.35f;
            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static HorizontalFisheyeRendererFeature FindFeature(UniversalRendererData rendererData)
        {
            if (rendererData == null || rendererData.rendererFeatures == null) return null;
            for (int i = 0; i < rendererData.rendererFeatures.Count; i++)
            {
                if (rendererData.rendererFeatures[i] is HorizontalFisheyeRendererFeature feature) return feature;
            }
            return null;
        }

        private static void RegisterFeature(UniversalRendererData rendererData, HorizontalFisheyeRendererFeature feature)
        {
            var serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
            if (features == null)
            {
                throw new InvalidOperationException("URP renderer data no longer exposes m_RendererFeatures; update the Phase 0 bootstrap for this URP version.");
            }

            int featureIndex = -1;
            for (int i = 0; i < features.arraySize; i++)
            {
                if (features.GetArrayElementAtIndex(i).objectReferenceValue == feature)
                {
                    featureIndex = i;
                    break;
                }
            }
            if (featureIndex < 0)
            {
                featureIndex = features.arraySize;
                features.InsertArrayElementAtIndex(featureIndex);
                features.GetArrayElementAtIndex(featureIndex).objectReferenceValue = feature;
            }

            if (featureMap != null)
            {
                featureMap.arraySize = features.arraySize;
                for (int i = 0; i < features.arraySize; i++)
                {
                    UnityEngine.Object featureObject = features.GetArrayElementAtIndex(i).objectReferenceValue;
                    long localId = 0;
                    if (featureObject != null)
                    {
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(featureObject, out string _, out localId);
                    }
                    featureMap.GetArrayElementAtIndex(i).longValue = localId;
                }
            }

            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
        }

        private static void EnsureMaterials()
        {
            EnsureMaterial("Road.mat", new Color(0.075f, 0.085f, 0.095f), 0f, 0.24f);
            EnsureMaterial("Ground.mat", new Color(0.16f, 0.23f, 0.18f), 0f, 0.16f);
            EnsureMaterial("Markings.mat", new Color(0.85f, 0.82f, 0.64f), 0f, 0.35f);
            EnsureMaterial("Building.mat", new Color(0.37f, 0.40f, 0.42f), 0f, 0.35f);
            EnsureMaterial("BikePaint.mat", new Color(0.03f, 0.23f, 0.32f), 0.42f, 0.56f);
            EnsureMaterial("BikeRubber.mat", new Color(0.035f, 0.04f, 0.045f), 0f, 0.16f);
            EnsureMaterial("BikeMetal.mat", new Color(0.28f, 0.31f, 0.33f), 0.72f, 0.48f);
            EnsureMaterial("BikeGlass.mat", new Color(0.1f, 0.36f, 0.43f), 0.18f, 0.72f);
            EnsureMaterial("Traffic.mat", new Color(0.27f, 0.37f, 0.43f), 0.12f, 0.36f);
        }

        private static Material EnsureMaterial(string fileName, Color baseColor, float metallic, float smoothness)
        {
            string path = MaterialsRoot + "/" + fileName;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null) throw new InvalidOperationException("Could not find URP Lit shader while creating " + fileName);
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(fileName) };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            else material.color = baseColor;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureProjectSettings()
        {
            ConfigurePlayerSettings();
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
            Application.runInBackground = true;
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "OPEN ROAD";
            PlayerSettings.productName = "OpenRoad";
            if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)) PlayerSettings.bundleVersion = "0.1.0-dev";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        }

        private static void SetBuildScene()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static string[] GetEnabledScenes()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes were generated for the OPEN ROAD build.");
            }
            return scenes;
        }

        private static void CreateSpikeScene()
        {
            Material roadMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/Road.mat");
            Material groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/Ground.mat");
            Material markingsMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/Markings.mat");
            Material buildingMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/Building.mat");
            Material bikePaint = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/BikePaint.mat");
            Material bikeRubber = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/BikeRubber.mat");
            Material bikeMetal = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/BikeMetal.mat");
            Material bikeGlass = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/BikeGlass.mat");
            Material trafficMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/Traffic.mat");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "OpenRoadPhase0";
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.31f, 0.40f, 0.50f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.25f, 0.23f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.09f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.55f, 0.62f, 0.67f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 250f;
            RenderSettings.fogEndDistance = 1000f;

            GameObject directional = new GameObject("Sun - Phase 0 fixed clear weather");
            Light sun = directional.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.05f;
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.shadows = LightShadows.Hard;
            directional.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            GameObject ground = CreatePrimitive("Ground / test terrain", PrimitiveType.Cube, new Vector3(0f, -0.17f, 0f), new Vector3(220f, 0.18f, 1200f), groundMaterial);
            RemoveCollider(ground);
            GameObject road = CreatePrimitive("Road / 1,000 m strip", PrimitiveType.Cube, new Vector3(0f, -0.015f, 0f), new Vector3(14f, 0.16f, 1000f), roadMaterial);
            RemoveCollider(road);
            CreateRoadMarkings(markingsMaterial);
            CreateRoadsideFixtureBuildings(buildingMaterial);
            CreateLightPoles(bikeMetal);

            GameObject bikeRoot = new GameObject("Bike / Phase 0 procedural performance fixture (not production art)");
            bikeRoot.transform.position = new Vector3(0f, 0.02f, -420f);
            bikeRoot.AddComponent<SpikeRideController>();
            CreateBikeVisualParts(bikeRoot.transform, bikePaint, bikeRubber, bikeMetal, bikeGlass);

            GameObject trafficRoot = new GameObject("Traffic / deterministic pooled fixture");
            TrafficWorld trafficWorld = trafficRoot.AddComponent<TrafficWorld>();
            trafficWorld.requestedVehicleCount = TrafficWorld.LowPresetVehicleCap;
            trafficWorld.seed = BenchmarkRunner.TrafficSeed;
            trafficWorld.trafficMaterial = trafficMaterial;

            GameObject cameraObject = new GameObject("Player Camera / lens test camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 84f;
            camera.nearClipPlane = 0.06f;
            camera.farClipPlane = 1400f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = true;
            camera.useOcclusionCulling = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.46f, 0.62f, 0.72f);
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;

            LensCameraMarker lensMarker = cameraObject.AddComponent<LensCameraMarker>();
            lensMarker.distortionStrength = 0.92f;
            lensMarker.edgeCoverage = 0.93f;
            lensMarker.verticalScale = 1f;

            SpikeCameraRig cameraRig = cameraObject.AddComponent<SpikeCameraRig>();
            cameraRig.playerCamera = camera;
            cameraRig.bikeRoot = bikeRoot.transform;
            cameraRig.lensMarker = lensMarker;
            cameraObject.transform.position = bikeRoot.transform.position - Vector3.forward * 7.2f + Vector3.up * 2.65f;
            cameraObject.transform.rotation = Quaternion.LookRotation(bikeRoot.transform.position + Vector3.forward * 9f + Vector3.up * 1.15f - cameraObject.transform.position, Vector3.up);

            GameObject mirrorRigObject = new GameObject("Mirrors / reflected-eye render rig");
            MirrorCameraRig mirrorRig = mirrorRigObject.AddComponent<MirrorCameraRig>();
            mirrorRig.bikeRoot = bikeRoot.transform;
            mirrorRig.eyeCamera = camera;
            mirrorRig.mirrorsEnabledAtStart = true;

            GameObject benchmarkObject = new GameObject("Benchmark and CI smoke entry point");
            BenchmarkRunner benchmark = benchmarkObject.AddComponent<BenchmarkRunner>();
            benchmark.rideController = bikeRoot.GetComponent<SpikeRideController>();
            benchmark.trafficWorld = trafficWorld;
            benchmark.mirrorRig = mirrorRig;
            benchmark.cameraRig = cameraRig;
            benchmark.lensMarker = lensMarker;
            benchmark.playerCamera = camera;
            benchmarkObject.AddComponent<StartupSmokeTest>();

            SetBuildScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateRoadMarkings(Material markingsMaterial)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MarkingsMeshPath);
            if (mesh == null)
            {
                mesh = BuildRoadMarkingsMesh();
                AssetDatabase.CreateAsset(mesh, MarkingsMeshPath);
            }

            var markings = new GameObject("Road markings / dashed center and edges");
            var filter = markings.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = markings.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = markingsMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh BuildRoadMarkingsMesh()
        {
            var vertices = new List<Vector3>(512);
            var triangles = new List<int>(768);
            AddRect(vertices, triangles, -6.25f, 0.0705f, -500f, 0.12f, 1000f);
            AddRect(vertices, triangles, 6.13f, 0.0705f, -500f, 0.12f, 1000f);
            for (float z = -496f; z < 500f; z += 14f)
            {
                AddRect(vertices, triangles, -0.075f, 0.071f, z, 0.15f, 6f);
            }

            var mesh = new Mesh { name = "OPEN ROAD / road strip markings" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddRect(List<Vector3> vertices, List<int> triangles, float x, float y, float z, float width, float length)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(x, y, z));
            vertices.Add(new Vector3(x + width, y, z));
            vertices.Add(new Vector3(x + width, y, z + length));
            vertices.Add(new Vector3(x, y, z + length));
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }

        private static void CreateRoadsideFixtureBuildings(Material buildingMaterial)
        {
            for (int i = 0; i < 8; i++)
            {
                float z = -430f + i * 120f;
                float heightA = 6f + (i % 3) * 2f;
                float heightB = 7f + ((i + 1) % 4) * 1.5f;
                CreatePrimitive("Fixture building L " + i, PrimitiveType.Cube,
                    new Vector3(-17f, heightA * 0.5f, z), new Vector3(10f, heightA, 22f), buildingMaterial);
                CreatePrimitive("Fixture building R " + i, PrimitiveType.Cube,
                    new Vector3(17f, heightB * 0.5f, z + 34f), new Vector3(11f, heightB, 26f), buildingMaterial);
            }
        }

        private static void CreateLightPoles(Material metalMaterial)
        {
            for (int i = 0; i < 8; i++)
            {
                float z = -440f + i * 130f;
                CreatePrimitive("Roadside post L " + i, PrimitiveType.Cylinder,
                    new Vector3(-8.4f, 3.1f, z), new Vector3(0.12f, 3.1f, 0.12f), metalMaterial);
                CreatePrimitive("Roadside post R " + i, PrimitiveType.Cylinder,
                    new Vector3(8.4f, 3.1f, z + 58f), new Vector3(0.12f, 3.1f, 0.12f), metalMaterial);
            }
        }

        private static void CreateBikeVisualParts(Transform root, Material paint, Material rubber, Material metal, Material glass)
        {
            AddBikePart(root, "Rear tire", PrimitiveType.Cylinder, new Vector3(0f, 0.39f, -0.97f), new Vector3(0.78f, 0.15f, 0.78f), Quaternion.Euler(0f, 0f, 90f), rubber);
            AddBikePart(root, "Front tire", PrimitiveType.Cylinder, new Vector3(0f, 0.39f, 1.06f), new Vector3(0.78f, 0.15f, 0.78f), Quaternion.Euler(0f, 0f, 90f), rubber);
            AddBikePart(root, "Rear hub", PrimitiveType.Cylinder, new Vector3(0f, 0.39f, -0.97f), new Vector3(0.28f, 0.18f, 0.28f), Quaternion.Euler(0f, 0f, 90f), metal);
            AddBikePart(root, "Front hub", PrimitiveType.Cylinder, new Vector3(0f, 0.39f, 1.06f), new Vector3(0.28f, 0.18f, 0.28f), Quaternion.Euler(0f, 0f, 90f), metal);
            AddBikePart(root, "Lower frame", PrimitiveType.Cube, new Vector3(0f, 0.76f, -0.03f), new Vector3(0.28f, 0.28f, 1.24f), Quaternion.identity, metal);
            AddBikePart(root, "Engine block", PrimitiveType.Cube, new Vector3(0f, 0.76f, 0.03f), new Vector3(0.58f, 0.5f, 0.62f), Quaternion.identity, metal);
            AddBikePart(root, "Fuel tank", PrimitiveType.Capsule, new Vector3(0f, 1.11f, -0.10f), new Vector3(0.48f, 0.34f, 0.42f), Quaternion.Euler(90f, 0f, 0f), paint);
            AddBikePart(root, "Front fairing", PrimitiveType.Capsule, new Vector3(0f, 1.08f, 0.63f), new Vector3(0.48f, 0.38f, 0.44f), Quaternion.Euler(90f, 0f, 0f), paint);
            AddBikePart(root, "Tail fairing", PrimitiveType.Cube, new Vector3(0f, 1.04f, -0.70f), new Vector3(0.42f, 0.26f, 0.58f), Quaternion.identity, paint);
            AddBikePart(root, "Seat", PrimitiveType.Cube, new Vector3(0f, 1.22f, -0.36f), new Vector3(0.42f, 0.12f, 0.66f), Quaternion.identity, rubber);
            AddBikePart(root, "Windshield fixture", PrimitiveType.Quad, new Vector3(0f, 1.47f, 0.89f), new Vector3(0.46f, 0.36f, 1f), Quaternion.Euler(-14f, 0f, 0f), glass);
            AddBikePart(root, "Handlebar", PrimitiveType.Cylinder, new Vector3(0f, 1.22f, 0.52f), new Vector3(0.045f, 0.37f, 0.045f), Quaternion.Euler(0f, 0f, 90f), metal);
            AddBikePart(root, "Dashboard pod", PrimitiveType.Cube, new Vector3(0f, 1.28f, 0.67f), new Vector3(0.30f, 0.16f, 0.18f), Quaternion.Euler(-12f, 0f, 0f), metal);
            AddBikePart(root, "Front lamp", PrimitiveType.Cube, new Vector3(0f, 0.94f, 0.94f), new Vector3(0.30f, 0.12f, 0.06f), Quaternion.identity, glass);
        }

        private static void AddBikePart(Transform root, string partName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = "Fixture part / " + partName;
            part.transform.SetParent(root, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            RemoveCollider(part);
        }

        private static GameObject CreatePrimitive(string objectName, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = objectName;
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            return gameObject;
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }

        private static string ReadCommandLineArgument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            string flag = "-" + name;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    return args[i + 1];
                }

                string prefix = flag + "=";
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i].Substring(prefix.Length);
                }
            }
            return fallback;
        }
    }
}
