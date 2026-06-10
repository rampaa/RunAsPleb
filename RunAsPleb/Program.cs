using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace RunAsPleb;

file static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length is 0)
        {
            return;
        }

        string targetFilePath = Path.GetFullPath(args[0]);
        if (!File.Exists(targetFilePath))
        {
            return;
        }

        Environment.SetEnvironmentVariable("__COMPAT_LAYER", "RunAsInvoker");

        string? targetFileDirectory = Path.GetDirectoryName(targetFilePath);
        Debug.Assert(targetFileDirectory is not null);

        if (!NativeMethods.OpenProcessToken(NativeMethods.CURRENT_PROCESS_HANDLE, NativeMethods.TOKEN_QUERY, out nint tokenHandleWithTokenQueryPrivilege))
        {
            Launch(targetFilePath, targetFileDirectory);
            return;
        }

        try
        {
            if (!IsTokenElevated(tokenHandleWithTokenQueryPrivilege))
            {
                Launch(targetFilePath, targetFileDirectory);
                return;
            }

            if (TryLaunchWithExplorerToken(targetFilePath, targetFileDirectory))
            {
                return;
            }

            Environment.SetEnvironmentVariable("__COMPAT_LAYER", null);
            Launch(targetFilePath, targetFileDirectory);
        }
        finally
        {
            if (tokenHandleWithTokenQueryPrivilege is not 0)
            {
                _ = NativeMethods.CloseHandle(tokenHandleWithTokenQueryPrivilege);
            }
        }
    }

    private static bool IsTokenElevated(nint tokenHandleWithTokenQueryPrivilege)
    {
        return NativeMethods.GetTokenInformation(tokenHandleWithTokenQueryPrivilege, NativeMethods.TOKEN_INFORMATION_CLASS.TokenElevation, out NativeMethods.TOKEN_ELEVATION elevation, Unsafe.SizeOf<NativeMethods.TOKEN_ELEVATION>(), out _) && elevation.TokenIsElevated is not 0;
    }

    private static void Launch(string targetFilePath, string targetFileDirectory)
    {
        NativeMethods.STARTUPINFO startupInfo = new()
        {
            cb = Unsafe.SizeOf<NativeMethods.STARTUPINFO>(),
            dwFlags = NativeMethods.STARTF_USESHOWWINDOW,
            wShowWindow = NativeMethods.SW_SHOWNORMAL
        };

        bool success = NativeMethods.CreateProcessW(targetFilePath, null, 0, 0, false, 0, 0, targetFileDirectory, ref startupInfo, out NativeMethods.PROCESS_INFORMATION processInformation);
        if (success)
        {
            _ = NativeMethods.CloseHandle(processInformation.hProcess);
            _ = NativeMethods.CloseHandle(processInformation.hThread);
            return;
        }

        // CreateProcessW can't run shortcuts but ShellExecuteW can
        ShellExecute(targetFilePath, targetFileDirectory);
    }

    private static void ShellExecute(string targetFilePath, string targetFileDirectory)
    {
        _ = NativeMethods.ShellExecuteW(0, "open", targetFilePath, null, targetFileDirectory, 1);
    }

    private static bool TryLaunchWithExplorerToken(string targetFilePath, string targetFileDirectory)
    {
        nint explorerProcessHandle = 0;
        nint explorerTokenHandle = 0;
        nint primaryTokenHandle = 0;
        nint environment = 0;
        try
        {
            nint shellWindow = NativeMethods.GetShellWindow();
            if (shellWindow is 0)
            {
                return false;
            }

            _ = NativeMethods.GetWindowThreadProcessId(shellWindow, out uint explorerPid);
            if (explorerPid is 0)
            {
                return false;
            }

            explorerProcessHandle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_INFORMATION, false, explorerPid);
            if (explorerProcessHandle is 0)
            {
                return false;
            }

            if (!NativeMethods.OpenProcessToken(explorerProcessHandle, NativeMethods.TOKEN_DUPLICATE, out explorerTokenHandle))
            {
                return false;
            }

            if (!NativeMethods.DuplicateTokenEx(explorerTokenHandle, NativeMethods.TOKEN_ASSIGN_PRIMARY | NativeMethods.TOKEN_DUPLICATE | NativeMethods.TOKEN_QUERY | NativeMethods.TOKEN_ADJUST_DEFAULT | NativeMethods.TOKEN_ADJUST_SESSIONID, 0, NativeMethods.SecurityImpersonation, NativeMethods.TokenPrimary, out primaryTokenHandle))
            {
                return false;
            }

            string? commandLine = null;
            if (Path.GetExtension(targetFilePath) is ".lnk")
            {
                (targetFilePath, string arguments, targetFileDirectory) = ResolveShortcut(targetFilePath);
                if (string.IsNullOrEmpty(targetFilePath) || !File.Exists(targetFilePath))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(targetFileDirectory))
                {
                    string? workingDirectory = Path.GetDirectoryName(targetFilePath);
                    Debug.Assert(workingDirectory is not null);
                    targetFileDirectory = workingDirectory;
                }

                commandLine = string.IsNullOrWhiteSpace(arguments)
                    ? $"\"{targetFilePath}\""
                    : $"\"{targetFilePath}\" {arguments}";
            }

            NativeMethods.STARTUPINFO startupInfo = new()
            {
                cb = Unsafe.SizeOf<NativeMethods.STARTUPINFO>(),
                dwFlags = NativeMethods.STARTF_USESHOWWINDOW,
                wShowWindow = NativeMethods.SW_SHOWNORMAL
            };

            environment = NativeMethods.GetEnvironmentStringsW();
            bool success = NativeMethods.CreateProcessWithTokenW(primaryTokenHandle, 0, targetFilePath, commandLine, NativeMethods.CREATE_UNICODE_ENVIRONMENT, environment, targetFileDirectory, ref startupInfo, out NativeMethods.PROCESS_INFORMATION processInfo);
            if (success)
            {
                _ = NativeMethods.CloseHandle(processInfo.hProcess);
                _ = NativeMethods.CloseHandle(processInfo.hThread);
            }

            return success;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (explorerProcessHandle is not 0)
            {
                _ = NativeMethods.CloseHandle(explorerProcessHandle);
            }

            if (explorerTokenHandle is not 0)
            {
                _ = NativeMethods.CloseHandle(explorerTokenHandle);
            }

            if (primaryTokenHandle is not 0)
            {
                _ = NativeMethods.CloseHandle(primaryTokenHandle);
            }

            if (environment is not 0)
            {
                _ = NativeMethods.FreeEnvironmentStringsW(environment);
            }
        }
    }

    private static unsafe (string target, string arguments, string workingDirectory) ResolveShortcut(string lnkPath)
    {
        IShellLinkW shellLink = ShellLink.CreateInstance<IShellLinkW>();
        ((IPersistFile)shellLink).Load(lnkPath, 0);

        char* targetBuffer = stackalloc char[260];
        char* workDirBuffer = stackalloc char[260];
        WIN32_FIND_DATAW findData = default;

        shellLink.GetPath(targetBuffer, 260, &findData, (uint)SLGP_FLAGS.SLGP_RAWPATH);
        shellLink.GetWorkingDirectory(workDirBuffer, 260);

        return (
            Environment.ExpandEnvironmentVariables(new string(targetBuffer)),
            Environment.ExpandEnvironmentVariables(GetArguments(shellLink)),
            Environment.ExpandEnvironmentVariables(new string(workDirBuffer))
        );
    }

    private static unsafe string GetArguments(IShellLinkW shellLink)
    {
        IPropertyStore store = (IPropertyStore)shellLink;
        store.GetValue(PInvoke.PKEY_Link_Arguments, out PROPVARIANT prop);
        try
        {
            if (prop.Anonymous.Anonymous.vt is VARENUM.VT_EMPTY or VARENUM.VT_NULL)
            {
                return "";
            }

            _ = PInvoke.PropVariantToStringAlloc(prop, out PWSTR pszOut);
            try
            {
                return pszOut.ToString();
            }
            finally
            {
                PInvoke.CoTaskMemFree(pszOut.Value);
            }
        }
        finally
        {
            _ = PInvoke.PropVariantClear(ref prop);
        }
    }
}
