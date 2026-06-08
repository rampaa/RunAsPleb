using System.Diagnostics;
using System.Runtime.CompilerServices;

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

            if (TryTokenDeElevation(targetFilePath, targetFileDirectory))
            {
                return;
            }

            if (TryExplorerShellExecute(targetFilePath, targetFileDirectory))
            {
                return;
            }

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

    private static bool TryTokenDeElevation(string targetFilePath, string targetFileDirectory)
    {
        nint currentProcessTokenHandle = 0;
        nint newTokenHandle = 0;
        nint environment = 0;
        try
        {
            if (NativeMethods.OpenProcessToken(NativeMethods.CURRENT_PROCESS_HANDLE, NativeMethods.TOKEN_QUERY | NativeMethods.TOKEN_DUPLICATE | NativeMethods.TOKEN_ASSIGN_PRIMARY, out currentProcessTokenHandle))
            {
                // Requires TOKEN_QUERY
                if (NativeMethods.GetTokenInformation(currentProcessTokenHandle, NativeMethods.TOKEN_INFORMATION_CLASS.TokenLinkedToken, out NativeMethods.TOKEN_LINKED_TOKEN linkedToken, Unsafe.SizeOf<NativeMethods.TOKEN_LINKED_TOKEN>(), out _))
                {
                    newTokenHandle = linkedToken.LinkedToken;
                }
                else
                {
                    // Requires TOKEN_DUPLICATE
                    _ = NativeMethods.CreateRestrictedToken(currentProcessTokenHandle, NativeMethods.LUA_TOKEN, 0, 0, 0, 0, 0, 0, out newTokenHandle);
                }

                if (newTokenHandle is not 0)
                {
                    NativeMethods.STARTUPINFO startupInfo = new()
                    {
                        cb = Unsafe.SizeOf<NativeMethods.STARTUPINFO>(),
                        dwFlags = NativeMethods.STARTF_USESHOWWINDOW,
                        wShowWindow = NativeMethods.SW_SHOWNORMAL
                    };

                    environment = NativeMethods.GetEnvironmentStringsW();

                    // Requires TOKEN_QUERY | TOKEN_DUPLICATE | TOKEN_ASSIGN_PRIMARY and SE_IMPERSONATE_NAME
                    // We don't need to set AdjustTokenPrivileges since for an elevated process we already have SE_IMPERSONATE_NAME
                    bool success = NativeMethods.CreateProcessWithTokenW(newTokenHandle, 0, targetFilePath, null, NativeMethods.CREATE_UNICODE_ENVIRONMENT, environment, targetFileDirectory, ref startupInfo, out NativeMethods.PROCESS_INFORMATION processInfo);
                    if (success)
                    {
                        _ = NativeMethods.CloseHandle(processInfo.hProcess);
                        _ = NativeMethods.CloseHandle(processInfo.hThread);
                    }

                    return success;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (currentProcessTokenHandle is not 0)
            {
                _ = NativeMethods.CloseHandle(currentProcessTokenHandle);
            }

            if (newTokenHandle is not 0)
            {
                _ = NativeMethods.CloseHandle(newTokenHandle);
            }

            if (environment is not 0)
            {
                _ = NativeMethods.FreeEnvironmentStringsW(environment);
            }
        }
    }

    private static bool TryExplorerShellExecute(string targetFilePath, string targetFileDirectory)
    {
        if (NativeMethods.GetShellWindow() is 0)
        {
            return false;
        }

        try
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            NativeMethods.IShellDispatch2 shell = (NativeMethods.IShellDispatch2)new NativeMethods.ShellApplication();
            shell.ShellExecute(targetFilePath, null, targetFileDirectory, "open", 1);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ShellExecute(string targetFilePath, string targetFileDirectory)
    {
        _ = NativeMethods.ShellExecuteW(0, "open", targetFilePath, null, targetFileDirectory, 1);
    }
}
