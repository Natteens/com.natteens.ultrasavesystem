using System.Threading.Tasks;

namespace UltraSaveSystem
{
    public static class UltraSaveAPI
    {
        public static async Task<bool> Save()
        {
            return await UltraSaveManager.SaveAsync();
        }
        
        public static async Task<bool> Load()
        {
            return await UltraSaveManager.LoadAsync();
        }
        
        public static async Task<bool> SaveToSlot(int slot)
        {
            return await UltraSaveManager.SaveAsync(slot);
        }
        
        public static async Task<bool> LoadFromSlot(int slot)
        {
            return await UltraSaveManager.LoadAsync(slot);
        }
        
        public static void SetPlayerName(string name)
        {
            if (UltraSaveManager.Config != null)
                UltraSaveManager.Config.playerName = name;
        }
        
        public static string GetPlayerName()
        {
            return UltraSaveManager.Config?.playerName ?? "Player";
        }
        
        public static bool HasSave(int slot = 0)
        {
            return UltraSaveManager.HasSave(slot);
        }
        
        public static bool DeleteSave(int slot)
        {
            return UltraSaveManager.DeleteSave(slot);
        }
    }
}