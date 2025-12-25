using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace AlakazamPortal.Editor
{
    /// <summary>
    /// Editor utilities for setting up Alakazam Portal in your scenes.
    /// </summary>
    public static class AlakazamPortalMenu
    {
        [MenuItem("AlakazamPortal/Add to Current Scene")]
        public static void AddAlakazamToScene()
        {
            // Check if already exists
            if (Object.FindObjectOfType<AlakazamController>() != null)
            {
                EditorUtility.DisplayDialog(
                    "Alakazam Portal",
                    "AlakazamController is already in this scene.",
                    "OK"
                );
                return;
            }

            // Create AlakazamController
            var alakazamGO = new GameObject("AlakazamController");
            Undo.RegisterCreatedObjectUndo(alakazamGO, "Add Alakazam Portal");
            alakazamGO.AddComponent<AlakazamController>();

            // Create ShowcaseUI
            var uiGO = new GameObject("AlakazamUI");
            Undo.RegisterCreatedObjectUndo(uiGO, "Add Alakazam UI");
            uiGO.AddComponent<Demo.ShowcaseUI>();

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            // Select the controller
            Selection.activeGameObject = alakazamGO;

            Debug.Log("[AlakazamPortal] Added to scene");

            EditorUtility.DisplayDialog(
                "Alakazam Portal",
                "Added to your scene!\n\n" +
                "Next steps:\n" +
                "1. Select 'AlakazamController' in Hierarchy\n" +
                "2. Configure your Server URL\n" +
                "3. Press Play and hit Space to start",
                "Got it!"
            );
        }

        [MenuItem("AlakazamPortal/Documentation")]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://docs.alakazam.io");
        }

        [MenuItem("AlakazamPortal/Support")]
        public static void OpenSupport()
        {
            Application.OpenURL("https://alakazam.io/support");
        }
    }
}
