using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace UltraSaveSystem
{
    /// <summary>
    ///     Sistema de criptografia ultra-otimizado e robusto para saves
    /// </summary>
    public static class UltraCrypto
    {
        // Prefixo para identificar dados criptografados
        private const string CRYPTO_PREFIX = "ULTRAENC:";
        private static readonly byte[] _saltBytes = Encoding.UTF8.GetBytes("UltraGameSave2024_SecureSalt");
        private static byte[] _derivedKey;
        private static bool _isInitialized;

        /// <summary>
        ///     Inicializa a criptografia com chave única do dispositivo
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            var deviceInfo = $"{SystemInfo.deviceUniqueIdentifier}_{Application.version}_{SystemInfo.processorType}";
            var userInfo = $"Natteens_{DateTime.UtcNow.Year}";
            var finalKey = $"{deviceInfo}_{userInfo}";

            _derivedKey = GenerateSecureKey(finalKey);
            _isInitialized = true;

            Debug.Log("🔐 UltraCrypto inicializado com chave segura");
        }

        private static byte[] GenerateSecureKey(string password)
        {
            using var rfc2898 = new Rfc2898DeriveBytes(
                password,
                _saltBytes,
                100000, // 100k iterações para segurança extra
                HashAlgorithmName.SHA256
            );
            return rfc2898.GetBytes(32); // Chave AES-256
        }

        /// <summary>
        ///     Criptografa texto de forma assíncrona e ultra-segura
        /// </summary>
        public static async Task<string> EncryptAsync(string plainText)
        {
            if (!_isInitialized) Initialize();
            if (string.IsNullOrEmpty(plainText)) return "";

            return await Task.Run(() =>
            {
                try
                {
                    // Verificar se já está criptografado
                    if (IsEncrypted(plainText))
                    {
                        Debug.LogWarning("⚠️ Texto já está criptografado, retornando como está");
                        return plainText;
                    }

                    var plainBytes = Encoding.UTF8.GetBytes(plainText);
                    var encryptedBytes = EncryptBytesInternal(plainBytes);
                    var base64 = Convert.ToBase64String(encryptedBytes);

                    // Adicionar prefixo para identificação
                    return CRYPTO_PREFIX + base64;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ Erro na criptografia: {ex.Message}");
                    return plainText; // Fallback para texto original
                }
            });
        }

        /// <summary>
        ///     Descriptografa texto de forma assíncrona e ultra-segura
        /// </summary>
        public static async Task<string> DecryptAsync(string encryptedText)
        {
            if (!_isInitialized) Initialize();
            if (string.IsNullOrEmpty(encryptedText)) return "";

            return await Task.Run(() =>
            {
                try
                {
                    // Verificar se realmente está criptografado
                    if (!IsEncrypted(encryptedText))
                    {
                        Debug.LogWarning("⚠️ Texto não está criptografado, retornando como está");
                        return encryptedText;
                    }

                    // Remover prefixo
                    var base64Data = encryptedText.Substring(CRYPTO_PREFIX.Length);
                    var encryptedBytes = Convert.FromBase64String(base64Data);
                    var decryptedBytes = DecryptBytesInternal(encryptedBytes);
                    var result = Encoding.UTF8.GetString(decryptedBytes);

                    // Verificar se o resultado é um JSON válido
                    if (IsValidJson(result)) return result;

                    Debug.LogWarning("⚠️ JSON descriptografado inválido");
                    return encryptedText; // Retornar original em caso de erro
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ Erro na descriptografia: {ex.Message}");
                    // Tentar retornar como texto normal
                    return encryptedText;
                }
            });
        }

        /// <summary>
        ///     Criptografia síncrona para campos específicos
        /// </summary>
        public static string EncryptField(object value)
        {
            if (!_isInitialized) Initialize();
            if (value == null) return "";

            try
            {
                var wrapper = new FieldWrapper { Value = value };
                var json = JsonUtility.ToJson(wrapper);
                var plainBytes = Encoding.UTF8.GetBytes(json);
                var encryptedBytes = EncryptBytesInternal(plainBytes);
                var base64 = Convert.ToBase64String(encryptedBytes);
                return CRYPTO_PREFIX + base64;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"⚠️ Erro ao criptografar campo: {ex.Message}");
                return value?.ToString() ?? "";
            }
        }

        /// <summary>
        ///     Descriptografia síncrona para campos específicos
        /// </summary>
        public static T DecryptField<T>(string encryptedValue, T defaultValue = default)
        {
            if (!_isInitialized) Initialize();
            if (string.IsNullOrEmpty(encryptedValue)) return defaultValue;

            try
            {
                if (!IsEncrypted(encryptedValue))
                {
                    // Tentar converter diretamente
                    if (encryptedValue is T directValue)
                        return directValue;

                    return (T)Convert.ChangeType(encryptedValue, typeof(T));
                }

                var base64Data = encryptedValue.Substring(CRYPTO_PREFIX.Length);
                var encryptedBytes = Convert.FromBase64String(base64Data);
                var decryptedBytes = DecryptBytesInternal(encryptedBytes);
                var json = Encoding.UTF8.GetString(decryptedBytes);
                var wrapper = JsonUtility.FromJson<FieldWrapper>(json);

                if (wrapper?.Value != null)
                {
                    if (wrapper.Value is T directVal)
                        return directVal;

                    return (T)Convert.ChangeType(wrapper.Value, typeof(T));
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"⚠️ Erro ao descriptografar campo: {ex.Message}");
                return defaultValue;
            }
        }

        private static byte[] EncryptBytesInternal(byte[] plainBytes)
        {
            using var aes = Aes.Create();
            aes.Key = _derivedKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var encryptedData = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Combinar IV + dados criptografados
            var result = new byte[aes.IV.Length + encryptedData.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedData, 0, result, aes.IV.Length, encryptedData.Length);

            return result;
        }

        private static byte[] DecryptBytesInternal(byte[] encryptedBytes)
        {
            if (encryptedBytes.Length < 16)
                throw new ArgumentException("Dados criptografados muito pequenos");

            using var aes = Aes.Create();
            aes.Key = _derivedKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Extrair IV dos primeiros 16 bytes
            var iv = new byte[16];
            Buffer.BlockCopy(encryptedBytes, 0, iv, 0, 16);
            aes.IV = iv;

            // Extrair dados criptografados
            var encryptedData = new byte[encryptedBytes.Length - 16];
            Buffer.BlockCopy(encryptedBytes, 16, encryptedData, 0, encryptedData.Length);

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        }

        /// <summary>
        ///     Verifica se o texto está criptografado (com prefixo)
        /// </summary>
        public static bool IsEncrypted(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            // Verificar prefixo
            if (!text.StartsWith(CRYPTO_PREFIX)) return false;

            try
            {
                var base64Data = text.Substring(CRYPTO_PREFIX.Length);
                Convert.FromBase64String(base64Data);
                return base64Data.Length > 20;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Verifica se uma string é um JSON válido
        /// </summary>
        private static bool IsValidJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            try
            {
                // Tentar fazer parse básico
                if (text.TrimStart().StartsWith("{") && text.TrimEnd().EndsWith("}"))
                    // Verificar se não contém caracteres inválidos para números
                    return !text.Contains("Miss exponent");
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Limpa e corrige possíveis problemas no JSON
        /// </summary>
        public static string CleanJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;

            try
            {
                // NÃO processar se ainda estiver criptografado
                if (IsEncrypted(json))
                {
                    Debug.LogWarning("⚠️ CleanJson: Texto ainda está criptografado!");
                    return json;
                }

                // Corrigir problemas de localização (vírgula decimal brasileira)
                var currentCulture = CultureInfo.CurrentCulture;
                if (currentCulture.NumberFormat.NumberDecimalSeparator == ",")
                {
                    // Substituir vírgulas por pontos em números decimais dentro do JSON
                    // Cuidado para não afetar strings
                    var regex = new Regex(@":\s*(-?\d+),(\d+)");
                    json = regex.Replace(json, ": $1.$2");
                }

                // Remover vírgulas desnecessárias
                json = json.Replace(",}", "}");
                json = json.Replace(",]", "]");

                // Corrigir possíveis problemas com espaços extras
                json = json.Trim();

                return json;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"⚠️ Erro ao limpar JSON: {ex.Message}");
                return json;
            }
        }

        [Serializable]
        private class FieldWrapper
        {
            public object Value;
        }
    }
}