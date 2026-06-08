using System.Runtime.InteropServices;

namespace RunAsPleb;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles")]
// ReSharper disable InconsistentNaming
internal static partial class NativeMethods
{
    public const uint TOKEN_QUERY = 0x0008;
    public const uint TOKEN_DUPLICATE = 0x0002;
    public const uint TOKEN_ASSIGN_PRIMARY = 0x0001;

    public const uint LUA_TOKEN = 0x4;
    public const nint CURRENT_PROCESS_HANDLE = -1;

    public const int STARTF_USESHOWWINDOW = 0x00000001;

    public const short SW_SHOWNORMAL = 1;

    public const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    internal enum TOKEN_INFORMATION_CLASS
    {
        // ReSharper disable once UnusedMember.Global
        None = 0,
        TokenLinkedToken = 19,
        TokenElevation = 20
    }

    [StructLayout(LayoutKind.Sequential)]
    internal record struct STARTUPINFO
    {
        public int cb;
        public nint lpReserved;
        public nint lpDesktop;
        public nint lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public nint lpReserved2;
        public nint hStdInput;
        public nint hStdOutput;
        public nint hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal record struct PROCESS_INFORMATION
    {
        public nint hProcess;
        public nint hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal record struct TOKEN_LINKED_TOKEN
    {
        public nint LinkedToken;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal record struct TOKEN_ELEVATION
    {
        public int TokenIsElevated;
    }

    [LibraryImport("user32.dll", EntryPoint = "GetShellWindow")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial nint GetShellWindow();

    [LibraryImport("kernel32.dll", EntryPoint = "GetEnvironmentStringsW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial nint GetEnvironmentStringsW();

    [LibraryImport("kernel32.dll", EntryPoint = "FreeEnvironmentStringsW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FreeEnvironmentStringsW(nint lpszEnvironmentBlock);

    [LibraryImport("advapi32.dll", EntryPoint = "OpenProcessToken", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenProcessToken(nint ProcessHandle, uint DesiredAccess, out nint TokenHandle);

    [LibraryImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetTokenInformation(nint TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, out TOKEN_ELEVATION TokenInformation, int TokenInformationLength, out int ReturnLength);

    [LibraryImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetTokenInformation(nint TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, out TOKEN_LINKED_TOKEN TokenInformation, int TokenInformationLength, out int ReturnLength);

    [LibraryImport("advapi32.dll", EntryPoint = "CreateRestrictedToken", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CreateRestrictedToken(nint ExistingTokenHandle, uint Flags, uint DisableSidCount, nint SidsToDisable, uint DeletePrivilegeCount, nint PrivilegesToDelete, uint RestrictedSidCount, nint SidsToRestrict, out nint NewTokenHandle);

    [LibraryImport("advapi32.dll", EntryPoint = "CreateProcessWithTokenW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CreateProcessWithTokenW(nint hToken, int dwLogonFlags, string? lpApplicationName, string? lpCommandLine, int dwCreationFlags, nint lpEnvironment, string? lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CreateProcessW(string? lpApplicationName, string? lpCommandLine, nint lpProcessAttributes, nint lpThreadAttributes, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles, uint dwCreationFlags, nint lpEnvironment, string? lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [LibraryImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint hObject);

    [LibraryImport("shell32.dll", EntryPoint = "ShellExecuteW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial nint ShellExecuteW(nint hwnd, string lpOperation, string lpFile, string? lpParameters, string? lpDirectory, int nShowCmd);

    [ComImport]
    [Guid("13709620-C279-11CE-A49E-444553540000")]
    // ReSharper disable once ClassCanBeSealed.Global
    internal class ShellApplication;

    [ComImport]
    [Guid("A4C6892C-3BA9-11D2-9DEA-00C04FB16162")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    internal interface IShellDispatch2
    {
        public void ShellExecute([MarshalAs(UnmanagedType.BStr)] string File,
            [MarshalAs(UnmanagedType.Struct)] object? vArgs,
            [MarshalAs(UnmanagedType.Struct)] object? vDir,
            [MarshalAs(UnmanagedType.Struct)] object? vOperation,
            [MarshalAs(UnmanagedType.Struct)] object? nShowCmd);
    }
}
