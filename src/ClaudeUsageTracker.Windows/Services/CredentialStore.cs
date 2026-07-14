using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Stores per-profile session key + organization ID in Windows Credential Manager, replacing the
/// macOS app's KeychainService. Each profile gets its own generic credential, target-named
/// "ClaudeUsageTracker:Profile:{profileId}". The legacy fixed target ("ClaudeUsageTracker:Profile",
/// used before multi-profile support) is only read/cleared now, by ProfileManager's one-time
/// migration — SaveLegacy exists solely so tests can seed a pre-migration state.
/// </summary>
public static class CredentialStore
{
    private const string LegacyTargetName = "ClaudeUsageTracker:Profile";
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;

    private static string TargetName(Guid profileId) => $"ClaudeUsageTracker:Profile:{profileId:D}";

    public static void Save(Guid profileId, StoredCredentials credentials) => SaveToTarget(TargetName(profileId), credentials);

    public static bool TryLoad(Guid profileId, out StoredCredentials? credentials) => TryLoadFromTarget(TargetName(profileId), out credentials);

    public static void Clear(Guid profileId) => CredDelete(TargetName(profileId), CredTypeGeneric, 0);

    /// <summary>Only used by tests to seed a pre-migration state.</summary>
    public static void SaveLegacy(StoredCredentials credentials) => SaveToTarget(LegacyTargetName, credentials);

    /// <summary>Only used by ProfileManager's one-time migration.</summary>
    public static bool TryLoadLegacy(out StoredCredentials? credentials) => TryLoadFromTarget(LegacyTargetName, out credentials);

    /// <summary>Only used by ProfileManager's one-time migration, after a successful copy.</summary>
    public static void ClearLegacy() => CredDelete(LegacyTargetName, CredTypeGeneric, 0);

    private static void SaveToTarget(string targetName, StoredCredentials credentials)
    {
        var json = JsonSerializer.Serialize(credentials);
        var blob = Encoding.Unicode.GetBytes(json);
        var blobPtr = Marshal.AllocHGlobal(blob.Length);
        var targetNamePtr = Marshal.StringToCoTaskMemUni(targetName);
        var userNamePtr = Marshal.StringToCoTaskMemUni(Environment.UserName);

        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);

            var native = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = targetNamePtr,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPtr,
                Persist = CredPersistLocalMachine,
                UserName = userNamePtr
            };

            if (!CredWrite(ref native, 0))
            {
                throw new InvalidOperationException(
                    $"Failed to save credentials to Windows Credential Manager (Win32 error {Marshal.GetLastWin32Error()})");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
            Marshal.FreeCoTaskMem(targetNamePtr);
            Marshal.FreeCoTaskMem(userNamePtr);
        }
    }

    private static bool TryLoadFromTarget(string targetName, out StoredCredentials? credentials)
    {
        credentials = null;

        if (!CredRead(targetName, CredTypeGeneric, 0, out var credentialPtr))
            return false;

        try
        {
            var native = Marshal.PtrToStructure<NativeCredential>(credentialPtr);
            if (native.CredentialBlob == IntPtr.Zero || native.CredentialBlobSize == 0)
                return false;

            var blob = new byte[native.CredentialBlobSize];
            Marshal.Copy(native.CredentialBlob, blob, 0, (int)native.CredentialBlobSize);

            credentials = JsonSerializer.Deserialize<StoredCredentials>(Encoding.Unicode.GetString(blob));
            return credentials is not null;
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
    private static extern void CredFree(IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, int flags);
}
