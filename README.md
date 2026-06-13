# RunAsPleb

RunAsPleb is a program for launching applications with standard user privileges. It is useful when an application requests administrator privileges (for example, via `requireAdministrator`) even when it does not necessarily need them.

When launched through RunAsPleb, various techniques are used in an attempt to run the application without elevated privileges. However, it is not always possible to run applications without administrator privileges, and even when it is, there is no guarantee that the application will behave as expected. This is a best-effort solution.

## How do I use RunAsPleb?

There are two simple ways to use RunAsPleb:

### 1. Drag and drop  
Drag the file you want to launch onto RunAsPleb to run it.

### 2. Context menu  
Right-click an executable or shortcut and select `Run as standard user`.

#### Enable the context menu option (one-time setup)  
To add the `Run as standard user` context menu option for executables and shortcuts, run `Add to context menu (EDIT PROGRAM PATH BEFORE RUNNING).reg` once.

Before running it, open the file and replace **all occurrences of**:
`C:\Users\User\Desktop\Programs\RunAsPleb\RunAsPleb.exe`
with the actual path to `RunAsPleb.exe`.

Note: You **MUST** use double backslashes (`\\`) as shown in the example path.

If you change the location of `RunAsPleb.exe`, remove the context menu entry (see below), then update the `.reg` file with the new path and run it again.

#### Remove the context menu option  
To remove the context menu entry, run `Remove from context menu.reg`.

No editing is required for the removal file.
