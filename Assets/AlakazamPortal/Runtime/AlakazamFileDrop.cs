using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlakazamPortal
{
    /// <summary>
    /// Handles file drag & drop onto the Unity window.
    /// Works automatically in the Editor. For builds, requires platform-specific integration.
    /// </summary>
    public class AlakazamFileDrop : MonoBehaviour
    {
        public event Action<string[]> OnFilesDropped;

        private static AlakazamFileDrop _instance;
        public static AlakazamFileDrop Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("AlakazamFileDrop");
                    _instance = go.AddComponent<AlakazamFileDrop>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            // Register for drag & drop events in Editor
            UnityEditor.DragAndDrop.AddDropHandler(HandleEditorDrop);
        }

        private void OnDisable()
        {
            UnityEditor.DragAndDrop.RemoveDropHandler(HandleEditorDrop);
        }

        private UnityEditor.DragAndDropVisualMode HandleEditorDrop(int dragInstanceId, string dropUponPath, bool perform)
        {
            // Only handle when dropping on Game view (dropUponPath will be empty for scene drops)
            var paths = UnityEditor.DragAndDrop.paths;

            if (paths == null || paths.Length == 0)
                return UnityEditor.DragAndDropVisualMode.None;

            // Filter for image files
            var imagePaths = new List<string>();
            foreach (var path in paths)
            {
                string ext = System.IO.Path.GetExtension(path).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
                {
                    imagePaths.Add(path);
                }
            }

            if (imagePaths.Count == 0)
                return UnityEditor.DragAndDropVisualMode.Rejected;

            if (perform)
            {
                Debug.Log($"[AlakazamFileDrop] Files dropped: {string.Join(", ", imagePaths)}");
                OnFilesDropped?.Invoke(imagePaths.ToArray());
            }

            return UnityEditor.DragAndDropVisualMode.Copy;
        }
#endif

        /// <summary>
        /// Manually trigger a file drop (useful for testing or custom integrations).
        /// </summary>
        public void TriggerFileDrop(string[] paths)
        {
            OnFilesDropped?.Invoke(paths);
        }
    }
}
