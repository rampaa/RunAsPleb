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

        if (!NativeMethods.OpenProcessToken(NativeMethods.CURRENT_PROCESS_HANDLE, NativeMethods.TOKEN_QUERY | NativeMethods.TOKEN_DUPLICATE, out nint tokenHandle))
        {
            if (TryExplorerShellExecute(targetFilePath, targetFileDirectory))
            {
                return;
            }

            if (!Launch(targetFilePath, targetFileDirectory))
            {
                ShellExecute(targetFilePath, targetFileDirectory);
            }

            return;
        }

        try
        {
            if (!IsTokenElevated(tokenHandle))
            {
                if (!Launch(targetFilePath, targetFileDirectory))
                {
                    ShellExecute(targetFilePath, targetFileDirectory);
                }

                return;
            }

            if (TryTokenDeElevation(targetFilePath, targetFileDirectory, tokenHandle))
            {
                return;
            }

            if (TryExplorerShellExecute(targetFilePath, targetFileDirectory))
            {
                return;
            }

            if (!Launch(targetFilePath, targetFileDirectory))
            {
                ShellExecute(targetFilePath, targetFileDirectory);
            }
        }
        finally
        {
            if (tokenHandle is not 0)
            {
                _ = NativeMethods.CloseHandle(tokenHandle);
            }
        }
    }

    private static bool IsTokenElevated(nint tokenHandle)
    {
        return NativeMethods.GetTokenInformation(tokenHandle, NativeMethods.TOKEN_INFORMATION_CLASS.TokenElevation, out NativeMethods.TOKEN_ELEVATION elevation, Unsafe.SizeOf<NativeMethods.TOKEN_ELEVATION>(), out _) && elevation.TokenIsElevated is not 0;
    }

    private static bool Launch(string targetFilePath, string targetFileDirectory)
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
            return true;
        }

        return false;
    }

    private static bool TryTokenDeElevation(string targetFilePath, string targetFileDirectory, nint tokenHandle)
    {
        nint newTokenHandle = 0;
        try
        {
            if (!NativeMethods.GetTokenInformation(tokenHandle, NativeMethods.TOKEN_INFORMATION_CLASS.TokenLinkedToken, out NativeMethods.TOKEN_LINKED_TOKEN linkedToken, Unsafe.SizeOf<NativeMethods.TOKEN_LINKED_TOKEN>(), out _))
            {
                if (!NativeMethods.CreateRestrictedToken(tokenHandle, NativeMethods.LUA_TOKEN, 0, 0, 0, 0, 0, 0, out newTokenHandle))
                {
                    return false;
                }
            }
            else
            {
                newTokenHandle = linkedToken.LinkedToken;
            }

            NativeMethods.STARTUPINFO startupInfo = new()
            {
                cb = Unsafe.SizeOf<NativeMethods.STARTUPINFO>(),
                dwFlags = NativeMethods.STARTF_USESHOWWINDOW,
                wShowWindow = NativeMethods.SW_SHOWNORMAL
            };

            bool success = NativeMethods.CreateProcessWithTokenW(newTokenHandle, 0, targetFilePath, null, 0, 0, targetFileDirectory, ref startupInfo, out NativeMethods.PROCESS_INFORMATION processInfo);
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
            if (newTokenHandle is not 0)
            {
                _ = NativeMethods.CloseHandle(newTokenHandle);
            }
        }
    }

    private static bool TryExplorerShellExecute(string targetFilePath, string targetFileDirectory)
    {
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
