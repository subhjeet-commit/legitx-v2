using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Linq;
using System.Diagnostics;

namespace LegitX_V2
{
    public class RegistryManager
    {
        private const string MAIN_REGISTRY_KEY = @"SOFTWARE\LegitX V2";
        private const string PROFILES_REGISTRY_KEY = @"SOFTWARE\LegitX V2\Profiles";
        private const string APPS_REGISTRY_KEY = @"SOFTWARE\LegitX V2\Applications";
        private const string SETTINGS_REGISTRY_KEY = @"SOFTWARE\LegitX V2\Settings";

        private const string ENCRYPTION_SALT_KEY = "EncryptionSalt";
        private const string DEFAULT_PROFILE = "Default";

        private string currentProfile = DEFAULT_PROFILE;

        private static RegistryManager instance;

        private byte[] encryptionKey;
        private byte[] encryptionSalt;

        public static RegistryManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new RegistryManager();
                return instance;
            }
        }

        private RegistryManager()
        {
            InitializeEncryption();

            EnsureRegistryKeysExist();
        }

        private void InitializeEncryption()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(MAIN_REGISTRY_KEY, false))
                {
                    if (key != null)
                    {
                        byte[] salt = key.GetValue(ENCRYPTION_SALT_KEY) as byte[];
                        if (salt != null && salt.Length == 16)
                        {
                            encryptionSalt = salt;
                        }
                    }
                }

                if (encryptionSalt == null)
                {
                    encryptionSalt = new byte[16];
                    using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                    {
                        rng.GetBytes(encryptionSalt);
                    }

                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(MAIN_REGISTRY_KEY, true))
                    {
                        if (key != null)
                        {
                            key.SetValue(ENCRYPTION_SALT_KEY, encryptionSalt, RegistryValueKind.Binary);
                        }
                    }
                }

                string uniqueId = GetUniqueHardwareId();
                encryptionKey = CreateEncryptionKey(uniqueId, encryptionSalt);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing encryption: {ex.Message}");
                string fallbackKey = "LegitX-V2-Default-Key";
                encryptionSalt = Encoding.UTF8.GetBytes(fallbackKey.PadRight(16).Substring(0, 16));
                encryptionKey = Encoding.UTF8.GetBytes(fallbackKey.PadRight(32).Substring(0, 32));
            }
        }

        private string GetUniqueHardwareId()
        {
            StringBuilder sb = new StringBuilder();

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (key != null)
                    {
                        object procId = key.GetValue("ProcessorNameString");
                        if (procId != null)
                            sb.Append(procId.ToString());
                    }
                }

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                {
                    if (key != null)
                    {
                        object biosDate = key.GetValue("BIOSReleaseDate");
                        object biosVendor = key.GetValue("BIOSVendor");
                        if (biosDate != null)
                            sb.Append(biosDate.ToString());
                        if (biosVendor != null)
                            sb.Append(biosVendor.ToString());
                    }
                }

                string systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
                if (!string.IsNullOrEmpty(systemDrive))
                {
                    try
                    {
                        string volumeSerial = BitConverter.ToString(new DriveInfo(systemDrive).RootDirectory.GetHashCode().ToString().Select(c => (byte)c).ToArray());
                        sb.Append(volumeSerial);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting hardware ID: {ex.Message}");
                sb.Append(Environment.MachineName);
            }

            if (sb.Length == 0)
                sb.Append("LegitX-Default-Hardware-ID");

            return sb.ToString();
        }

        private byte[] CreateEncryptionKey(string seed, byte[] salt)
        {
            using (Rfc2898DeriveBytes rfc2898 = new Rfc2898DeriveBytes(seed, salt, 10000))
            {
                return rfc2898.GetBytes(32);
            }
        }

        private void EnsureRegistryKeysExist()
        {
            try
            {
                Registry.CurrentUser.CreateSubKey(MAIN_REGISTRY_KEY);
                Registry.CurrentUser.CreateSubKey(PROFILES_REGISTRY_KEY);
                Registry.CurrentUser.CreateSubKey(APPS_REGISTRY_KEY);
                Registry.CurrentUser.CreateSubKey(SETTINGS_REGISTRY_KEY);

                string defaultProfileKey = $"{PROFILES_REGISTRY_KEY}\\{DEFAULT_PROFILE}";
                Registry.CurrentUser.CreateSubKey(defaultProfileKey);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring registry keys: {ex.Message}");
            }
        }

        public bool SaveSettings(Dictionary<string, object> settings)
        {
            try
            {
                string profileKeyPath = $"{PROFILES_REGISTRY_KEY}\\{currentProfile}";
                using (RegistryKey profileKey = Registry.CurrentUser.CreateSubKey(profileKeyPath, true))
                {
                    if (profileKey == null)
                        return false;

                    foreach (var setting in settings)
                    {
                        SaveRegistryValue(profileKey, setting.Key, setting.Value);
                    }
                }

                using (RegistryKey settingsKey = Registry.CurrentUser.CreateSubKey(SETTINGS_REGISTRY_KEY, true))
                {
                    if (settingsKey != null)
                    {
                        settingsKey.SetValue("CurrentProfile", currentProfile, RegistryValueKind.String);

                        foreach (var setting in settings)
                        {
                            SaveRegistryValue(settingsKey, setting.Key, setting.Value);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
                return false;
            }
        }

        private void SaveRegistryValue(RegistryKey key, string name, object value)
        {
            if (value == null)
                return;

            try
            {
                if (value is int intValue)
                {
                    key.SetValue(name, intValue, RegistryValueKind.DWord);
                }
                else if (value is double doubleValue)
                {
                    key.SetValue(name, doubleValue.ToString("G17"), RegistryValueKind.String);
                }
                else if (value is bool boolValue)
                {
                    key.SetValue(name, boolValue ? 1 : 0, RegistryValueKind.DWord);
                }
                else if (value is string stringValue)
                {
                    if (IsSensitiveSetting(name))
                    {
                        byte[] encrypted = EncryptString(stringValue);
                        key.SetValue(name, encrypted, RegistryValueKind.Binary);
                    }
                    else
                    {
                        key.SetValue(name, stringValue, RegistryValueKind.String);
                    }
                }
                else if (value is byte[] binaryValue)
                {
                    key.SetValue(name, binaryValue, RegistryValueKind.Binary);
                }
                else
                {
                    key.SetValue(name, value.ToString(), RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving registry value {name}: {ex.Message}");
            }
        }

        private bool IsSensitiveSetting(string settingName)
        {
            string[] sensitiveSettings = new string[]
            {
                "UserToken",
                "AccessKey",
                "ApiKey",
                "Password"
            };

            return sensitiveSettings.Any(s => settingName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private byte[] EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return new byte[0];

            try
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = encryptionKey;
                    aesAlg.Mode = CipherMode.CBC;

                    aesAlg.GenerateIV();
                    byte[] iv = aesAlg.IV;

                    ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, iv);

                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        msEncrypt.Write(iv, 0, iv.Length);

                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }

                        return msEncrypt.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encrypting string: {ex.Message}");
                return new byte[0];
            }
        }

        private string DecryptString(byte[] cipherText)
        {
            if (cipherText == null || cipherText.Length == 0)
                return string.Empty;

            try
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = encryptionKey;
                    aesAlg.Mode = CipherMode.CBC;

                    byte[] iv = new byte[aesAlg.BlockSize / 8];
                    Buffer.BlockCopy(cipherText, 0, iv, 0, iv.Length);
                    aesAlg.IV = iv;

                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    using (MemoryStream msDecrypt = new MemoryStream(cipherText, iv.Length, cipherText.Length - iv.Length))
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error decrypting string: {ex.Message}");
                return string.Empty;
            }
        }

        public Dictionary<string, object> LoadSettings()
        {
            Dictionary<string, object> settings = new Dictionary<string, object>();

            try
            {
                using (RegistryKey settingsKey = Registry.CurrentUser.OpenSubKey(SETTINGS_REGISTRY_KEY, false))
                {
                    if (settingsKey != null)
                    {
                        object profileName = settingsKey.GetValue("CurrentProfile");
                        if (profileName != null && !string.IsNullOrEmpty(profileName.ToString()))
                        {
                            currentProfile = profileName.ToString();
                        }
                    }
                }

                string profileKeyPath = $"{PROFILES_REGISTRY_KEY}\\{currentProfile}";
                using (RegistryKey profileKey = Registry.CurrentUser.OpenSubKey(profileKeyPath, false))
                {
                    if (profileKey != null)
                    {
                        string[] valueNames = profileKey.GetValueNames();
                        foreach (string name in valueNames)
                        {
                            object value = LoadRegistryValue(profileKey, name);
                            if (value != null)
                            {
                                settings[name] = value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            return settings;
        }

        private object LoadRegistryValue(RegistryKey key, string name)
        {
            try
            {
                object rawValue = key.GetValue(name);
                if (rawValue == null)
                    return null;

                RegistryValueKind valueKind = key.GetValueKind(name);
                switch (valueKind)
                {
                    case RegistryValueKind.DWord:
                        if (IsLikelyBooleanSetting(name))
                        {
                            int intValue = (int)rawValue;
                            return intValue != 0;
                        }
                        return (int)rawValue;

                    case RegistryValueKind.QWord:
                        return (long)rawValue;

                    case RegistryValueKind.String:
                        string stringValue = rawValue.ToString();
                        if (double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue)
                            || double.TryParse(stringValue, NumberStyles.Float, CultureInfo.CurrentCulture, out doubleValue))
                        {
                            return doubleValue;
                        }
                        return stringValue;

                    case RegistryValueKind.Binary:
                        byte[] binaryValue = (byte[])rawValue;

                        if (IsSensitiveSetting(name))
                        {
                            return DecryptString(binaryValue);
                        }
                        return binaryValue;

                    default:
                        return rawValue;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading registry value {name}: {ex.Message}");
                return null;
            }
        }

        private bool IsLikelyBooleanSetting(string settingName)
        {
            string[] boolPrefixes = new string[]
            {
                "Is", "Enable", "Disable", "Allow", "Show", "Hide", "Use", "Can"
            };

            string[] boolSuffixes = new string[]
            {
                "Enabled", "Disabled", "Active", "Visible", "Hidden", "On", "Off"
            };

            string normalizedName = settingName.ToLower();

            foreach (string prefix in boolPrefixes)
            {
                if (normalizedName.StartsWith(prefix.ToLower()))
                    return true;
            }

            foreach (string suffix in boolSuffixes)
            {
                if (normalizedName.EndsWith(suffix.ToLower()))
                    return true;
            }

            return false;
        }

        public bool SaveApplicationSettings(string applicationName, Dictionary<string, object> settings)
        {
            if (string.IsNullOrEmpty(applicationName))
                return false;

            try
            {
                string normalizedAppName = NormalizeKeyName(applicationName);

                string appKeyPath = $"{APPS_REGISTRY_KEY}\\{normalizedAppName}";
                using (RegistryKey appKey = Registry.CurrentUser.CreateSubKey(appKeyPath, true))
                {
                    if (appKey == null)
                        return false;

                    foreach (var setting in settings)
                    {
                        SaveRegistryValue(appKey, setting.Key, setting.Value);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving app settings: {ex.Message}");
                return false;
            }
        }

        public Dictionary<string, object> LoadApplicationSettings(string applicationName)
        {
            Dictionary<string, object> settings = new Dictionary<string, object>();

            if (string.IsNullOrEmpty(applicationName))
                return settings;

            try
            {
                string normalizedAppName = NormalizeKeyName(applicationName);

                string appKeyPath = $"{APPS_REGISTRY_KEY}\\{normalizedAppName}";
                using (RegistryKey appKey = Registry.CurrentUser.OpenSubKey(appKeyPath, false))
                {
                    if (appKey != null)
                    {
                        string[] valueNames = appKey.GetValueNames();
                        foreach (string name in valueNames)
                        {
                            object value = LoadRegistryValue(appKey, name);
                            if (value != null)
                            {
                                settings[name] = value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading app settings: {ex.Message}");
            }

            return settings;
        }

        public bool CreateProfile(string profileName, Dictionary<string, object> settings = null)
        {
            if (string.IsNullOrEmpty(profileName))
                return false;

            try
            {
                string normalizedProfileName = NormalizeKeyName(profileName);

                string profileKeyPath = $"{PROFILES_REGISTRY_KEY}\\{normalizedProfileName}";

                using (RegistryKey existingKey = Registry.CurrentUser.OpenSubKey(profileKeyPath, false))
                {
                    if (existingKey != null)
                    {
                        MessageBox.Show($"Profile '{profileName}' already exists.", "Profile Exists",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                }

                using (RegistryKey profileKey = Registry.CurrentUser.CreateSubKey(profileKeyPath, true))
                {
                    if (profileKey == null)
                        return false;

                    profileKey.SetValue("CreatedOn", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), RegistryValueKind.String);

                    profileKey.SetValue("DisplayName", profileName, RegistryValueKind.String);

                    if (settings != null)
                    {
                        foreach (var setting in settings)
                        {
                            SaveRegistryValue(profileKey, setting.Key, setting.Value);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating profile: {ex.Message}");
                return false;
            }
        }

        public bool DeleteProfile(string profileName)
        {
            if (string.IsNullOrEmpty(profileName) || profileName.Equals(DEFAULT_PROFILE, StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                string normalizedProfileName = NormalizeKeyName(profileName);

                string profileKeyPath = $"{PROFILES_REGISTRY_KEY}\\{normalizedProfileName}";

                bool exists = false;
                using (RegistryKey existingKey = Registry.CurrentUser.OpenSubKey(profileKeyPath, false))
                {
                    exists = existingKey != null;
                }

                if (!exists)
                {
                    MessageBox.Show($"Profile '{profileName}' doesn't exist.", "Profile Not Found",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                if (currentProfile.Equals(normalizedProfileName, StringComparison.OrdinalIgnoreCase))
                {
                    SwitchProfile(DEFAULT_PROFILE);
                }

                Registry.CurrentUser.DeleteSubKeyTree(profileKeyPath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting profile: {ex.Message}");
                return false;
            }
        }

        public bool SwitchProfile(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
                return false;

            try
            {
                string normalizedProfileName = NormalizeKeyName(profileName);

                string profileKeyPath = $"{PROFILES_REGISTRY_KEY}\\{normalizedProfileName}";

                bool exists = false;
                using (RegistryKey existingKey = Registry.CurrentUser.OpenSubKey(profileKeyPath, false))
                {
                    exists = existingKey != null;
                }

                if (!exists)
                {
                    if (!CreateProfile(profileName))
                    {
                        return false;
                    }
                }

                currentProfile = normalizedProfileName;

                using (RegistryKey settingsKey = Registry.CurrentUser.OpenSubKey(SETTINGS_REGISTRY_KEY, true))
                {
                    if (settingsKey != null)
                    {
                        settingsKey.SetValue("CurrentProfile", normalizedProfileName, RegistryValueKind.String);

                        settingsKey.SetValue("LastProfileAccess", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), RegistryValueKind.String);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error switching profile: {ex.Message}");
                return false;
            }
        }

        public List<string> GetProfiles()
        {
            List<string> profiles = new List<string>();

            try
            {
                using (RegistryKey profilesKey = Registry.CurrentUser.OpenSubKey(PROFILES_REGISTRY_KEY, false))
                {
                    if (profilesKey != null)
                    {
                        string[] subKeyNames = profilesKey.GetSubKeyNames();
                        foreach (string subKeyName in subKeyNames)
                        {
                            string displayName = subKeyName;
                            using (RegistryKey profileKey = profilesKey.OpenSubKey(subKeyName, false))
                            {
                                if (profileKey != null)
                                {
                                    object name = profileKey.GetValue("DisplayName");
                                    if (name != null && !string.IsNullOrEmpty(name.ToString()))
                                    {
                                        displayName = name.ToString();
                                    }
                                }
                            }

                            profiles.Add(displayName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting profiles: {ex.Message}");
            }

            if (profiles.Count > 0)
            {
                int defaultIndex = profiles.FindIndex(p => p.Equals(DEFAULT_PROFILE, StringComparison.OrdinalIgnoreCase));
                if (defaultIndex > 0)
                {
                    string defaultProfile = profiles[defaultIndex];
                    profiles.RemoveAt(defaultIndex);
                    profiles.Insert(0, defaultProfile);
                }
                else if (defaultIndex < 0)
                {
                    profiles.Insert(0, DEFAULT_PROFILE);
                }
            }
            else
            {
                profiles.Add(DEFAULT_PROFILE);
            }

            return profiles;
        }

        public string CurrentProfile => currentProfile;

        private string NormalizeKeyName(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
                return "UnknownKey";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                keyName = keyName.Replace(c, '_');
            }

            keyName = keyName.Replace(' ', '_');
            keyName = keyName.Replace('.', '_');

            if (keyName.Length > 255)
                keyName = keyName.Substring(0, 255);

            return keyName;
        }

        public bool ExportSettings(string filePath, bool includeAppSettings = true)
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "regedit.exe";

                    string exportKeys = MAIN_REGISTRY_KEY;
                    if (!includeAppSettings)
                    {
                        exportKeys = $"{PROFILES_REGISTRY_KEY} {SETTINGS_REGISTRY_KEY}";
                    }

                    process.StartInfo.Arguments = $"/e \"{filePath}\" \"HKEY_CURRENT_USER\\{exportKeys}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    process.WaitForExit();

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting settings: {ex.Message}");
                return false;
            }
        }

        public bool ImportSettings(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("The specified settings file doesn't exist.", "File Not Found",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "regedit.exe";
                    process.StartInfo.Arguments = $"/s \"{filePath}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    process.WaitForExit();

                    InitializeEncryption();

                    using (RegistryKey settingsKey = Registry.CurrentUser.OpenSubKey(SETTINGS_REGISTRY_KEY, false))
                    {
                        if (settingsKey != null)
                        {
                            object profileName = settingsKey.GetValue("CurrentProfile");
                            if (profileName != null && !string.IsNullOrEmpty(profileName.ToString()))
                            {
                                currentProfile = profileName.ToString();
                            }
                        }
                    }

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error importing settings: {ex.Message}");
                return false;
            }
        }

        public bool BackupSettings()
        {
            try
            {
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LegitX", "Backups");
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string backupFile = Path.Combine(backupDir, $"LegitX_Backup_{timestamp}.reg");

                return ExportSettings(backupFile, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error backing up settings: {ex.Message}");
                return false;
            }
        }

        public T GetSetting<T>(string key, T defaultValue)
        {
            try
            {
                string profileKeyPath = $"{PROFILES_REGISTRY_KEY}\\{currentProfile}";
                using (RegistryKey profileKey = Registry.CurrentUser.OpenSubKey(profileKeyPath, false))
                {
                    if (profileKey != null)
                    {
                        object value = LoadRegistryValue(profileKey, key);
                        if (value != null)
                        {
                            return (T)Convert.ChangeType(value, typeof(T));
                        }
                    }
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting setting {key}: {ex.Message}");
                return defaultValue;
            }
        }

        public bool SetSetting<T>(string key, T value)
        {
            try
            {
                string profileKeyPath = $"{PROFILES_REGISTRY_KEY}\\{currentProfile}";
                using (RegistryKey profileKey = Registry.CurrentUser.CreateSubKey(profileKeyPath, true))
                {
                    if (profileKey == null)
                        return false;

                    SaveRegistryValue(profileKey, key, value);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting {key}: {ex.Message}");
                return false;
            }
        }
    }
}