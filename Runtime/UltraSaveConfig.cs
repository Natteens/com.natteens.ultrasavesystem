using System.Collections.Generic;
using UnityEngine;

namespace UltraSaveSystem
{
    [CreateAssetMenu(fileName = "UltraSaveConfig", menuName = "Ultra Save System/Config", order = 1)]
    public class UltraSaveConfig : ScriptableObject
    {
        [Header("Sistema")]
        public bool enableSystem = true;
        public bool enableEncryption;
        public bool enableVerboseLogging;
        
        [Header("Autosave")]
        public bool enableAutoSave;
        
        [SerializeField] private int _autoSaveMinutes = 5;
        
        [Header("Slots")]
        public int maxSaveSlots = 10;
        public int currentSlot = 1;
        
        [Header("Player")]
        public string playerName = "Player";
        
        [Header("Performance")]
        public int maxJobsPerFrame = 8;
        public bool useJobSystem = true;
        public bool enableCompression = true;
        
        [Header("Interface")]
        public bool showEditorDialogs;
        public bool showDebugInfo;
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
}