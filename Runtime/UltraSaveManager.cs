using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace UltraSaveSystem
{
    /// <summary>
    ///     Sistema híbrido CORRIGIDO para Unity JsonUtility
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class UltraSaveManager : MonoBehaviour
    {
        [Header("🎮 Configurações Básicas")] [SerializeField]
        private bool _enableEncryption;

        [SerializeField] private bool _verboseLogging = true;
        [SerializeField] private int _maxParallelJobs = 8;
        [SerializeField] private string _playerName = "Player";

        // Cache de GameObjects normais
        private readonly Dictionary<string, SavedObjectData> _objectCache = new();
        private readonly Dictionary<Type, SaveableMetadata> _typeCache = new();

        public int TotalSaves { get; private set; }

        public int TotalLoads { get; private set; }

        public int ObjectCount => _objectCache.Count;
        public bool IsInitialized { get; private set; }

        public bool EncryptionEnabled => _enableEncryption;

        private async void Start()
        {
            await InitializeSystem();
        }

        // Propriedades públicas
        public void SetEnableEncryption(bool value)
        {
            _enableEncryption = value;
        }

        public void SetVerboseLogging(bool value)
        {
            _verboseLogging = value;
        }

        public void SetPlayerName(string name)
        {
            _playerName = name;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var existingManager = FindObjectOfType<UltraSaveManager>();
            if (existingManager == null)
            {
                var go = new GameObject("🎮 UltraSaveManager");
                go.AddComponent<UltraSaveManager>();
                DontDestroyOnLoad(go);
            }
        }

        private async Task InitializeSystem()
        {
            var startTime = Time.realtimeSinceStartup;

            if (_enableEncryption) UltraCrypto.Initialize();

            await BuildTypeCache();
            DiscoverSaveableObjects();

            IsInitialized = true;
            var initTime = (Time.realtimeSinceStartup - startTime) * 1000f;

            if (_verboseLogging)
            {
                Debug.Log($"🚀 UltraSaveManager inicializado em {initTime:F1}ms");
                Debug.Log($"📊 {_objectCache.Count} GameObjects saveáveis encontrados");
                Debug.Log($"🔐 Criptografia: {(_enableEncryption ? "ATIVADA" : "DESATIVADA")}");
                Debug.Log($"🎮 Player: {_playerName}");
                Debug.Log("💾 Save: APENAS MANUAL");
            }
        }

        private async Task BuildTypeCache()
        {
            await Task.Run(() =>
            {
                var saveableTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(assembly =>
                    {
                        try
                        {
                            return assembly.GetTypes();
                        }
                        catch (ReflectionTypeLoadException ex)
                        {
                            return ex.Types.Where(t => t != null);
                        }
                    })
                    .Where(type => type != null &&
                                   type.GetCustomAttribute<SaveableAttribute>() != null &&
                                   typeof(MonoBehaviour).IsAssignableFrom(type));

                foreach (var type in saveableTypes)
                    try
                    {
                        var attribute = type.GetCustomAttribute<SaveableAttribute>();
                        var fields = type
                            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            .Where(f => f.GetCustomAttribute<SaveFieldAttribute>() != null)
                            .ToArray();

                        _typeCache[type] = new SaveableMetadata
                        {
                            SaveKey = attribute.SaveKey ?? type.Name,
                            SaveTransform = attribute.SaveTransform,
                            AutoSave = attribute.AutoSave,
                            Priority = attribute.Priority,
                            SaveableFields = fields
                        };

                        if (_verboseLogging)
                            Debug.Log($"📝 Tipo registrado: {type.Name} ({fields.Length} campos saveáveis)");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"❌ Erro ao processar tipo {type.Name}: {ex.Message}");
                    }
            });
        }

        private void DiscoverSaveableObjects()
        {
            _objectCache.Clear();

            foreach (var kvp in _typeCache)
            {
                var type = kvp.Key;
                var metadata = kvp.Value;

                try
                {
                    var components = FindObjectsOfType(type).Cast<MonoBehaviour>().ToArray();

                    for (var i = 0; i < components.Length; i++)
                    {
                        var component = components[i];
                        if (component == null) continue;

                        var key = components.Length > 1 ? $"{metadata.SaveKey}_{i}" : metadata.SaveKey;

                        if (_objectCache.ContainsKey(key)) key = $"{metadata.SaveKey}_{component.GetInstanceID()}";

                        _objectCache[key] = new SavedObjectData
                        {
                            Instance = component,
                            Metadata = metadata,
                            GameObject = component.gameObject,
                            LastSaveTime = 0f
                        };

                        if (_verboseLogging) Debug.Log($"🎯 Objeto registrado: {key} ({component.name})");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ Erro ao descobrir objetos do tipo {type.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        ///     SALVA TODOS OS OBJETOS - VERSÃO CORRIGIDA PARA UNITY JSON
        /// </summary>
        public async Task<bool> SaveAll()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("⚠️ UltraSaveManager não foi inicializado ainda");
                return false;
            }

            var startTime = Time.realtimeSinceStartup;

            try
            {
                UltraSaveSystem.TriggerSaveStarted();

                if (_verboseLogging) Debug.Log($"💾 SAVE MANUAL iniciado - {_objectCache.Count} objetos");

                var saveDataList = CollectCurrentSaveDataMainThread();

                if (saveDataList.Count == 0)
                {
                    Debug.LogWarning("⚠️ Nenhum dado para salvar encontrado!");
                    return false;
                }

                var processedData = await ProcessSaveDataWithJobs(saveDataList);
                await WriteToFileSimple(processedData);

                TotalSaves++;
                var saveTime = (Time.realtimeSinceStartup - startTime) * 1000f;

                if (_verboseLogging)
                {
                    Debug.Log($"✅ SAVE MANUAL completo em {saveTime:F1}ms");
                    Debug.Log($"📊 {processedData.Count} objetos salvos");
                    Debug.Log($"📈 Total de saves: {TotalSaves}");
                }

                UltraSaveSystem.TriggerSaveCompleted();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Save falhou: {ex.Message}\n{ex.StackTrace}");
                UltraSaveSystem.TriggerSaveFailed();
                return false;
            }
        }

        /// <summary>
        ///     VERSÃO CORRIGIDA - Usa estrutura compatível com Unity JSON
        /// </summary>
        private List<SaveableData> CollectCurrentSaveDataMainThread()
        {
            var dataList = new List<SaveableData>();

            foreach (var kvp in _objectCache.ToList())
            {
                var key = kvp.Key;
                var objectData = kvp.Value;

                if (objectData.Instance == null || objectData.GameObject == null)
                {
                    if (_verboseLogging) Debug.LogWarning($"⚠️ GameObject {key} foi destruído");
                    continue;
                }

                var component = objectData.Instance as MonoBehaviour;
                var metadata = objectData.Metadata;

                var saveData = new SaveableData
                {
                    SaveKey = key,
                    Priority = metadata.Priority,
                    SaveTime = Time.time
                };

                // Callback pré-save
                if (component is ICustomSaveable customSaveable)
                    try
                    {
                        customSaveable.OnBeforeSave();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"⚠️ Erro no OnBeforeSave de {key}: {ex.Message}");
                    }

                // Salvar transform
                if (metadata.SaveTransform)
                {
                    var t = component.transform;
                    saveData.Position = t.position;
                    saveData.Rotation = t.rotation;
                    saveData.Scale = t.localScale;
                    saveData.HasTransform = true;

                    if (_verboseLogging)
                        Debug.Log(
                            $"🔄 Transform salvo para {key}: Pos={t.position}, Rot={t.rotation.eulerAngles}, Scale={t.localScale}");
                }

                // CORREÇÃO PRINCIPAL: Salvar campos usando o novo método
                foreach (var field in metadata.SaveableFields)
                    try
                    {
                        var value = field.GetValue(component);
                        var saveFieldAttr = field.GetCustomAttribute<SaveFieldAttribute>();

                        // Criptografar se necessário
                        if (saveFieldAttr?.Encrypted == true && _enableEncryption)
                        {
                            value = UltraCrypto.EncryptField(value);
                            if (_verboseLogging) Debug.Log($"🔐 Campo {field.Name} criptografado");
                        }

                        var fieldKey = saveFieldAttr?.CustomKey ?? field.Name;

                        // MUDANÇA: Usar novo método SetFieldData
                        saveData.SetFieldData(fieldKey, value);

                        if (_verboseLogging)
                            Debug.Log($"📝 Campo salvo: {fieldKey} = {value} ({field.FieldType.Name})");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"⚠️ Erro ao extrair campo {field.Name}: {ex.Message}");
                    }

                // Dados customizados
                if (component is ICustomSaveable customData)
                    try
                    {
                        var customBytes = customData.SerializeCustomData();
                        if (customBytes != null && customBytes.Length > 0)
                        {
                            saveData.CustomData = customBytes;
                            if (_verboseLogging)
                                Debug.Log($"📦 Dados customizados salvos para {key}: {customBytes.Length} bytes");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"⚠️ Erro ao serializar dados customizados: {ex.Message}");
                    }

                dataList.Add(saveData);

                if (_verboseLogging) Debug.Log($"✅ Objeto {key} coletado com {saveData.FieldKeys.Count} campos");
            }

            dataList.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            if (_verboseLogging) Debug.Log($"📋 Total coletado: {dataList.Count} objetos com dados");

            return dataList;
        }

        private async Task<List<SaveableData>> ProcessSaveDataWithJobs(List<SaveableData> dataList)
        {
            if (dataList.Count == 0) return dataList;

            var transformsToProcess = dataList.Where(d => d.HasTransform).ToList();
            if (transformsToProcess.Count == 0) return dataList;

            var inputArray = new NativeArray<TransformJobData>(transformsToProcess.Count, Allocator.TempJob);
            var outputArray = new NativeArray<TransformJobData>(transformsToProcess.Count, Allocator.TempJob);

            try
            {
                for (var i = 0; i < transformsToProcess.Count; i++)
                {
                    var data = transformsToProcess[i];
                    inputArray[i] = new TransformJobData
                    {
                        Position = data.Position,
                        Rotation = data.Rotation,
                        Scale = data.Scale
                    };
                }

                var job = new OptimizeTransformJob
                {
                    InputData = inputArray,
                    OutputData = outputArray
                };

                var jobHandle = job.Schedule(transformsToProcess.Count, _maxParallelJobs);

                while (!jobHandle.IsCompleted) await Task.Yield();
                jobHandle.Complete();

                for (var i = 0; i < transformsToProcess.Count; i++)
                {
                    var processed = outputArray[i];
                    var originalData = transformsToProcess[i];
                    originalData.Position = processed.Position;
                    originalData.Rotation = processed.Rotation;
                    originalData.Scale = processed.Scale;
                }

                if (_verboseLogging) Debug.Log($"⚡ {transformsToProcess.Count} transforms processados com Jobs");
            }
            finally
            {
                if (inputArray.IsCreated) inputArray.Dispose();
                if (outputArray.IsCreated) outputArray.Dispose();
            }

            return dataList;
        }

        /// <summary>
        ///     VERSÃO CORRIGIDA - Agora salva tudo corretamente
        /// </summary>
        private async Task WriteToFileSimple(List<SaveableData> data)
        {
            var fileExtension = _enableEncryption ? ".dat" : ".json";
            var fileName = "ultrasave" + fileExtension;
            var path = Path.Combine(Application.persistentDataPath, fileName);

            try
            {
                var saveFile = new UltraSaveFile
                {
                    Version = Application.version,
                    SaveTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
                    SaveTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ObjectCount = data.Count,
                    IsEncrypted = _enableEncryption,
                    PlayerName = _playerName,
                    DeviceInfo = SystemInfo.deviceModel,
                    Objects = data // CORREÇÃO: Agora é uma List, não Dictionary!
                };

                var json = JsonUtility.ToJson(saveFile, !_enableEncryption);

                if (_enableEncryption && !string.IsNullOrEmpty(json))
                {
                    UltraCrypto.Initialize();
                    var encryptedJson = await UltraCrypto.EncryptAsync(json);
                    await File.WriteAllTextAsync(path, encryptedJson);

                    if (_verboseLogging)
                    {
                        Debug.Log($"🔐 Save CRIPTOGRAFADO: {fileName}");
                        Debug.Log($"📄 Arquivo salvo em: {path}");
                    }
                }
                else
                {
                    await File.WriteAllTextAsync(path, json);

                    if (_verboseLogging)
                    {
                        Debug.Log($"📄 Save em texto: {fileName}");
                        Debug.Log($"📄 Arquivo salvo em: {path}");
                        Debug.Log($"📋 Conteúdo completo:\n{json}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao salvar: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     CARREGA - VERSÃO DEFINITIVAMENTE CORRIGIDA
        /// </summary>
        public async Task<bool> LoadAll()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("⚠️ UltraSaveManager não foi inicializado ainda");
                return false;
            }

            var startTime = Time.realtimeSinceStartup;

            try
            {
                UltraSaveSystem.TriggerLoadStarted();

                var encryptedPath = Path.Combine(Application.persistentDataPath, "ultrasave.dat");
                var normalPath = Path.Combine(Application.persistentDataPath, "ultrasave.json");

                string json = null;
                var isEncrypted = false;
                var loadedFrom = "";

                if (File.Exists(encryptedPath))
                {
                    var fileContent = await File.ReadAllTextAsync(encryptedPath);

                    if (_verboseLogging)
                        Debug.Log($"📁 Arquivo .dat encontrado. Criptografado: {UltraCrypto.IsEncrypted(fileContent)}");

                    // CORREÇÃO PRINCIPAL: Descriptografar SEMPRE se for arquivo .dat
                    if (UltraCrypto.IsEncrypted(fileContent))
                    {
                        UltraCrypto.Initialize();
                        json = await UltraCrypto.DecryptAsync(fileContent);
                        isEncrypted = true;

                        if (_verboseLogging) Debug.Log("🔓 Arquivo descriptografado com sucesso");
                    }
                    else
                    {
                        json = fileContent; // Tratar como texto normal
                        isEncrypted = false;
                    }

                    loadedFrom = "ultrasave.dat";
                }
                else if (File.Exists(normalPath))
                {
                    json = await File.ReadAllTextAsync(normalPath);
                    isEncrypted = false;
                    loadedFrom = "ultrasave.json";
                }
                else
                {
                    if (_verboseLogging) Debug.Log("ℹ️ Nenhum save encontrado - primeira execução");
                    return false;
                }

                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogError("❌ Save file vazio após descriptografia");
                    return false;
                }

                // Verificar se ainda está criptografado (erro na descriptografia)
                if (UltraCrypto.IsEncrypted(json))
                {
                    Debug.LogError("❌ JSON ainda está criptografado após descriptografia!");
                    Debug.LogError($"Conteúdo problemático: {json.Substring(0, Math.Min(200, json.Length))}...");
                    return false;
                }

                // Limpar JSON apenas se NÃO estiver criptografado
                json = UltraCrypto.CleanJson(json);

                if (_verboseLogging)
                    Debug.Log(
                        $"📖 JSON limpo e pronto para parse (primeiros 500 chars):\n{json.Substring(0, Math.Min(500, json.Length))}...");

                // Tentar parse do JSON
                UltraSaveFile saveFile = null;
                try
                {
                    saveFile = JsonUtility.FromJson<UltraSaveFile>(json);
                }
                catch (Exception parseEx)
                {
                    Debug.LogError($"❌ Erro no parse do JSON: {parseEx.Message}");

                    // Debug: Verificar se é problema de locale (vírgula vs ponto)
                    if (parseEx.Message.Contains("exponent") || parseEx.Message.Contains("number"))
                    {
                        Debug.LogError("🔍 Problema de formatação numérica detectado. Tentando correção...");
                        json = json.Replace(",", ".");

                        try
                        {
                            saveFile = JsonUtility.FromJson<UltraSaveFile>(json);
                            Debug.Log("✅ Correção de vírgula funcionou!");
                        }
                        catch (Exception secondEx)
                        {
                            Debug.LogError($"❌ Segunda tentativa falhou: {secondEx.Message}");
                            Debug.LogError($"JSON problemático: {json}");
                            return false;
                        }
                    }
                    else
                    {
                        Debug.LogError($"JSON problemático: {json}");
                        return false;
                    }
                }

                if (saveFile == null)
                {
                    Debug.LogError("❌ SaveFile é null após parse");
                    return false;
                }

                if (saveFile.Objects == null || saveFile.Objects.Count == 0)
                {
                    Debug.LogWarning("⚠️ Save file não contém objetos");
                    return false;
                }

                ApplyLoadedDataToGameObjectsMainThread(saveFile.Objects);

                TotalLoads++;
                var loadTime = (Time.realtimeSinceStartup - startTime) * 1000f;

                if (_verboseLogging)
                {
                    Debug.Log($"✅ LOAD MANUAL completo em {loadTime:F1}ms");
                    Debug.Log($"📁 Arquivo: {loadedFrom} (Criptografado: {isEncrypted})");
                    Debug.Log($"📊 {saveFile.ObjectCount} objetos carregados");
                    Debug.Log($"🎮 Player: {saveFile.PlayerName}");
                    Debug.Log($"📈 Total de loads: {TotalLoads}");
                }

                UltraSaveSystem.TriggerLoadCompleted();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Load falhou: {ex.Message}\n{ex.StackTrace}");
                UltraSaveSystem.TriggerLoadFailed();
                return false;
            }
        }

        /// <summary>
        ///     VERSÃO CORRIGIDA - Trabalha com List em vez de Dictionary
        /// </summary>
        private void ApplyLoadedDataToGameObjectsMainThread(List<SaveableData> loadedData)
        {
            var appliedCount = 0;

            foreach (var saveData in loadedData)
            {
                // Encontrar objeto pelo SaveKey
                var objectDataKvp = _objectCache.FirstOrDefault(kvp => kvp.Key == saveData.SaveKey);

                if (objectDataKvp.Key == null)
                {
                    if (_verboseLogging)
                        Debug.LogWarning($"⚠️ Objeto {saveData.SaveKey} não encontrado no cache atual");
                    continue;
                }

                var objectData = objectDataKvp.Value;

                if (objectData.Instance == null || objectData.GameObject == null) continue;

                ApplyDataToGameObject(objectData.Instance as MonoBehaviour, saveData, objectData.Metadata);
                appliedCount++;
            }

            if (_verboseLogging) Debug.Log($"🎯 Dados aplicados a {appliedCount} GameObjects");
        }

        /// <summary>
        ///     VERSÃO CORRIGIDA - Trabalha com nova estrutura de dados
        /// </summary>
        private void ApplyDataToGameObject(MonoBehaviour component, SaveableData saveData, SaveableMetadata metadata)
        {
            if (component == null) return;

            try
            {
                // Transform
                if (saveData.HasTransform && metadata.SaveTransform)
                {
                    var t = component.transform;
                    t.position = saveData.Position;
                    t.rotation = saveData.Rotation;
                    t.localScale = saveData.Scale;

                    if (_verboseLogging)
                        Debug.Log(
                            $"🔄 Transform aplicado: Pos={saveData.Position}, Rot={saveData.Rotation.eulerAngles}, Scale={saveData.Scale}");
                }

                // Campos
                foreach (var field in metadata.SaveableFields)
                {
                    var saveFieldAttr = field.GetCustomAttribute<SaveFieldAttribute>();
                    var fieldKey = saveFieldAttr?.CustomKey ?? field.Name;

                    if (saveData.HasFieldData(fieldKey))
                        try
                        {
                            var stringValue = saveData.GetFieldData(fieldKey);
                            object value = stringValue;

                            // Descriptografar se necessário
                            if (saveFieldAttr?.Encrypted == true && _enableEncryption)
                                if (UltraCrypto.IsEncrypted(stringValue))
                                {
                                    value = UltraCrypto.DecryptField(stringValue, field.GetValue(component));
                                    if (_verboseLogging) Debug.Log($"🔓 Campo {field.Name} descriptografado");
                                }

                            // Conversão de tipos
                            if (field.FieldType == typeof(float))
                                value = float.Parse(stringValue);
                            else if (field.FieldType == typeof(int))
                                value = int.Parse(stringValue);
                            else if (field.FieldType == typeof(bool))
                                value = bool.Parse(stringValue);
                            else if (field.FieldType != typeof(string))
                                value = Convert.ChangeType(stringValue, field.FieldType);

                            field.SetValue(component, value);

                            if (_verboseLogging) Debug.Log($"📝 Campo aplicado: {fieldKey} = {value}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"⚠️ Erro ao aplicar {field.Name}: {ex.Message}");
                        }
                    else if (_verboseLogging) Debug.LogWarning($"⚠️ Campo {fieldKey} não encontrado no save");
                }

                // Dados customizados
                if (saveData.CustomData != null && saveData.CustomData.Length > 0 &&
                    component is ICustomSaveable customSaveable)
                    try
                    {
                        customSaveable.DeserializeCustomData(saveData.CustomData);
                        if (_verboseLogging)
                            Debug.Log($"📦 Dados customizados aplicados: {saveData.CustomData.Length} bytes");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"⚠️ Erro nos dados customizados: {ex.Message}");
                    }

                // Callback pós-load
                if (component is ICustomSaveable postLoad)
                    try
                    {
                        postLoad.OnAfterLoad();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"⚠️ Erro no OnAfterLoad: {ex.Message}");
                    }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Erro ao aplicar dados: {ex.Message}");
            }
        }

        public void RefreshSaveableObjects()
        {
            DiscoverSaveableObjects();

            if (_verboseLogging) Debug.Log($"🔄 Lista atualizada: {_objectCache.Count} objetos");
        }

        public string GetDebugInfo()
        {
            var info = new StringBuilder();
            info.AppendLine("🎮 ULTRA SAVE MANAGER - MODO MANUAL");
            info.AppendLine($"📊 Status: {(IsInitialized ? "Inicializado" : "Não inicializado")}");
            info.AppendLine($"📁 Objetos: {_objectCache.Count}");
            info.AppendLine($"📈 Saves manuais: {TotalSaves}");
            info.AppendLine($"📉 Loads manuais: {TotalLoads}");
            info.AppendLine($"🔐 Criptografia: {(_enableEncryption ? "ATIVA" : "INATIVA")}");
            info.AppendLine($"🎮 Player: {_playerName}");
            info.AppendLine("💾 Auto-save: DESABILITADO");
            info.AppendLine("📦 Backup: DESABILITADO");

            return info.ToString();
        }
    }

    [BurstCompile]
    public struct OptimizeTransformJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TransformJobData> InputData;
        public NativeArray<TransformJobData> OutputData;

        public void Execute(int index)
        {
            var input = InputData[index];
            var output = input;

            output.Rotation = math.normalize(input.Rotation);
            output.Position = math.round(input.Position * 1000f) / 1000f;
            output.Scale = math.max(input.Scale, new float3(0.001f));

            OutputData[index] = output;
        }
    }
}