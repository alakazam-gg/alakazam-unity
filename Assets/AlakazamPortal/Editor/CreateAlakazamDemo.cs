using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace AlakazamPortal.Editor
{
    /// <summary>
    /// Editor utility to create ready-to-go Alakazam demo scenes.
    /// </summary>
    public static class CreateAlakazamDemo
    {
        [MenuItem("AlakazamPortal/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Get the main camera (created by default)
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0, 5, -10);
                camera.transform.LookAt(Vector3.zero);
                camera.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
                camera.clearFlags = CameraClearFlags.SolidColor;
            }

            // Create ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(3, 1, 3);
            ground.tag = "Ground";

            // Create ground material
            var groundMat = new Material(Shader.Find("Standard"));
            groundMat.color = new Color(0.3f, 0.3f, 0.35f);
            ground.GetComponent<MeshRenderer>().material = groundMat;

            // Create the moving cube
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "MovingCube";
            cube.transform.position = new Vector3(0, 1, 0);
            cube.transform.localScale = Vector3.one * 1.5f;

            // Create cube material (colorful so transformation is visible)
            var cubeMat = new Material(Shader.Find("Standard"));
            cubeMat.color = new Color(0.2f, 0.6f, 1f);
            cubeMat.SetFloat("_Metallic", 0.5f);
            cubeMat.SetFloat("_Glossiness", 0.7f);
            cube.GetComponent<MeshRenderer>().material = cubeMat;

            // Add CubeMover script
            cube.AddComponent<Demo.CubeMover>();

            // Create some decoration cubes for visual interest
            CreateDecorCube(new Vector3(-5, 0.5f, 5), new Color(1f, 0.3f, 0.3f));
            CreateDecorCube(new Vector3(5, 0.75f, 5), new Color(0.3f, 1f, 0.3f));
            CreateDecorCube(new Vector3(-5, 1f, -5), new Color(1f, 1f, 0.3f));
            CreateDecorCube(new Vector3(5, 0.6f, -5), new Color(1f, 0.3f, 1f));

            // Create directional light
            var lightGO = new GameObject("DirectionalLight");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.9f);
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Create AlakazamController object
            var alakazamGO = new GameObject("AlakazamController");
            alakazamGO.AddComponent<AlakazamController>();

            // Create ShowcaseUI
            var uiGO = new GameObject("ShowcaseUI");
            uiGO.AddComponent<Demo.ShowcaseUI>();

            // Select the AlakazamController so user can configure it
            Selection.activeGameObject = alakazamGO;

            // Save scene
            string scenePath = "Assets/AlakazamPortal/Demo/AlakazamDemo.unity";

            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/AlakazamPortal/Demo"))
            {
                AssetDatabase.CreateFolder("Assets/AlakazamPortal", "Demo");
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[AlakazamPortal] Demo scene created at: {scenePath}");

            EditorUtility.DisplayDialog(
                "Alakazam Demo Scene Created",
                "Demo scene created successfully!\n\n" +
                "Next steps:\n" +
                "1. Select 'AlakazamController' in Hierarchy\n" +
                "2. Set Server URL (e.g., ws://localhost:9001)\n" +
                "3. Press Play to test\n" +
                "4. Press ENTER to start streaming\n" +
                "5. Use [ ] keys to cycle style presets",
                "Got it!"
            );
        }

        private static void CreateDecorCube(Vector3 position, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "DecorCube";
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * Random.Range(0.8f, 1.5f);
            cube.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Metallic", 0.3f);
            mat.SetFloat("_Glossiness", 0.5f);
            cube.GetComponent<MeshRenderer>().material = mat;
        }

        [MenuItem("AlakazamPortal/Add Alakazam to Current Scene")]
        public static void AddAlakazamToScene()
        {
            // Check if already exists
            if (Object.FindObjectOfType<AlakazamController>() != null)
            {
                EditorUtility.DisplayDialog(
                    "Alakazam Already Exists",
                    "AlakazamController is already in the scene.",
                    "OK"
                );
                return;
            }

            // Create AlakazamController object
            var alakazamGO = new GameObject("AlakazamController");
            alakazamGO.AddComponent<AlakazamController>();

            // Create ShowcaseUI
            var uiGO = new GameObject("ShowcaseUI");
            uiGO.AddComponent<Demo.ShowcaseUI>();

            // Mark scene dirty so it can be saved
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            // Select the AlakazamController so user can configure it
            Selection.activeGameObject = alakazamGO;

            Debug.Log("[AlakazamPortal] Alakazam added to scene!");

            EditorUtility.DisplayDialog(
                "Alakazam Added to Scene",
                "AlakazamController added successfully!\n\n" +
                "Next steps:\n" +
                "1. Select 'AlakazamController' in Hierarchy\n" +
                "2. Set Server URL (e.g., ws://localhost:9001)\n" +
                "3. Press Play to test\n" +
                "4. Press ENTER to start streaming",
                "Got it!"
            );
        }
    }
}
