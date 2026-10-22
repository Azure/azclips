// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Core.Abstractions.Recovery;

namespace Core.Authentication.Recovery;

/// <summary>Stores an immutable history key in the user's macOS file-based Keychain, without UI.</summary>
/// <remarks>
/// The file-based Keychain supports command-line hosts without data-protection-keychain
/// provisioning entitlements. Its UI policy is process-wide: the gate below coordinates only
/// this backend's instances. Other Keychain users may temporarily fail instead of prompting,
/// and must not concurrently change that policy. Use the existing isolated capture worker;
/// these synchronous native calls have no cancellation or duration guarantee.
/// The containing key store owns marker persistence after write/readback validation;
/// this adapter has no filesystem responsibilities.
/// <para>
/// Apple documents the legacy-keychain limitation of query-level no-UI flags in
/// <see href="https://github.com/apple-oss-distributions/Security/blob/main/keychain/headers/SecItem.h">SecItem.h</see>.
/// The additional public, deprecated
/// <see href="https://developer.apple.com/documentation/security/seckeychainsetuserinteractionallowed(_:)">UI-policy API</see>
/// disables missing/locked-keychain dialogs and is restored after every operation.
/// See also <see href="https://developer.apple.com/documentation/technotes/tn3137-on-mac-keychains">TN3137</see>.
/// </para>
/// </remarks>
internal sealed class MacOsFailureHistoryKeyStorage : IProtectedHistoryKeyStorage
{
    private const int KeySize = 32;
    private const int ItemNotFound = -25300;
    private static readonly object UserInteractionGate = new();

    private readonly string _service;
    private readonly string _account;
    private readonly IMacOsKeychainNative _native;

    /// <param name="service">The history-purpose namespace, not an authentication-token service.</param>
    /// <param name="account">The factory's canonical history-directory identifier.</param>
    internal MacOsFailureHistoryKeyStorage(string service, string account)
        : this(service, account, CreateNative()) { }

    internal MacOsFailureHistoryKeyStorage(
        string service, string account, IMacOsKeychainNative native)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        ArgumentNullException.ThrowIfNull(native);
        _service = service;
        _account = account;
        _native = native;
    }

    public bool RequiresMarker => true;

    public byte[] ReadData()
    {
        byte[]? key = null;
        try
        {
            WithoutUserInteraction(() => ReadKey(ref key));
            return key ?? [];
        }
        catch
        {
            if (key is not null)
                CryptographicOperations.ZeroMemory(key);
            throw;
        }
    }

    public void WriteData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != KeySize)
            throw new ArgumentException("A failure-history key must contain exactly 32 bytes.", nameof(data));

        WithoutUserInteraction(() => AddKey(data));
    }

    private void WithoutUserInteraction(Action operation)
    {
        lock (UserInteractionGate)
        {
            ThrowIfFailed(_native.GetUserInteractionAllowed(out var wasAllowed));
            try
            {
                ThrowIfFailed(_native.SetUserInteractionAllowed(false));
                operation();
            }
            finally
            {
                // Restore the observed policy, not an assumed true value, even if disabling failed.
                ThrowIfFailed(_native.SetUserInteractionAllowed(wasAllowed));
            }
        }
    }

    private void ReadKey(ref byte[]? key)
    {
        using var query = CreateQuery();
        SetConstant(query.Handle, MacOsKeychainConstant.ReturnData, MacOsKeychainConstant.BooleanTrue);
        SetConstant(query.Handle, MacOsKeychainConstant.MatchLimit, MacOsKeychainConstant.MatchLimitOne);
        nint result = 0;
        try
        {
            var status = _native.CopyMatching(query.Handle, out result);
            if (status == ItemNotFound)
                return;
            ThrowIfFailed(status);
            if (result == 0 || !_native.IsData(result) || _native.GetDataLength(result) != KeySize)
                throw InvalidKey();

            key = new byte[KeySize];
            _native.CopyDataBytes(result, key);
        }
        finally
        {
            if (result != 0)
                _native.Release(result);
        }
    }

    private void AddKey(byte[] data)
    {
        using var attributes = CreateQuery();
        using var value = Own(_native.CreateData(data));
        SetValue(attributes.Handle, MacOsKeychainConstant.ValueData, value.Handle);
        // The key is immutable. In particular, duplicate-item must never become update/delete.
        ThrowIfFailed(_native.Add(attributes.Handle));
    }

    private OwnedReference CreateQuery()
    {
        var query = Own(_native.CreateDictionary());
        try
        {
            using var service = Own(_native.CreateString(_service));
            using var account = Own(_native.CreateString(_account));
            SetConstant(query.Handle, MacOsKeychainConstant.Class, MacOsKeychainConstant.GenericPassword);
            SetValue(query.Handle, MacOsKeychainConstant.Service, service.Handle);
            SetValue(query.Handle, MacOsKeychainConstant.Account, account.Handle);
            SetConstant(query.Handle, MacOsKeychainConstant.UseDataProtectionKeychain, MacOsKeychainConstant.BooleanFalse);
            SetConstant(query.Handle, MacOsKeychainConstant.UseAuthenticationUi, MacOsKeychainConstant.AuthenticationUiFail);
            return query;
        }
        catch
        {
            query.Dispose();
            throw;
        }
    }

    private void SetConstant(nint dictionary, MacOsKeychainConstant key, MacOsKeychainConstant value)
        => SetValue(dictionary, key, RequireReference(_native.GetConstant(value)));

    private void SetValue(nint dictionary, MacOsKeychainConstant key, nint value)
        => _native.SetDictionaryValue(dictionary, RequireReference(_native.GetConstant(key)), value);

    private OwnedReference Own(nint handle) => new(_native, RequireReference(handle));

    private static nint RequireReference(nint value) => value != 0 ? value : throw Unavailable();

    private static void ThrowIfFailed(int status)
    {
        if (status != 0)
            throw Unavailable(status);
    }

    private static FailureHistoryException InvalidKey()
        => new("failure_history_key_invalid", "The macOS Keychain failure-history key is invalid.");

    private static FailureHistoryException Unavailable(int? status = null)
        => new("failure_history_key_unavailable",
            status is null
                ? "Noninteractive macOS Keychain storage is unavailable."
                : $"Noninteractive macOS Keychain storage failed (OSStatus {status.Value.ToString(CultureInfo.InvariantCulture)}). Check the user's Keychain outside this command.");

    private static IMacOsKeychainNative CreateNative()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("The macOS Keychain backend requires macOS.");
        return new SecurityFrameworkNative();
    }

    private sealed class OwnedReference(IMacOsKeychainNative native, nint handle) : IDisposable
    {
        public nint Handle { get; private set; } = handle;

        public void Dispose()
        {
            var owned = Handle;
            Handle = 0;
            if (owned != 0)
                native.Release(owned);
        }
    }

    private sealed class SecurityFrameworkNative : IMacOsKeychainNative
    {
        private const string Security = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        // Keep the libraries loaded for the lifetime of their borrowed constants and callback tables.
        private static readonly Lazy<nint> SecurityLibrary = new(() => NativeLibrary.Load(Security));
        private static readonly Lazy<nint> CoreFoundationLibrary = new(() => NativeLibrary.Load(CoreFoundation));

        public int GetUserInteractionAllowed(out bool allowed)
        {
            var status = SecKeychainGetUserInteractionAllowed(out var value);
            allowed = value != 0;
            return status;
        }

        public int SetUserInteractionAllowed(bool allowed)
            => SecKeychainSetUserInteractionAllowed(allowed ? (byte)1 : (byte)0);

        public nint GetConstant(MacOsKeychainConstant constant)
        {
            var symbol = constant switch
            {
                MacOsKeychainConstant.Class => "kSecClass",
                MacOsKeychainConstant.GenericPassword => "kSecClassGenericPassword",
                MacOsKeychainConstant.Service => "kSecAttrService",
                MacOsKeychainConstant.Account => "kSecAttrAccount",
                MacOsKeychainConstant.UseDataProtectionKeychain => "kSecUseDataProtectionKeychain",
                MacOsKeychainConstant.UseAuthenticationUi => "kSecUseAuthenticationUI",
                MacOsKeychainConstant.AuthenticationUiFail => "kSecUseAuthenticationUIFail",
                MacOsKeychainConstant.ReturnData => "kSecReturnData",
                MacOsKeychainConstant.MatchLimit => "kSecMatchLimit",
                MacOsKeychainConstant.MatchLimitOne => "kSecMatchLimitOne",
                MacOsKeychainConstant.ValueData => "kSecValueData",
                MacOsKeychainConstant.BooleanTrue => "kCFBooleanTrue",
                MacOsKeychainConstant.BooleanFalse => "kCFBooleanFalse",
                _ => throw new ArgumentOutOfRangeException(nameof(constant)),
            };
            var library = constant is MacOsKeychainConstant.BooleanTrue or MacOsKeychainConstant.BooleanFalse
                ? CoreFoundationLibrary.Value : SecurityLibrary.Value;
            // CF-valued globals contain pointers. The dictionary callback exports below are structs.
            return Marshal.ReadIntPtr(NativeLibrary.GetExport(library, symbol));
        }

        public nint CreateDictionary() => CFDictionaryCreateMutable(0, 0,
            NativeLibrary.GetExport(CoreFoundationLibrary.Value, "kCFTypeDictionaryKeyCallBacks"),
            NativeLibrary.GetExport(CoreFoundationLibrary.Value, "kCFTypeDictionaryValueCallBacks"));

        public nint CreateString(string value) => CFStringCreateWithCharacters(0, value, value.Length);

        public nint CreateData(byte[] value) => CFDataCreate(0, value, value.Length);

        public void SetDictionaryValue(nint dictionary, nint key, nint value)
            => CFDictionarySetValue(dictionary, key, value);

        public int CopyMatching(nint query, out nint result) => SecItemCopyMatching(query, out result);

        public int Add(nint attributes) => SecItemAdd(attributes, 0);

        public bool IsData(nint value) => CFGetTypeID(value) == CFDataGetTypeID();

        public nint GetDataLength(nint value) => CFDataGetLength(value);

        public void CopyDataBytes(nint value, byte[] destination)
        {
            var bytes = CFDataGetBytePtr(value);
            if (bytes == 0)
                throw InvalidKey();
            Marshal.Copy(bytes, destination, 0, destination.Length);
        }

        public void Release(nint value) => CFRelease(value);

        [DllImport(Security, ExactSpelling = true)]
        private static extern int SecKeychainGetUserInteractionAllowed(out byte state);

        [DllImport(Security, ExactSpelling = true)]
        private static extern int SecKeychainSetUserInteractionAllowed(byte state);

        [DllImport(Security, ExactSpelling = true)]
        private static extern int SecItemCopyMatching(nint query, out nint result);

        [DllImport(Security, ExactSpelling = true)]
        private static extern int SecItemAdd(nint attributes, nint result);

        [DllImport(CoreFoundation, ExactSpelling = true)]
        private static extern nint CFDictionaryCreateMutable(nint allocator, nint capacity, nint keyCallbacks, nint valueCallbacks);

        [DllImport(CoreFoundation, ExactSpelling = true)]
        private static extern void CFDictionarySetValue(nint dictionary, nint key, nint value);

        [DllImport(CoreFoundation, ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern nint CFStringCreateWithCharacters(nint allocator, [MarshalAs(UnmanagedType.LPWStr)] string value, nint length);

        [DllImport(CoreFoundation, ExactSpelling = true)]
        private static extern nint CFDataCreate(nint allocator, [In] byte[] bytes, nint length);

        [DllImport(CoreFoundation, ExactSpelling = true)]
        private static extern nuint CFGetTypeID(nint value);

        [DllImport(CoreFoundation, ExactSpelling = true)]
        private static extern nuint CFDataGetTypeID();

        [DllImport(CoreFoundation, ExactSpelling = true)]
        private static extern nint CFDataGetLength(nint value);

        [DllImport(CoreFoundation, ExactSpelling = true)]
        private static extern nint CFDataGetBytePtr(nint value);

        [DllImport(CoreFoundation, ExactSpelling = true)]
        private static extern void CFRelease(nint value);
    }
}

internal enum MacOsKeychainConstant
{
    Class,
    GenericPassword,
    Service,
    Account,
    UseDataProtectionKeychain,
    UseAuthenticationUi,
    AuthenticationUiFail,
    ReturnData,
    MatchLimit,
    MatchLimitOne,
    ValueData,
    BooleanTrue,
    BooleanFalse,
}

internal interface IMacOsKeychainNative
{
    int GetUserInteractionAllowed(out bool allowed);
    int SetUserInteractionAllowed(bool allowed);
    nint GetConstant(MacOsKeychainConstant constant);
    nint CreateDictionary();
    nint CreateString(string value);
    nint CreateData(byte[] value);
    void SetDictionaryValue(nint dictionary, nint key, nint value);
    int CopyMatching(nint query, out nint result);
    int Add(nint attributes);
    bool IsData(nint value);
    nint GetDataLength(nint value);
    void CopyDataBytes(nint value, byte[] destination);
    void Release(nint value);
}
