using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CaveSouls.Features.SaveSystem
{
    /// <summary>
    /// Provides AES-256 encryption and decryption for tamper-resistant Souls save files.
    /// </summary>
    public static class SaveCryptoUtility
    {
        private static readonly byte[] KeyBytes = Encoding.UTF8.GetBytes("CaveSouls_2026_PrincipalArchKey!");
        private static readonly byte[] IvBytes = Encoding.UTF8.GetBytes("SoulsInitVector!");

        public static byte[] EncryptJsonToBytes(string json)
        {
            if (string.IsNullOrEmpty(json)) return Array.Empty<byte>();

            using (Aes aes = Aes.Create())
            {
                aes.Key = KeyBytes;
                aes.IV = IvBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (MemoryStream ms = new MemoryStream())
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                    cs.Write(plainBytes, 0, plainBytes.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        public static string DecryptBytesToJson(byte[] cipherBytes)
        {
            if (cipherBytes == null || cipherBytes.Length == 0) return string.Empty;

            using (Aes aes = Aes.Create())
            {
                aes.Key = KeyBytes;
                aes.IV = IvBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (MemoryStream ms = new MemoryStream(cipherBytes))
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (StreamReader reader = new StreamReader(cs, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
