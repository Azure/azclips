using System.Reflection;
using System.Runtime.InteropServices;

namespace Accounts;

public static class CoreWrapper
{
    private delegate IntPtr RunFunc(IntPtr uri, IntPtr argsJson);
    private delegate void FreeFunc(IntPtr ptr);

    private static RunFunc  _run;
    private static FreeFunc _free;

    private static string _coreName = "az_ps";

    private static string NormalizeLibraryName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return _coreName + ".dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return _coreName + ".so";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return _coreName + ".dylib";
        throw new PlatformNotSupportedException("Unsupported OS platform");
    }

    public static void Initialize()
    {
        string fullPath = FindCirrusCorePath();

        // Load the shared library
        IntPtr handle = NativeLibrary.Load(fullPath);
        if (handle == IntPtr.Zero)
            throw new DllNotFoundException($"Cannot load library: {fullPath}");

        Console.WriteLine($"Loaded library: {fullPath}");

        IntPtr runPtr  = NativeLibrary.GetExport(handle, "run");
        IntPtr freePtr = NativeLibrary.GetExport(handle, "free_string");

        _run  = Marshal.GetDelegateForFunctionPointer<RunFunc>(runPtr);
        _free = Marshal.GetDelegateForFunctionPointer<FreeFunc>(freePtr);
    }

    public static string Run(string uri, string argumentsJson)
    {
        IntPtr uriPtr  = Marshal.StringToCoTaskMemUTF8(uri);
        IntPtr argsPtr = Marshal.StringToCoTaskMemUTF8(argumentsJson);

        IntPtr resultPtr = _run(uriPtr, argsPtr);
        string result = Marshal.PtrToStringUTF8(resultPtr);

        _free(resultPtr);
        Marshal.FreeCoTaskMem(uriPtr);
        Marshal.FreeCoTaskMem(argsPtr);

        return result;
    }

    private static string FindCirrusCorePath()
    {
        string rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string targetFile = NormalizeLibraryName();

        while (true)
        {
            if (string.IsNullOrEmpty(rootPath))
                throw new FileNotFoundException($"Could not find {targetFile}");

            try
            {
                string[] matches = Directory.GetFiles(rootPath, targetFile, SearchOption.AllDirectories);

                if (matches.Length > 0)
                {
                    return matches[0];
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 某些文件夹可能无权限访问，忽略继续向上
            }
            catch (PathTooLongException)
            {
                // 路径过长异常也忽略
            }

            var parent = Directory.GetParent(rootPath);
            rootPath = parent?.FullName;
        }
    }
}
