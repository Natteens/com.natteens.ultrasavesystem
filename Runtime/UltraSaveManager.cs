using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
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
        public static int CurrentSlot => _config?.currentSlot ?? 0;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _config = Resources.Load<UltraSaveConfig>("UltraSave/UltraSaveConfig");
            
            if (_config == null)
            {
                _config = Resources.Load<UltraSaveConfig>("UltraSaveConfig");
            }
            
            if (_config == null)
            {
                Debug.LogWarning("Ultra Save System: Configuração não encontrada!\n" +
                               "Vá em Tools → Ultra Save System → Create Config para corrigir.");
                return;
            }
            
            if (!_config.enableSystem)
            {
                if (_config.enableVerboseLogging)
                    Debug.Log("Ultra Save System está desabilitado nas configurações.");
                return;
            }
            
            ValidateSlotConfiguration();
            
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
                Debug.Log($"Ultra Save System inicializado - Slot ativo: Slot {_config.currentSlot + 1} | " +
                         $"Compressão: {(_config.enableCompression ? "Ativada" : "Desativada")}");
        }
        
        private static void ValidateSlotConfiguration()
        {
            if (_config.maxSaveSlots < 1)
                _config.maxSaveSlots = 10;
            
            if (_config.currentSlot < 0 || _config.currentSlot >= _config.maxSaveSlots)
                _config.currentSlot = 0;
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
        
        public static async Task<bool> SaveAsync(int slot = -1, bool isAutoSave = false)
        {
            if (!_isInitialized)
            {
                Debug.LogError("Ultra Save System não está inicializado!");
                return false;
            }
            
            if (slot == -1)
                slot = _config.currentSlot;
            
            if (slot < 0 || slot >= _config.maxSaveSlots)
            {
                Debug.LogError($"Slot inválido: {slot}. Deve estar entre 0 e {_config.maxSaveSlots - 1}");
                return false;
            }
            
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
                    isCompressed = _config.enableCompression,
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
                        Debug.LogError($"Falha ao salvar objeto {kvp.Key}: {ex.Message}");
                    }
                }
                
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Formatting = _config.enableCompression ? Formatting.None : Formatting.Indented,
                    ContractResolver = new UnityContractResolver()
                };
                
                var json = JsonConvert.SerializeObject(saveData, settings);
                var originalSize = Encoding.UTF8.GetBytes(json).Length;
                
                byte[] finalData;
                
                if (_config.enableCompression)
                {
                    finalData = await CompressStringAsync(json);
                }
                else
                {
                    finalData = Encoding.UTF8.GetBytes(json);
                }
                
                if (_config.enableEncryption)
                {
                    try
                    {
                        finalData = await UltraCrypto.EncryptBytesAsync(finalData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Erro na criptografia do Slot {slot + 1}: {ex.Message}");
                        OnSaveCompleted?.Invoke(slot, false);
                        return false;
                    }
                }
                
                var filePath = GetSaveFilePath(slot);
                await File.WriteAllBytesAsync(filePath, finalData);
                
                UpdateConfigTrackedObjects(saveData);
                
                OnSaveCompleted?.Invoke(slot, true);
                
                var saveType = isAutoSave ? "Autosave" : "Save manual";
                if (_config.logSaveOperations)
                {
                    var compressionInfo = "";
                    if (_config.enableCompression)
                    {
                        var compressionRatio = (1f - (float)finalData.Length / originalSize) * 100f;
                        compressionInfo = $" (Compressão: {compressionRatio:F1}%)";
                    }
                    
                    Debug.Log($"{saveType} concluído no Slot {slot + 1} com {saveData.objects.Count} objetos " +
                             $"- {FormatBytes(finalData.Length)}{compressionInfo}");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Falha ao salvar no Slot {slot + 1}: {ex.Message}");
                OnSaveCompleted?.Invoke(slot, false);
                return false;
            }
        }
        
        public static async Task<bool> LoadAsync(int slot = -1)
        {
            if (!_isInitialized)
            {
                Debug.LogError("Ultra Save System não está inicializado!");
                return false;
            }
            
            if (slot == -1)
                slot = _config.currentSlot;
            
            if (slot < 0 || slot >= _config.maxSaveSlots)
            {
                Debug.LogError($"Slot inválido: {slot}");
                return false;
            }
            
            var filePath = GetSaveFilePath(slot);
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"Arquivo de save não encontrado para o Slot {slot + 1}");
                return false;
            }
            
            OnLoadStarted?.Invoke(slot);
            
            try
            {
                var fileData = await File.ReadAllBytesAsync(filePath);
                
                if (fileData == null || fileData.Length == 0)
                {
                    Debug.LogError($"Arquivo de save do Slot {slot + 1} está vazio ou corrompido");
                    OnLoadCompleted?.Invoke(slot, false);
                    return false;
                }
                
                if (_config.enableEncryption)
                {
                    try
                    {
                        fileData = await UltraCrypto.DecryptBytesAsync(fileData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Erro na descriptografia do Slot {slot + 1}: {ex.Message}");
                        
                        if (_config.enableVerboseLogging)
                        {
                            Debug.LogWarning($"Tentando carregar Slot {slot + 1} sem descriptografia (compatibilidade)");
                        }
                        
                        fileData = await File.ReadAllBytesAsync(filePath);
                    }
                }
                
                string json;
                
                try
                {
                    json = await DecompressStringAsync(fileData);
                }
                catch
                {
                    try
                    {
                        json = Encoding.UTF8.GetString(fileData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Falha ao processar dados do Slot {slot + 1}: {ex.Message}");
                        OnLoadCompleted?.Invoke(slot, false);
                        return false;
                    }
                }
                
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
                    Debug.LogError($"Falha ao deserializar dados do Slot {slot + 1}");
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
                        Debug.LogError($"Falha ao carregar objeto {objectData.saveKey}: {ex.Message}");
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
                                Debug.LogError($"Falha ao carregar objeto {objectData.saveKey} na segunda tentativa: {ex.Message}");
                            }
                        }
                    }
                }
                
                _config.playerName = saveData.playerName;
                
                OnLoadCompleted?.Invoke(slot, true);
                
                if (_config.logSaveOperations)
                {
                    var compressionInfo = saveData.isCompressed ? " (comprimido)" : "";
                    Debug.Log($"Load concluído do Slot {slot + 1}{compressionInfo} - " +
                             $"processados {processedObjects.Count}/{saveData.objects.Count} objetos");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Falha ao carregar do Slot {slot + 1}: {ex.Message}");
                OnLoadCompleted?.Invoke(slot, false);
                return false;
            }
        }
        
        private static async Task<byte[]> CompressStringAsync(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress))
                {
                    await gzip.WriteAsync(bytes, 0, bytes.Length);
                }
                return output.ToArray();
            }
        }
        
        private static async Task<string> DecompressStringAsync(byte[] compressedData)
        {
            using (var input = new MemoryStream(compressedData))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                await gzip.CopyToAsync(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }
        
        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes / (1024f * 1024f):F1} MB";
        }
        
        public static bool HasSave(int slot)
        {
            if (slot < 0 || slot >= _config.maxSaveSlots)
                return false;
                
            return File.Exists(GetSaveFilePath(slot));
        }
        
        public static bool DeleteSave(int slot)
        {
            if (slot < 0 || slot >= _config.maxSaveSlots)
            {
                Debug.LogError($"Slot inválido: {slot}");
                return false;
            }
            
            var filePath = GetSaveFilePath(slot);
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    if (_config.logSaveOperations)
                        Debug.Log($"Save do Slot {slot + 1} deletado com sucesso");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Falha ao deletar save do Slot {slot + 1}: {ex.Message}");
                }
            }
            return false;
        }
        
        public static SaveSlotInfo GetSaveSlotInfo(int slot)
        {
            if (slot < 0 || slot >= _config.maxSaveSlots)
                return null;
                
            var filePath = GetSaveFilePath(slot);
            if (!File.Exists(filePath))
                return new SaveSlotInfo { slot = slot, exists = false };
            
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
        
        public static List<SaveSlotInfo> GetAllSlotInfos()
        {
            var slots = new List<SaveSlotInfo>();
            
            for (int i = 0; i < _config.maxSaveSlots; i++)
            {
                slots.Add(GetSaveSlotInfo(i) ?? new SaveSlotInfo { slot = i, exists = false });
            }
            
            return slots;
        }
        
        public static void SetCurrentSlot(int slot)
        {
            if (slot < 0 || slot >= _config.maxSaveSlots)
            {
                Debug.LogError($"Slot inválido: {slot}. Deve estar entre 0 e {_config.maxSaveSlots - 1}");
                return;
            }
            
            _config.currentSlot = slot;
            
            if (_config.enableVerboseLogging)
                Debug.Log($"Slot atual alterado para: Slot {slot + 1}");
        }
        
        public static void ForceRefreshObjects()
        {
            AutoDiscoverAndRegisterObjects();
            
            if (_config.enableVerboseLogging)
                Debug.Log($"Refresh forçado - {TrackedObjectCount} objetos encontrados");
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
            return Path.Combine(_saveDirectoryPath, $"save_slot_{slot + 1:D2}.usav");
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
}