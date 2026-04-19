using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EncFile.Lib.Keyring
{
    public static class KeyringManager
    {
        private static readonly string ServiceName = "encfile";
        private static readonly string ConfigDir = GetConfigDir();
        private static readonly string SlotsFile = Path.Combine(ConfigDir, "slots.json");
        private static readonly string CredsFile = Path.Combine(ConfigDir, "creds.enc");

        private static string GetConfigDir()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "encfile");
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".config", "encfile");
        }

        private static void EnsureConfigDir() => Directory.CreateDirectory(ConfigDir);

        private static List<string> LoadSlots()
        {
            if (File.Exists(SlotsFile))
            {
                var json = File.ReadAllText(SlotsFile);
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            return new List<string>();
        }

        private static void SaveSlots(List<string> slots)
        {
            EnsureConfigDir();
            var unique = slots.Distinct().OrderBy(s => s).ToList();
            File.WriteAllText(SlotsFile, JsonSerializer.Serialize(unique, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static bool IsAvailable()
        {
            try { LoadSlots(); return true; }
            catch { return false; }
        }

        public static void SavePassword(string slot, string password)
        {
            slot = slot.Trim();
            if (string.IsNullOrEmpty(slot)) throw new ArgumentException("Slot name cannot be empty");

            var slots = LoadSlots();
            if (!slots.Contains(slot))
            {
                slots.Add(slot);
                SaveSlots(slots);
            }

            var creds = LoadCredentials();
            creds[slot] = password;
            SaveCredentials(creds);
        }

        public static string? GetPassword(string slot)
        {
            slot = slot.Trim();
            if (string.IsNullOrEmpty(slot)) return null;
            var creds = LoadCredentials();
            return creds.TryGetValue(slot, out var pwd) ? pwd : null;
        }

        public static bool DeletePassword(string slot)
        {
            slot = slot.Trim();
            if (string.IsNullOrEmpty(slot)) return false;

            var slots = LoadSlots();
            if (!slots.Contains(slot)) return false;

            var creds = LoadCredentials();
            if (creds.Remove(slot))
            {
                SaveCredentials(creds);
                slots.Remove(slot);
                SaveSlots(slots);
                return true;
            }
            return false;
        }

        public static List<string> ListSlots() => LoadSlots();

        // --- Secure Storage Helpers ---
        private static Dictionary<string, string> LoadCredentials()
        {
            if (!File.Exists(CredsFile)) return new Dictionary<string, string>();
            var encrypted = File.ReadAllBytes(CredsFile);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(plain) ?? new Dictionary<string, string>();
            }
            else
            {
                return AesGcmUnprotect(encrypted);
            }
        }

        private static void SaveCredentials(Dictionary<string, string> creds)
        {
            EnsureConfigDir();
            var protectedBytes = GetProtectedData(creds);
            File.WriteAllBytes(CredsFile, protectedBytes);
        }

        private static byte[] GetProtectedData(Dictionary<string, string> creds)
        {
            var json = JsonSerializer.Serialize(creds);
            var dataBytes = Encoding.UTF8.GetBytes(json);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ProtectedData.Protect(dataBytes, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                return AesGcmProtect(dataBytes);
            }
        }

        // Cross-platform AES-256-GCM fallback for Linux/macOS
        private static byte[] AesGcmProtect(byte[] plaintext)
        {
            var keyPath = Path.Combine(ConfigDir, "aes.key");
            byte[] key = File.Exists(keyPath) ? File.ReadAllBytes(keyPath) : GenerateAndSaveKey(keyPath);

            using var aes = new AesGcm(key);
            var nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            
            aes.Encrypt(nonce, plaintext, null, ciphertext, tag);

            // Format: [Nonce(12)][Ciphertext][Tag(16)]
            var result = new byte[12 + ciphertext.Length + 16];
            Buffer.BlockCopy(nonce, 0, result, 0, 12);
            Buffer.BlockCopy(ciphertext, 0, result, 12, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, 12 + ciphertext.Length, 16);
            return result;
        }

        private static Dictionary<string, string> AesGcmUnprotect(byte[] protectedData)
        {
            if (protectedData.Length < 28) 
                throw new CryptographicException("Invalid protected data format.");

            var nonce = new byte[12];
            var tag = new byte[16];
            Buffer.BlockCopy(protectedData, 0, nonce, 0, 12);
            Buffer.BlockCopy(protectedData, protectedData.Length - 16, tag, 0, 16);
            
            var ciphertext = new byte[protectedData.Length - 28];
            Buffer.BlockCopy(protectedData, 12, ciphertext, 0, ciphertext.Length);

            var keyPath = Path.Combine(ConfigDir, "aes.key");
            if (!File.Exists(keyPath)) throw new CryptographicException("Missing encryption key for cross-platform fallback.");
            var key = File.ReadAllBytes(keyPath);

            using var aes = new AesGcm(key);
            var plaintext = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            var json = Encoding.UTF8.GetString(plaintext);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }

        private static byte[] GenerateAndSaveKey(string keyPath)
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            File.WriteAllBytes(keyPath, key);
            return key;
        }
    }
}