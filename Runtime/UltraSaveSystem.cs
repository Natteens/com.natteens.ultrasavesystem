using System;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UltraSaveSystem
{
    /// <summary>
    ///     API pública ultra simples para save/load
    /// </summary>
    public static class UltraSaveSystem
    {
        // Eventos públicos
        public static event Action OnSaveStarted;
        public static event Action OnSaveCompleted;
        public static event Action OnSaveFailed;
        public static event Action OnLoadStarted;
        public static event Action OnLoadCompleted;
        public static event Action OnLoadFailed;

        /// <summary>
        ///     Salva todos os objetos marcados com [Saveable]
        /// </summary>
        public static async Task<bool> Save()
        {
            var manager = GetManager();
            return manager != null && await manager.SaveAll();
        }

        /// <summary>
        ///     Carrega todos os objetos salvos
        /// </summary>
        public static async Task<bool> Load()
        {
            var manager = GetManager();
            return manager != null && await manager.LoadAll();
        }

        /// <summary>
        ///     Ativa/desativa criptografia
        /// </summary>
        public static void EnableEncryption(bool enable)
        {
            GetManager()?.SetEnableEncryption(enable);
        }

        /// <summary>
        ///     Verifica se criptografia está ativa
        /// </summary>
        public static bool IsEncryptionEnabled()
        {
            var manager = GetManager();
            return manager != null && manager.EncryptionEnabled;
        }

        /// <summary>
        ///     Configura nome do jogador
        /// </summary>
        public static void SetPlayerName(string name)
        {
            GetManager()?.SetPlayerName(name);
        }

        /// <summary>
        ///     Ativa/desativa logs verbosos
        /// </summary>
        public static void EnableVerboseLogging(bool enable)
        {
            GetManager()?.SetVerboseLogging(enable);
        }

        /// <summary>
        ///     Atualiza lista de objetos saveáveis
        /// </summary>
        public static void RefreshObjects()
        {
            GetManager()?.RefreshSaveableObjects();
        }

        /// <summary>
        ///     Obtém informações de debug do sistema
        /// </summary>
        public static string GetDebugInfo()
        {
            return GetManager()?.GetDebugInfo() ?? "UltraSaveManager não encontrado";
        }

        /// <summary>
        ///     Verifica se o sistema está inicializado
        /// </summary>
        public static bool IsInitialized()
        {
            var manager = GetManager();
            return manager != null && manager.IsInitialized;
        }

        /// <summary>
        ///     Obtém estatísticas do sistema
        /// </summary>
        public static (int saves, int loads, int objects) GetStats()
        {
            var manager = GetManager();
            if (manager == null) return (0, 0, 0);

            return (manager.TotalSaves, manager.TotalLoads, manager.ObjectCount);
        }

        private static UltraSaveManager GetManager()
        {
            var manager = Object.FindAnyObjectByType<UltraSaveManager>();
            if (manager == null) Debug.LogError("❌ UltraSaveManager não encontrado!");
            return manager;
        }

        // Métodos internos para triggers de eventos
        internal static void TriggerSaveStarted()
        {
            OnSaveStarted?.Invoke();
        }

        internal static void TriggerSaveCompleted()
        {
            OnSaveCompleted?.Invoke();
        }

        internal static void TriggerSaveFailed()
        {
            OnSaveFailed?.Invoke();
        }

        internal static void TriggerLoadStarted()
        {
            OnLoadStarted?.Invoke();
        }

        internal static void TriggerLoadCompleted()
        {
            OnLoadCompleted?.Invoke();
        }

        internal static void TriggerLoadFailed()
        {
            OnLoadFailed?.Invoke();
        }
    }
}