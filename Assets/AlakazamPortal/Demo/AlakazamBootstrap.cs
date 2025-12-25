using UnityEngine;

namespace AlakazamPortal.Demo
{
    /// <summary>
    /// Drop this on any GameObject in an empty scene and it creates the entire Alakazam demo.
    /// Just set your server URL, then hit Play.
    /// </summary>
    public class AlakazamBootstrap : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private string serverUrl = "ws://localhost:9001";

        [Header("Options")]
        [SerializeField] private string initialPrompt = "anime style, vibrant colors, cel shading";
        [SerializeField] private bool autoStart = false;

        private void Awake()
        {
            CreateDemoScene();
        }

        private void CreateDemoScene()
        {
            // Setup camera
            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                cam = camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
                camGO.tag = "MainCamera";
            }
            cam.transform.position = new Vector3(0, 5, -10);
            cam.transform.LookAt(Vector3.zero);
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            // Create ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(3, 1, 3);
            ground.tag = "Ground";
            ground.GetComponent<MeshRenderer>().material = CreateMaterial(new Color(0.3f, 0.3f, 0.35f));

            // Create walking mannequin
            var mannequinGO = new GameObject("WalkingMannequin");
            mannequinGO.transform.position = new Vector3(0, 0, 0);
            mannequinGO.AddComponent<WalkingMannequin>();

            // Decoration cubes
            CreateDecorCube(new Vector3(-5, 0.5f, 5), new Color(1f, 0.3f, 0.3f));
            CreateDecorCube(new Vector3(5, 0.75f, 5), new Color(0.3f, 1f, 0.3f));
            CreateDecorCube(new Vector3(-5, 1f, -5), new Color(1f, 1f, 0.3f));
            CreateDecorCube(new Vector3(5, 0.6f, -5), new Color(1f, 0.3f, 1f));

            // Light
            var lightGO = new GameObject("DirectionalLight");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.9f);
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

            // AlakazamController
            var alakazamGO = new GameObject("AlakazamController");
            var alakazam = alakazamGO.AddComponent<AlakazamController>();

            // Set fields via reflection (fields are serialized private)
            var serverUrlField = typeof(AlakazamController).GetField("serverUrl",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var promptField = typeof(AlakazamController).GetField("prompt",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (serverUrlField != null) serverUrlField.SetValue(alakazam, serverUrl);
            if (promptField != null) promptField.SetValue(alakazam, initialPrompt);

            // ShowcaseUI
            var uiGO = new GameObject("ShowcaseUI");
            uiGO.AddComponent<ShowcaseUI>();

            Debug.Log("[AlakazamBootstrap] Demo scene created!");

            if (autoStart)
            {
                alakazam.StartAlakazam();
            }

            // Self-destruct bootstrap object
            Destroy(gameObject);
        }

        private void CreateDecorCube(Vector3 pos, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "DecorCube";
            cube.transform.position = pos;
            cube.transform.localScale = Vector3.one * Random.Range(0.8f, 1.5f);
            cube.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            cube.GetComponent<MeshRenderer>().material = CreateMaterial(color, 0.3f, 0.5f);
        }

        private Material CreateMaterial(Color color, float metallic = 0f, float smoothness = 0.5f)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }
    }
}
