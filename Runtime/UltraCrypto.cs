using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UltraSaveSystem
{
    public static class UltraCrypto
    {
        private const string CRYPTO_PREFIX = "ULTRAENC:";
        private static readonly byte[] _saltBytes = Encoding.UTF8.GetBytes("UltraGameSave2024_SecureSalt");
        private static byte[] _derivedKey;
        private static bool _isInitialized;
        
        public static void Initialize()
        {
            if (_isInitialized) return;
            
            var deviceInfo = $"{SystemInfo.deviceUniqueIdentifier}_{Application.version}_{SystemInfo.processorType}";
            var userInfo = $"Natteens_{DateTime.UtcNow.Year}";
            var finalKey = $"{deviceInfo}_{userInfo}";
            
            _derivedKey = GenerateSecureKey(finalKey);
            _isInitialized = true;
            
            Debug.Log("UltraCrypto initialized");
        }
        
        public static async Task<string> EncryptAsync(string plainText)
        {
            if (!_isInitialized) Initialize();
            if (string.IsNullOrEmpty(plainText)) return "";
            
            return await Task.Run(() =>
            {
                try
                {
                    if (IsEncrypted(plainText))
                        return plainText;
                    
                    var plainBytes = Encoding.UTF8.GetBytes(plainText);
                    var encryptedBytes = EncryptBytesInternal(plainBytes);
                    var base64 = Convert.ToBase64String(encryptedBytes);
                    
                    return CRYPTO_PREFIX + base64;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Encryption failed: {ex.Message}");
                    return plainText;
                }
            });
        }
        
        public static async Task<string> DecryptAsync(string encryptedText)
        {
            if (!_isInitialized) Initialize();
            if (string.IsNullOrEmpty(encryptedText)) return "";
            
            return await Task.Run(() =>
            {
                try
                {
                    if (!IsEncrypted(encryptedText))
                        return encryptedText;
                    
                    var base64Data = encryptedText.Substring(CRYPTO_PREFIX.Length);
                    var encryptedBytes = Convert.FromBase64String(base64Data);
                    var decryptedBytes = DecryptBytesInternal(encryptedBytes);
                    
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Decryption failed: {ex.Message}");
                    return encryptedText;
                }
            });
        }
        
        private static byte[] GenerateSecureKey(string password)
        {
            using var rfc2898 = new Rfc2898DeriveBytes(password, _saltBytes, 100000, HashAlgorithmName.SHA256);
            return rfc2898.GetBytes(32);
        }
        
        private static byte[] EncryptBytesInternal(byte[] data)
        {
            using var aes = Aes.Create();
            aes.Key = _derivedKey;
            aes.GenerateIV();
            
            using var encryptor = aes.CreateEncryptor();
            var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);
            
            var result = new byte[aes.IV.Length + encrypted.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
            
            return result;
        }
        
        private static byte[] DecryptBytesInternal(byte[] encryptedData)
        {
            using var aes = Aes.Create();
            aes.Key = _derivedKey;
            
            var iv = new byte[16];
            var encrypted = new byte[encryptedData.Length - 16];
            
            Buffer.BlockCopy(encryptedData, 0, iv, 0, 16);
            Buffer.BlockCopy(encryptedData, 16, encrypted, 0, encrypted.Length);
            
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        }
        
        private static bool IsEncrypted(string text)
        {
            return text.StartsWith(CRYPTO_PREFIX);
        }
    }
}