using UnityEngine;

namespace AlakazamPortal.Demo
{
    /// <summary>
    /// Quick setup component - adds AlakazamController and UI to any existing scene.
    /// Drop this on any GameObject, configure your settings, and hit Play.
    /// </summary>
    public class AlakazamBootstrap : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private string serverUrl = "ws://35.224.217.144:9001";

        [Header("Style")]
        [SerializeField] private string initialPrompt = "anime style, vibrant colors, cel shading";

        [Header("Options")]
        [SerializeField] private bool autoStart = false;
        [SerializeField] private bool addShowcaseUI = true;

        private void Awake()
        {
            SetupAlakazam();
        }

        private void SetupAlakazam()
        {
            // Ensure we have a camera
            if (Camera.main == null)
            {
                Debug.LogError("[AlakazamBootstrap] No Main Camera found. Please add a camera to your scene.");
                return;
            }

            // Create AlakazamController
            var alakazamGO = new GameObject("AlakazamController");
            var alakazam = alakazamGO.AddComponent<AlakazamController>();

            // Set fields via reflection (fields are serialized private)
            var serverUrlField = typeof(AlakazamController).GetField("serverUrl",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var promptField = typeof(AlakazamController).GetField("prompt",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (serverUrlField != null) serverUrlField.SetValue(alakazam, serverUrl);
            if (promptField != null) promptField.SetValue(alakazam, initialPrompt);

            // Optionally add ShowcaseUI
            if (addShowcaseUI)
            {
                var uiGO = new GameObject("ShowcaseUI");
                uiGO.AddComponent<ShowcaseUI>();
            }

            Debug.Log("[AlakazamBootstrap] Alakazam Portal ready!");

            if (autoStart)
            {
                alakazam.StartAlakazam();
            }

            // Self-destruct bootstrap object
            Destroy(gameObject);
        }
    }
}
