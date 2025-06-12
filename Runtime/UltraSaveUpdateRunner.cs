using UnityEngine;

namespace UltraSaveSystem
{
    internal class UltraSaveUpdateRunner : MonoBehaviour
    {
        private void Update()
        {
            UltraSaveManager.Update();
        }
    }
}