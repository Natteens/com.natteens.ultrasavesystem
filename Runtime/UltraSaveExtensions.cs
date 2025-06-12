using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Object = UnityEngine.Object;

namespace UltraSaveSystem
{
    public static class UltraSaveExtensions
    {
        private static readonly Dictionary<string, object> _saveableObjects = new();
        private static readonly Dictionary<string, SaveableMetadata> _objectMetadata = new();
        private static readonly Dictionary<Type, FieldInfo[]> _cachedFields = new();
        private static readonly Dictionary<string, SavedObjectData> _pendingLoadData = new();
        
        public static void RegisterForSave(this object obj, string customKey = null)
        {
            if (obj == null) return;
            
            var key = customKey ?? GenerateSaveKey(obj);
            _saveableObjects[key] = obj;
            
            var metadata = new SaveableMetadata
            {
                saveKey = key,
                objectType = obj.GetType(),
                hasTransform = obj is Component,
                saveableFields = GetSaveableFields(obj.GetType())
            };
            
            _objectMetadata[key] = metadata;
            
            if (_pendingLoadData.TryGetValue(key, out var pendingData))
            {
                obj.DeserializeObject(pendingData);
                _pendingLoadData.Remove(key);
                
                if (UltraSaveManager.Config?.enableVerboseLogging == true)
                    Debug.Log($"Applied pending load data to: {key}");
            }
            
            if (UltraSaveManager.Config?.enableVerboseLogging == true)
                Debug.Log($"Registered object for save: {key}");
        }
        
        public static void UnregisterFromSave(this object obj, string customKey = null)
        {
            var key = customKey ?? GenerateSaveKey(obj);
            _saveableObjects.Remove(key);
            _objectMetadata.Remove(key);
            
            if (UltraSaveManager.Config?.enableVerboseLogging == true)
                Debug.Log($"Unregistered object from save: {key}");
        }
        
        public static SavedObjectData SerializeObject(this object obj)
        {
            if (obj == null) return null;
            
            var key = GenerateSaveKey(obj);
            var data = new SavedObjectData { saveKey = key };
            
            if (_objectMetadata.TryGetValue(key, out var metadata))
            {
                data.fieldData = new Dictionary<string, object>();
                
                foreach (var field in metadata.saveableFields)
                {
                    try
                    {
                        var value = field.GetValue(obj);
                        if (value != null)
                        {
                            var attr = field.GetCustomAttribute<SaveFieldAttribute>();
                            var fieldKey = attr?.CustomKey ?? field.Name;
                            data.fieldData[fieldKey] = value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to serialize field {field.Name}: {ex.Message}");
                    }
                }
                
                if (obj is Component comp && metadata.hasTransform)
                {
                    data.hasTransform = true;
                    data.position = comp.transform.position;
                    data.rotation = comp.transform.rotation;
                    data.scale = comp.transform.localScale;
                }
                
                if (obj is ICustomSaveable customSaveable)
                {
                    try
                    {
                        customSaveable.OnBeforeSave();
                        data.customData = customSaveable.SerializeCustomData();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Custom save failed for {key}: {ex.Message}");
                    }
                }
            }
            
            return data;
        }
        
        public static void DeserializeObject(this object obj, SavedObjectData data)
        {
            if (obj == null || data?.fieldData == null) return;
            
            var key = GenerateSaveKey(obj);
            
            if (_objectMetadata.TryGetValue(key, out var metadata))
            {
                foreach (var field in metadata.saveableFields)
                {
                    try
                    {
                        var attr = field.GetCustomAttribute<SaveFieldAttribute>();
                        var fieldKey = attr?.CustomKey ?? field.Name;
                        
                        if (data.fieldData.TryGetValue(fieldKey, out var value))
                        {
                            var convertedValue = ConvertValue(value, field.FieldType);
                            field.SetValue(obj, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to deserialize field {field.Name}: {ex.Message}");
                    }
                }
                
                if (obj is Component comp && data.hasTransform)
                {
                    try
                    {
                        comp.transform.position = data.position;
                        comp.transform.rotation = data.rotation;
                        comp.transform.localScale = data.scale;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to restore transform for {key}: {ex.Message}");
                    }
                }
                
                if (obj is ICustomSaveable customSaveable && data.customData != null)
                {
                    try
                    {
                        customSaveable.DeserializeCustomData(data.customData);
                        customSaveable.OnAfterLoad();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Custom load failed for {key}: {ex.Message}");
                    }
                }
                
                CallOnAfterLoadMethod(obj);
            }
        }
        
        public static void AutoRegisterSaveableFields(this MonoBehaviour obj)
        {
            obj.RegisterForSave();
        }
        
        public static void AutoRegisterSaveableFields(this ScriptableObject obj)
        {
            obj.RegisterForSave();
        }
        
        public static void AutoRegisterSaveableFields(this object obj)
        {
            obj.RegisterForSave();
        }
        
        internal static Dictionary<string, object> GetAllSaveableObjects()
        {
            return new Dictionary<string, object>(_saveableObjects);
        }
        
        internal static SaveableMetadata GetMetadata(string key)
        {
            return _objectMetadata.TryGetValue(key, out var metadata) ? metadata : default;
        }
        
        internal static void StorePendingLoadData(string key, SavedObjectData data)
        {
            _pendingLoadData[key] = data;
        }
        
        internal static void ClearPendingLoadData()
        {
            _pendingLoadData.Clear();
        }
        
        private static void CallOnAfterLoadMethod(object obj)
        {
            try
            {
                var method = obj.GetType().GetMethod("OnAfterLoad", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                method?.Invoke(obj, null);
                
                var applyMethod = obj.GetType().GetMethod("ApplyVisualSettings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                applyMethod?.Invoke(obj, null);
                
                var refreshMethod = obj.GetType().GetMethod("RefreshVisuals", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                refreshMethod?.Invoke(obj, null);
                
                var updateMethod = obj.GetType().GetMethod("UpdateVisuals", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                updateMethod?.Invoke(obj, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to call post-load methods: {ex.Message}");
            }
        }
        
        private static string GenerateSaveKey(object obj)
        {
            if (obj == null) return "";
            
            var type = obj.GetType();
            var attr = type.GetCustomAttribute<SaveableAttribute>();
            
            if (attr != null && !string.IsNullOrEmpty(attr.SaveKey))
                return attr.SaveKey;
            
            if (obj is Object unityObj)
                return $"{type.Name}_{unityObj.GetInstanceID()}";
            
            return $"{type.Name}_{obj.GetHashCode()}";
        }
        
        private static FieldInfo[] GetSaveableFields(Type type)
        {
            if (_cachedFields.TryGetValue(type, out var cached))
                return cached;
            
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<SaveFieldAttribute>() != null)
                .ToArray();
            
            _cachedFields[type] = fields;
            return fields;
        }
        
        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null || targetType.IsAssignableFrom(value.GetType()))
                return value;
            
            try
            {
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, value.ToString());
                
                if (targetType == typeof(Vector3) && value is JObject jVector3)
                {
                    return new Vector3(
                        jVector3["x"]?.ToObject<float>() ?? 0f,
                        jVector3["y"]?.ToObject<float>() ?? 0f,
                        jVector3["z"]?.ToObject<float>() ?? 0f
                    );
                }
                
                if (targetType == typeof(Quaternion) && value is JObject jQuaternion)
                {
                    return new Quaternion(
                        jQuaternion["x"]?.ToObject<float>() ?? 0f,
                        jQuaternion["y"]?.ToObject<float>() ?? 0f,
                        jQuaternion["z"]?.ToObject<float>() ?? 0f,
                        jQuaternion["w"]?.ToObject<float>() ?? 1f
                    );
                }
                
                if (targetType == typeof(Color) && value is JObject jColor)
                {
                    return new Color(
                        jColor["r"]?.ToObject<float>() ?? 0f,
                        jColor["g"]?.ToObject<float>() ?? 0f,
                        jColor["b"]?.ToObject<float>() ?? 0f,
                        jColor["a"]?.ToObject<float>() ?? 1f
                    );
                }
                
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return Activator.CreateInstance(targetType);
            }
        }
    }
    
    public struct SaveableMetadata
    {
        public string saveKey;
        public Type objectType;
        public bool hasTransform;
        public FieldInfo[] saveableFields;
    }
}