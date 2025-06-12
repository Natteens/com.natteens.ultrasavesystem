using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;

namespace UltraSaveSystem.Editor
{
    public static class UltraSaveSystemTools
    {
        [MenuItem("Tools/UltraSaveSystem/Create Config")]
        public static void CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<UltraSaveConfig>();
            
            var resourcesPath = "Assets/Resources";
            if (!Directory.Exists(resourcesPath))
                Directory.CreateDirectory(resourcesPath);
            
            var assetPath = Path.Combine(resourcesPath, "UltraSaveConfig.asset");
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            
            UnityEngine.Debug.Log("UltraSaveConfig created at " + assetPath);
        }
        
        [MenuItem("Tools/UltraSaveSystem/Open Save Folder")]
        public static void OpenSaveFolder()
        {
            var path = Path.Combine(Application.persistentDataPath, "UltraSaves");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            
            Process.Start(path);
        }
        
        [MenuItem("Tools/UltraSaveSystem/Clear All Saves")]
        public static void ClearAllSaves()
        {
            if (EditorUtility.DisplayDialog("Clear All Saves", 
                "This will delete all save files. Are you sure?", "Yes", "Cancel"))
            {
                var path = Path.Combine(Application.persistentDataPath, "UltraSaves");
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    Directory.CreateDirectory(path);
                    UnityEngine.Debug.Log("All saves cleared");
                }
            }
        }
        
        [MenuItem("Tools/UltraSaveSystem/Refresh System")]
        public static void RefreshSystem()
        {
            if (Application.isPlaying)
            {
                UnityEngine.Debug.Log("System refreshed - object count: " + UltraSaveManager.TrackedObjectCount);
            }
            else
            {
                UnityEngine.Debug.Log("System refresh is only available in play mode");
            }
        }
    }
}