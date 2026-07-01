using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Stores the session key + organization ID in Windows Credential Manager, replacing the
/// macOS app's KeychainService. Uses a single generic credential holding a small JSON blob.
/// </summary>
public static class CredentialStore
{
    private const string TargetName = "ClaudeUsageTracker:Profile";
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;

    public static void Save(StoredCredentials credentials)
    {
        var json = JsonSerializer.Serialize(credentials);
        var blob = Encoding.Unicode.GetBytes(json);
        var blobPtr = Marshal.AllocHGlobal(blob.Length);
        var targetNamePtr = Marshal.StringToCoTaskMemUni(TargetName);
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

    public static bool TryLoad(out StoredCredentials? credentials)
    {
        credentials = null;

        if (!CredRead(TargetName, CredTypeGeneric, 0, out var credentialPtr))
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

    /// <summary>No-op if no credential is currently stored.</summary>
    public static void Clear() => CredDelete(TargetName, CredTypeGeneric, 0);

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
