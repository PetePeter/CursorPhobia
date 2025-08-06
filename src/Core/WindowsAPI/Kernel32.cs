using System.Runtime.InteropServices;

namespace CursorPhobia.Core.WindowsAPI;

/// <summary>
/// P/Invoke declarations for Kernel32.dll Windows API functions
/// </summary>
public static class Kernel32
{
    private const string Kernel32Dll = "kernel32.dll";

    #region Error Handling

    /// <summary>
    /// Retrieves the calling thread's last-error code value
    /// </summary>
    /// <returns>The last error code</returns>
    [DllImport(Kernel32Dll, SetLastError = true)]
    public static extern uint GetLastError();

    /// <summary>
    /// Sets the last-error code for the calling thread
    /// </summary>
    /// <param name="dwErrCode">The last-error code for the thread</param>
    [DllImport(Kernel32Dll, SetLastError = true)]
    public static extern void SetLastError(uint dwErrCode);

    #endregion

    #region Process Information

    /// <summary>
    /// Retrieves the process identifier of the calling process
    /// </summary>
    /// <returns>The process identifier of the calling process</returns>
    [DllImport(Kernel32Dll, SetLastError = true)]
    public static extern uint GetCurrentProcessId();

    /// <summary>
    /// Retrieves the thread identifier of the calling thread
    /// </summary>
    /// <returns>The thread identifier of the calling thread</returns>
    [DllImport(Kernel32Dll, SetLastError = true)]
    public static extern uint GetCurrentThreadId();

    /// <summary>
    /// Opens an existing local process object
    /// </summary>
    /// <param name="dwDesiredAccess">The access to the process object</param>
    /// <param name="bInheritHandle">If true, processes created by this process will inherit the handle</param>
    /// <param name="dwProcessId">The identifier of the local process to be opened</param>
    /// <returns>An open handle to the specified process</returns>
    [DllImport(Kernel32Dll, SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    /// <summary>
    /// Closes an open object handle
    /// </summary>
    /// <param name="hObject">A valid handle to an open object</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(Kernel32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    #endregion

    #region Module Information

    /// <summary>
    /// Retrieves a module handle for the specified module
    /// </summary>
    /// <param name="lpModuleName">The name of the loaded module</param>
    /// <returns>A handle to the specified module</returns>
    [DllImport(Kernel32Dll, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    /// <summary>
    /// Retrieves the address of an exported function or variable from the specified dynamic-link library (DLL)
    /// </summary>
    /// <param name="hModule">A handle to the DLL module that contains the function or variable</param>
    /// <param name="lpProcName">The function or variable name</param>
    /// <returns>The address of the exported function or variable</returns>
    [DllImport(Kernel32Dll, SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    #endregion

    #region Memory Management

    /// <summary>
    /// Allocates memory from the heap
    /// </summary>
    /// <param name="hHeap">Handle to the heap from which the memory will be allocated</param>
    /// <param name="dwFlags">Heap allocation options</param>
    /// <param name="dwBytes">Number of bytes to allocate</param>
    /// <returns>Pointer to the allocated memory block</returns>
    [DllImport(Kernel32Dll, SetLastError = true)]
    public static extern IntPtr HeapAlloc(IntPtr hHeap, uint dwFlags, UIntPtr dwBytes);

    /// <summary>
    /// Frees a memory block allocated from a heap
    /// </summary>
    /// <param name="hHeap">Handle to the heap whose memory block is to be freed</param>
    /// <param name="dwFlags">Heap free options</param>
    /// <param name="lpMem">Pointer to the memory block to be freed</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(Kernel32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool HeapFree(IntPtr hHeap, uint dwFlags, IntPtr lpMem);

    /// <summary>
    /// Retrieves a handle to the default heap of the calling process
    /// </summary>
    /// <returns>Handle to the calling process's heap</returns>
    [DllImport(Kernel32Dll, SetLastError = true)]
    public static extern IntPtr GetProcessHeap();

    #endregion

    #region Performance Counter

    /// <summary>
    /// Retrieves the current value of the performance counter
    /// </summary>
    /// <param name="lpPerformanceCount">Pointer to a variable that receives the counter value</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(Kernel32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

    /// <summary>
    /// Retrieves the frequency of the performance counter
    /// </summary>
    /// <param name="lpFrequency">Pointer to a variable that receives the frequency</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(Kernel32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryPerformanceFrequency(out long lpFrequency);

    #endregion

    #region Constants

    // Process access rights
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_VM_READ = 0x0010;
    public const uint PROCESS_ALL_ACCESS = 0x1F0FFF;

    // Heap flags
    public const uint HEAP_ZERO_MEMORY = 0x00000008;
    public const uint HEAP_NO_SERIALIZE = 0x00000001;

    // Common error codes
    public const uint ERROR_SUCCESS = 0;
    public const uint ERROR_FILE_NOT_FOUND = 2;
    public const uint ERROR_ACCESS_DENIED = 5;
    public const uint ERROR_INVALID_HANDLE = 6;
    public const uint ERROR_NOT_ENOUGH_MEMORY = 8;
    public const uint ERROR_INVALID_PARAMETER = 87;
    public const uint ERROR_INSUFFICIENT_BUFFER = 122;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Helper method to get the last Win32 error as a formatted string
    /// </summary>
    /// <returns>Formatted error message</returns>
    public static string GetLastErrorMessage()
    {
        uint errorCode = GetLastError();
        return errorCode == ERROR_SUCCESS
            ? "No error"
            : $"Win32 Error {errorCode}: {GetErrorMessage(errorCode)}";
    }

    /// <summary>
    /// Helper method to get a user-friendly error message for common error codes
    /// </summary>
    /// <param name="errorCode">The error code</param>
    /// <returns>User-friendly error message</returns>
    public static string GetErrorMessage(uint errorCode)
    {
        return errorCode switch
        {
            ERROR_SUCCESS => "The operation completed successfully",
            ERROR_FILE_NOT_FOUND => "The system cannot find the file specified",
            ERROR_ACCESS_DENIED => "Access is denied",
            ERROR_INVALID_HANDLE => "The handle is invalid",
            ERROR_NOT_ENOUGH_MEMORY => "Not enough storage is available to complete this operation",
            ERROR_INVALID_PARAMETER => "The parameter is incorrect",
            ERROR_INSUFFICIENT_BUFFER => "The data area passed to a system call is too small",
            _ => "Unknown error"
        };
    }

    #endregion
}