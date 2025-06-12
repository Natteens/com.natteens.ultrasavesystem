using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Object = UnityEngine.Object;

namespace UltraSaveSystem
{
    public static class UltraSaveManager
    {
        private static UltraSaveConfig _config;
        private static string _saveDirectoryPath;
        private static float _lastAutoSave;
        private static bool _isInitialized;
        private static int _currentJobCount;
        private static MonoBehaviour _updateRunner;
        
        public static event Action<int> OnSaveStarted;
        public static event Action<int, bool> OnSaveCompleted;
        public static event Action<int> OnLoadStarted;
        public static event Action<int, bool> OnLoadCompleted;
        public static event Action<string> OnObjectSaved;
        public static event Action<string> OnObjectLoaded;
        
        public static bool IsInitialized => _isInitialized;
        public static UltraSaveConfig Config => _config;
        public static string SaveDirectory => _saveDirectoryPath;
        public static int TrackedObjectCount => UltraSaveExtensions.GetAllSaveableObjects().Count;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _config = Resources.Load<UltraSaveConfig>("UltraSaveConfig");
            if (_config == null)
            {
                Debug.LogError("UltraSaveConfig not found in Resources folder");
                return;
            }
            
            if (!_config.enableSystem)
            {
                Debug.Log("UltraSave System is disabled");
                return;
            }
            
            _saveDirectoryPath = Path.Combine(Application.persistentDataPath, "UltraSaves");
            if (!Directory.Exists(_saveDirectoryPath))
                Directory.CreateDirectory(_saveDirectoryPath);
            
            if (_config.enableEncryption)
                UltraCrypto.Initialize();
            
            CreateUpdateRunner();
            
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting += OnApplicationQuitting;
            
            DelayedAutoDiscover();
            
            _isInitialized = true;
            
            if (_config.enableVerboseLogging)
                Debug.Log($"UltraSaveManager initialized");
        }
        
        private static async void DelayedAutoDiscover()
        {
            await Task.Delay(100);
            AutoDiscoverAndRegisterObjects();
        }
        
        private static void CreateUpdateRunner()
        {
            var go = new GameObject("UltraSaveUpdateRunner");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _updateRunner = go.AddComponent<UltraSaveUpdateRunner>();
        }
        
        internal static void Update()
        {
            if (!_isInitialized || !_config.enableAutoSave) return;
            
            if (Time.realtimeSinceStartup - _lastAutoSave >= _config.autoSaveInterval)
            {
                _ = SaveAsync(_config.currentSlot, true);
                _lastAutoSave = Time.realtimeSinceStartup;
            }
        }
        
        public static async Task<bool> SaveAsync(int slot = 0, bool isAutoSave = false)
        {
            if (!_isInitialized || slot < 0 || slot >= _config.maxSaveSlots)
                return false;
            
            OnSaveStarted?.Invoke(slot);
            
            try
            {
                var saveData = new UltraSaveFile
                {
                    version = "2.0",
                    slot = slot,
                    isAutoSave = isAutoSave,
                    playerName = _config.playerName,
                    sceneName = SceneManager.GetActiveScene().name,
                    saveTime = DateTime.Now,
                    isEncrypted = _config.enableEncryption,
                    objects = new List<SavedObjectData>()
                };
                
                var saveableObjects = UltraSaveExtensions.GetAllSaveableObjects();
                
                foreach (var kvp in saveableObjects)
                {
                    try
                    {
                        var objectData = kvp.Value.SerializeObject();
                        if (objectData != null)
                        {
                            saveData.objects.Add(objectData);
                            OnObjectSaved?.Invoke(kvp.Key);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to save object {kvp.Key}: {ex.Message}");
                    }
                }
                
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Formatting = Formatting.Indented,
                    ContractResolver = new UnityContractResolver()
                };
                
                var json = JsonConvert.SerializeObject(saveData, settings);
                
                if (_config.enableEncryption)
                    json = await UltraCrypto.EncryptAsync(json);
                
                var filePath = GetSaveFilePath(slot);
                await File.WriteAllTextAsync(filePath, json);
                
                UpdateConfigTrackedObjects(saveData);
                
                OnSaveCompleted?.Invoke(slot, true);
                
                if (_config.logSaveOperations)
                    Debug.Log($"Save completed for slot {slot} with {saveData.objects.Count} objects");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save failed for slot {slot}: {ex.Message}");
                OnSaveCompleted?.Invoke(slot, false);
                return false;
            }
        }
        
        public static async Task<bool> LoadAsync(int slot = 0)
        {
            if (!_isInitialized || slot < 0 || slot >= _config.maxSaveSlots)
                return false;
            
            var filePath = GetSaveFilePath(slot);
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"Save file not found for slot {slot}");
                return false;
            }
            
            OnLoadStarted?.Invoke(slot);
            
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                
                if (_config.enableEncryption)
                    json = await UltraCrypto.DecryptAsync(json);
                
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    ContractResolver = new UnityContractResolver()
                };
                
                var saveData = JsonConvert.DeserializeObject<UltraSaveFile>(json, settings);
                
                if (saveData == null)
                {
                    Debug.LogError($"Failed to deserialize save data for slot {slot}");
                    OnLoadCompleted?.Invoke(slot, false);
                    return false;
                }
                
                UltraSaveExtensions.ClearPendingLoadData();
                
                var saveableObjects = UltraSaveExtensions.GetAllSaveableObjects();
                var processedObjects = new HashSet<string>();
                
                foreach (var objectData in saveData.objects)
                {
                    try
                    {
                        if (saveableObjects.TryGetValue(objectData.saveKey, out var obj))
                        {
                            obj.DeserializeObject(objectData);
                            OnObjectLoaded?.Invoke(objectData.saveKey);
                            processedObjects.Add(objectData.saveKey);
                        }
                        else
                        {
                            UltraSaveExtensions.StorePendingLoadData(objectData.saveKey, objectData);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to load object {objectData.saveKey}: {ex.Message}");
                    }
                }
                
                if (processedObjects.Count < saveData.objects.Count)
                {
                    AutoDiscoverAndRegisterObjects();
                    
                    await Task.Delay(50);
                    
                    var newSaveableObjects = UltraSaveExtensions.GetAllSaveableObjects();
                    foreach (var objectData in saveData.objects)
                    {
                        if (!processedObjects.Contains(objectData.saveKey) && 
                            newSaveableObjects.TryGetValue(objectData.saveKey, out var obj))
                        {
                            try
                            {
                                obj.DeserializeObject(objectData);
                                OnObjectLoaded?.Invoke(objectData.saveKey);
                                processedObjects.Add(objectData.saveKey);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Failed to load object {objectData.saveKey} on retry: {ex.Message}");
                            }
                        }
                    }
                }
                
                _config.playerName = saveData.playerName;
                
                OnLoadCompleted?.Invoke(slot, true);
                
                if (_config.logSaveOperations)
                    Debug.Log($"Load completed for slot {slot} - processed {processedObjects.Count}/{saveData.objects.Count} objects");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Load failed for slot {slot}: {ex.Message}");
                OnLoadCompleted?.Invoke(slot, false);
                return false;
            }
        }
        
        public static bool HasSave(int slot)
        {
            return File.Exists(GetSaveFilePath(slot));
        }
        
        public static bool DeleteSave(int slot)
        {
            var filePath = GetSaveFilePath(slot);
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to delete save slot {slot}: {ex.Message}");
                }
            }
            return false;
        }
        
        public static SaveSlotInfo GetSaveSlotInfo(int slot)
        {
            var filePath = GetSaveFilePath(slot);
            if (!File.Exists(filePath))
                return null;
            
            try
            {
                var fileInfo = new FileInfo(filePath);
                return new SaveSlotInfo
                {
                    slot = slot,
                    exists = true,
                    lastModified = fileInfo.LastWriteTime,
                    sizeBytes = fileInfo.Length
                };
            }
            catch
            {
                return null;
            }
        }
        
        public static void ForceRefreshObjects()
        {
            AutoDiscoverAndRegisterObjects();
        }
        
        private static void AutoDiscoverAndRegisterObjects()
        {
            var monoBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            
            foreach (var mb in monoBehaviours)
            {
                var type = mb.GetType();
                var saveableAttr = type.GetCustomAttribute<SaveableAttribute>();
                var hasFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Any(f => f.GetCustomAttribute<SaveFieldAttribute>() != null);
                
                if (saveableAttr != null || hasFields)
                {
                    mb.RegisterForSave();
                }
            }
            
            var scriptableObjects = Resources.LoadAll<ScriptableObject>("");
            foreach (var so in scriptableObjects)
            {
                if (so == _config) continue;
                
                var type = so.GetType();
                var saveableAttr = type.GetCustomAttribute<SaveableAttribute>();
                var hasFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Any(f => f.GetCustomAttribute<SaveFieldAttribute>() != null);
                
                if (saveableAttr != null || hasFields)
                {
                    so.RegisterForSave();
                }
            }
        }
        
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DelayedAutoDiscover();
        }
        
        private static void OnApplicationQuitting()
        {
            if (_config?.enableAutoSave == true)
            {
                _ = SaveAsync(_config.currentSlot, true);
            }
        }
        
        private static string GetSaveFilePath(int slot)
        {
            return Path.Combine(_saveDirectoryPath, $"save_slot_{slot:D2}.json");
        }
        
        private static void UpdateConfigTrackedObjects(UltraSaveFile saveData)
        {
            _config?.ClearTrackedObjects();
            
            foreach (var obj in saveData.objects)
            {
                var info = new SavedObjectInfo(obj.saveKey, "Unknown", saveData.sceneName)
                {
                    position = obj.hasTransform ? obj.position : Vector3.zero,
                    hasTransform = obj.hasTransform,
                    fieldCount = obj.fieldData?.Count ?? 0,
                    dataSize = obj.customData?.Length ?? 0
                };
                
                _config?.AddTrackedObject(info);
            }
        }
    }
    
    public class UnityContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, Newtonsoft.Json.MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            
            if (property.PropertyType == typeof(Vector3) && (property.PropertyName == "normalized" || property.PropertyName == "magnitude"))
            {
                property.ShouldSerialize = instance => false;
            }
            
            if (property.PropertyType == typeof(Quaternion) && (property.PropertyName == "normalized"))
            {
                property.ShouldSerialize = instance => false;
            }
            
            return property;
        }
    }
    
    internal class UltraSaveUpdateRunner : MonoBehaviour
    {
        private void Update()
        {
            UltraSaveManager.Update();
        }
    }
    
    [Serializable]
    public class SaveSlotInfo
    {
        public int slot;
        public bool exists;
        public DateTime lastModified;
        public long sizeBytes;
    }
}