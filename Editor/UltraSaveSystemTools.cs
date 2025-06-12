using UnityEditor;
using UnityEngine;
using System.IO;

namespace UltraSaveSystem.Editor
{
    public static class UltraSaveMenuItems
    {
        [MenuItem("Tools/Ultra Save System/Create Config", false, 1)]
        public static void CreateConfig()
        {
            var resourcesPath = "Assets/Resources";
            var ultraSaveFolderPath = Path.Combine(resourcesPath, "UltraSave");
            
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            if (!AssetDatabase.IsValidFolder(ultraSaveFolderPath))
            {
                AssetDatabase.CreateFolder(resourcesPath, "UltraSave");
            }
            
            var configPath = Path.Combine(ultraSaveFolderPath, "UltraSaveConfig.asset");
            var existingConfig = AssetDatabase.LoadAssetAtPath<UltraSaveConfig>(configPath);
            
            if (existingConfig != null)
            {
                if (EditorUtility.DisplayDialog("Config Existente", 
                    "Já existe um UltraSaveConfig. Deseja substituí-lo?", "Sim", "Não"))
                {
                    AssetDatabase.DeleteAsset(configPath);
                }
                else
                {
                    Selection.activeObject = existingConfig;
                    EditorGUIUtility.PingObject(existingConfig);
                    return;
                }
            }
            
            var config = ScriptableObject.CreateInstance<UltraSaveConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            
            Debug.Log($"UltraSaveConfig criado em: {configPath}");
            EditorUtility.DisplayDialog("Config Criado", 
                "UltraSaveConfig criado com sucesso!\n\nLocalização: " + configPath, "OK");
        }
        
        [MenuItem("Tools/Ultra Save System/Open Save Folder", false, 2)]
        public static void OpenSaveFolder()
        {
            var path = Path.Combine(Application.persistentDataPath, "UltraSaves");
            
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            
            System.Diagnostics.Process.Start(path);
        }
        
        [MenuItem("Tools/Ultra Save System/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/Natteens/com.natteens.ultrasavesystem");
        }
    }
}