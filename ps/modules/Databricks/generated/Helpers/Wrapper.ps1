function New-BindingInterface {
    param (
        [string]$dllPath
    )
    $csWrapperCode = @"
using System;
using System.Runtime.InteropServices;

namespace Cirrus {
    public static class CoreWrapper {
        const string __DllName = @"$dllPath";

        [DllImport(__DllName, EntryPoint = "run", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr run(IntPtr uri, IntPtr argumentsJson);

        [DllImport(__DllName, EntryPoint = "free_string", CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeString(IntPtr ptr);

        public static string Run(string uri, string argumentsJson) {
            IntPtr uriPtr = Marshal.StringToCoTaskMemUTF8(uri);
            IntPtr argsPtr = Marshal.StringToCoTaskMemUTF8(argumentsJson);

            IntPtr resultPtr = run(uriPtr, argsPtr);
            string result = Marshal.PtrToStringUTF8(resultPtr);

            FreeString(resultPtr);
            Marshal.FreeCoTaskMem(uriPtr);
            Marshal.FreeCoTaskMem(argsPtr);

            return result;
        }
    }
}
"@

    try {
        $null = [Cirrus.CoreWrapper]
    }
    catch {
        Add-Type -TypeDefinition $csWrapperCode -Language CSharp
    }
}

function Find-CirrusCorePath {
    $rootPath = $PSScriptRoot
    while ($true) {
        if ($null -eq $rootPath) {
            throw "Could not find az_ps.dll"
        }
        $corePath = Get-ChildItem -Path $rootPath -Filter "az_ps.dll" -Recurse -ErrorAction SilentlyContinue
        if (($null -ne $corePath) -and (Test-Path $corePath)) {
            if ($corePath.Count -eq 1) {
                return $corePath.FullName
            }
            else {
                return $corePath[0].FullName
            }
        }
        $rootPath = Join-Path $rootPath ".."
    }
}

$corePath = Find-CirrusCorePath
Write-Host "Core Path: $corePath"
New-BindingInterface -dllPath $corePath


# $result = [Cirrus.CoreWrapper]::Run("crs://azure/config/set", '{"value.key5": "value5", "value.key6": "value6"}')
# Write-Host "Run Result: $result" 
# $result = [Cirrus.CoreWrapper]::Run("crs://azure/config/get", '{}')
# Write-Host "Run Result: $result" 