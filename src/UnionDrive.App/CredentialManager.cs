using System.ComponentModel;
using System.Runtime.InteropServices;

namespace UnionDrive.App;

internal static class CredentialManager
{
    private const string Prefix = "UnionDrive/Profile/";
    private static string Target(Guid id) => Prefix + id.ToString("N");

    public static void Save(Guid id, string username, string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 1200 || password.Contains('\0'))
            throw new InvalidOperationException("비밀번호는 1~1200자로 입력하세요.");
        var blob = Marshal.StringToCoTaskMemUni(password);
        try
        {
            var credential = new Credential
            {
                Type = 1, TargetName = Target(id), UserName = username,
                CredentialBlob = blob, CredentialBlobSize = checked((uint)password.Length * 2), Persist = 2
            };
            if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(blob); }
    }

    public static string? Read(Guid id)
    {
        if (!CredRead(Target(id), 1, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1168) return null;
            throw new Win32Exception(error);
        }
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            return Marshal.PtrToStringUni(credential.CredentialBlob, checked((int)credential.CredentialBlobSize / 2));
        }
        finally { CredFree(pointer); }
    }

    public static void Delete(Guid id) => DeleteTarget(Target(id));
    private static void DeleteTarget(string target)
    {
        if (!CredDelete(target, 1, 0) && Marshal.GetLastWin32Error() != 1168)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static void DeleteAll()
    {
        if (!CredEnumerate(Prefix + "*", 0, out var count, out var pointers))
        {
            if (Marshal.GetLastWin32Error() == 1168) return;
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        try
        {
            for (var index = 0; index < count; index++)
            {
                var credential = Marshal.PtrToStructure<Credential>(Marshal.ReadIntPtr(pointers, index * IntPtr.Size));
                if (credential.TargetName?.StartsWith(Prefix, StringComparison.Ordinal) == true)
                    DeleteTarget(credential.TargetName);
            }
        }
        finally { CredFree(pointers); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string? TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredEnumerate(string filter, uint flags, out int count, out IntPtr credentials);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
