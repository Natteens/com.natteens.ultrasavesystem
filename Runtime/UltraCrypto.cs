using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UltraSaveSystem
{
    public static class UltraCrypto
    {
        private static string _key;
        private static byte[] _keyBytes;
        private static byte[] _iv;
        
        public static void Initialize()
        {
            _key = SystemInfo.deviceUniqueIdentifier + "495svF!iDX";
            _keyBytes = GenerateKey(_key);
            _iv = GenerateIV(_key);
        }
        
        public static async Task<string> EncryptAsync(string plainText)
        {
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = await EncryptBytesAsync(bytes);
            return Convert.ToBase64String(encryptedBytes);
        }
        
        public static async Task<string> DecryptAsync(string cipherText)
        {
            try
            {
                var encryptedBytes = Convert.FromBase64String(cipherText);
                var decryptedBytes = await DecryptBytesAsync(encryptedBytes);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Erro na descriptografia de string: {ex.Message}");
                throw;
            }
        }
        
        public static async Task<byte[]> EncryptBytesAsync(byte[] data)
        {
            if (_keyBytes == null) Initialize();
            
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _keyBytes;
                    aes.IV = _iv;
                    
                    using (var encryptor = aes.CreateEncryptor())
                    using (var ms = new MemoryStream())
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        await cs.WriteAsync(data, 0, data.Length);
                        cs.FlushFinalBlock();
                        return ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Erro na criptografia de bytes: {ex.Message}");
                throw;
            }
        }
        
        public static async Task<byte[]> DecryptBytesAsync(byte[] encryptedData)
        {
            if (_keyBytes == null) Initialize();
            
            if (encryptedData == null || encryptedData.Length == 0)
            {
                throw new ArgumentException("Dados criptografados inválidos ou vazios");
            }
            
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _keyBytes;
                    aes.IV = _iv;
                    
                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(encryptedData))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var result = new MemoryStream())
                    {
                        await cs.CopyToAsync(result);
                        var decryptedData = result.ToArray();
                        
                        if (decryptedData.Length == 0)
                        {
                            throw new InvalidOperationException("Resultado da descriptografia está vazio");
                        }
                        
                        return decryptedData;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Erro na descriptografia de bytes: {ex.Message}");
                throw;
            }
        }
        
        private static byte[] GenerateKey(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            }
        }
        
        private static byte[] GenerateIV(string input)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            }
        }
    }
}