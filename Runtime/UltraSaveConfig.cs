using System;
using System.Collections.Generic;
using UnityEngine;

namespace UltraSaveSystem
{
    public class UltraSaveConfig : ScriptableObject
    {
        [Header("Sistema")]
        public bool enableSystem = true;
        public bool enableEncryption = false;
        public bool enableVerboseLogging = true;
        
        [Header("Autosave")]
        public bool enableAutoSave = false;
        
        [SerializeField] private int _autoSaveMinutes = 5;
        
        [Header("Slots")]
        public int maxSaveSlots = 10;
        public int currentSlot = 0;
        
        [Header("Player")]
        public string playerName = "Player";
        
        [Header("Performance")]
        public int maxJobsPerFrame = 8;
        public bool useJobSystem = true;
        
        [Header("Debug")]
        public bool showDebugInfo = false;
        public bool logSaveOperations = true;
        
        [SerializeField] private List<SavedObjectInfo> _trackedObjects = new List<SavedObjectInfo>();
        
        public List<SavedObjectInfo> TrackedObjects => _trackedObjects;
        
        public int AutoSaveMinutes
        {
            get => _autoSaveMinutes;
            set => _autoSaveMinutes = Mathf.Max(1, value);
        }
        
        public float autoSaveInterval => _autoSaveMinutes * 60f;
        
        public void AddTrackedObject(SavedObjectInfo info)
        {
            var existing = _trackedObjects.Find(x => x.saveKey == info.saveKey);
            if (existing != null)
                _trackedObjects.Remove(existing);
            
            _trackedObjects.Add(info);
        }
        
        public void RemoveTrackedObject(string saveKey)
        {
            _trackedObjects.RemoveAll(x => x.saveKey == saveKey);
        }
        
        public void ClearTrackedObjects()
        {
            _trackedObjects.Clear();
        }
    }
    
    [Serializable]
    public class SavedObjectInfo
    {
        public string saveKey;
        public string typeName;
        public string sceneName;
        public Vector3 position;
        public bool hasTransform;
        public DateTime lastSaved;
        public int fieldCount;
        public long dataSize;
        
        public SavedObjectInfo(string key, string type, string scene)
        {
            saveKey = key;
            typeName = type;
            sceneName = scene;
            lastSaved = DateTime.Now;
        }
    }
}