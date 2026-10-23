// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class StorageFileFrontendRegistration
{
    internal static void AddCli(IDictionary<string, CommandMetadata> table)
    {
        Add<UploadStorageFileCommand>("storage file upload",
        [
            .. Address(),
            P("Path", "path", typeof(string), "Remote file path, directory/ or default source filename.", aliases: ["p"]),
            P("Source", "source", typeof(string), "Local source file.", true),
            .. Content(),
            P("MetadataInput", "metadata", typeof(string[]), "Space-separated unique key=value metadata."),
            P("ValidateContent", "validate-content", typeof(bool), "Validate an MD5 hash for every uploaded range."),
            P("NoProgress", "no-progress", typeof(bool), "Suppress progress, not errors."),
            P("Concurrency", "max-connections", typeof(int), "One through four workers; default two.")
        ]);
        Add<DownloadStorageFileCommand>("storage file download",
        [
            .. Address(),
            P("Path", "path", typeof(string), "Exact source file path.", true, ["p"]),
            P("Destination", "dest", typeof(string), "Local file or existing directory; defaults to the source filename."),
            P("OpenMode", "open-mode", typeof(string), "wb replaces; ab appends and requires one worker.", values: ["wb", "ab"]),
            P("StartRange", "start-range", typeof(long), "Inclusive nonnegative start offset."),
            P("EndRange", "end-range", typeof(long), "Inclusive end offset; requires start-range."),
            P("ValidateContent", "validate-content", typeof(bool), "Validate every downloaded range."),
            P("NoProgress", "no-progress", typeof(bool), "Suppress progress, not errors."),
            P("Concurrency", "max-connections", typeof(int), "One through four workers; default two.")
        ]);
        Add<ListStorageFilesCommand>("storage file list",
        [
            .. Address(),
            P("Path", "path", typeof(string), "Directory to list; omitted means share root.", aliases: ["p"]),
            .. Listing()
        ]);
        Add<ListStorageDirectoriesCommand>("storage directory list",
        [
            .. Address(),
            P("Path", "name", typeof(string), "Directory to list; omitted means share root.", aliases: ["n"]),
            .. Listing()
        ]);
        Add<NewStorageDirectoryCommand>("storage directory create",
        [
            .. Address(),
            P("Path", "name", typeof(string), "New directory path.", true, ["n"]),
            P("MetadataInput", "metadata", typeof(string[]), "Space-separated unique key=value metadata."),
            P("FailOnExist", "fail-on-exist", typeof(bool), "Fail instead of returning created:false when the directory exists.")
        ]);

        void Add<T>(string name, ParameterMetadata[] parameters) where T : IProcessor =>
            table.Add(name, Command<T>(name, parameters));
    }

    internal static void AddPowerShell(IDictionary<string, CommandMetadata> table)
    {
        Add<GetStorageFileProcessor>("Get-AzStorageFile",
        [
            .. NativeAddress(),
            P("ExcludeExtendedInfo", "ExcludeExtendedInfo", typeof(bool), "Exclude extended properties when listing without Path.")
        ]);
        Add<NewStorageDirectoryProcessor>("New-AzStorageDirectory",
        [
            .. NativeAddress(),
            P("Metadata", "Metadata", typeof(IReadOnlyDictionary<string, string>), "Frozen metadata.")
        ]);
        Add<UploadStorageFilePowerShellCommand>("Set-AzStorageFileContent",
        [
            .. NativeAddress(),
            P("Source", "Source", typeof(string), "Resolved local source file."),
            P("PreflightOnly", "PreflightOnly", typeof(bool), "Resolve/probe without writing; default true."),
            P("Force", "Force", typeof(bool), "Skip overwrite probe in preflight."),
            P("PassThru", "PassThru", typeof(bool), "Return a native file after verification."),
            P("ValidateContent", "ValidateContent", typeof(bool), "Validate range MD5."),
            P("StoreContentMd5", "StoreContentMd5", typeof(bool), "Store a whole-file MD5; default true."),
            P("Concurrency", "Concurrency", typeof(int), "Bounded transfer workers."),
            P("ChunkSize", "ChunkSize", typeof(int), "Upload range size from one byte to 4 MiB."),
            P("ContentType", "ContentType", typeof(string), "Content type."),
            P("ContentEncoding", "ContentEncoding", typeof(string), "Content encoding."),
            P("ContentLanguage", "ContentLanguage", typeof(string), "Content language."),
            P("ContentDisposition", "ContentDisposition", typeof(string), "Content disposition."),
            P("CacheControl", "CacheControl", typeof(string), "Cache control."),
            P("ContentMd5", "ContentMd5", typeof(byte[]), "Whole-file MD5."),
            P("Metadata", "Metadata", typeof(IReadOnlyDictionary<string, string>), "Frozen metadata.")
        ]);
        Add<DownloadStorageFileProcessor>("Get-AzStorageFileContent",
        [
            .. NativeAddress(),
            P("FileObject", "FileObject", typeof(StorageFile), "Native source file."),
            P("Destination", "Destination", typeof(string), "Resolved local filename."),
            P("Overwrite", "Overwrite", typeof(bool), "Invocation-thread overwrite approval."),
            P("PassThru", "PassThru", typeof(bool), "Return a verified native source file."),
            P("CheckMd5", "CheckMd5", typeof(bool), "Validate range and stored whole-file MD5."),
            P("Concurrency", "Concurrency", typeof(int), "Bounded transfer workers."),
            P("ChunkSize", "ChunkSize", typeof(int), "Download range size from one byte to 8 MiB.")
        ]);

        void Add<T>(string name, ParameterMetadata[] parameters) where T : IProcessor =>
            table.Add(name, Command<T>(name, parameters));
    }

    private static ParameterMetadata[] Address() =>
    [
        P("StorageAccountName", "account-name", typeof(string), "Public Azure account name."),
        P("FileEndpoint", "file-endpoint", typeof(string), "HTTPS public Azure Files account root, without query."),
        P("ShareName", "share-name", typeof(string), "Share name.", true, ["s"]),
        P("ShareSnapshot", "snapshot", typeof(string), "Read-only share snapshot."),
        P("EnableFileBackupRequestIntent", "enable-file-backup-request-intent", typeof(bool), "Required explicit Files OAuth backup intent."),
        P("AllowTrailingDot", "allow-trailing-dot", typeof(bool), "Preserve trailing dots in Files names."),
        P("AuthMode", "auth-mode", typeof(string), "Entra login only; no key fallback.", values: ["login"]),
        P("Timeout", "timeout", typeof(int), "Positive service timeout per request."),
        P("Profile", "profile", typeof(string), "Named public Azure connection profile.")
    ];

    private static ParameterMetadata[] Content() =>
    [
        P("ContentType", "content-type", typeof(string), "Content type; inferred from source extension if omitted."),
        P("ContentEncoding", "content-encoding", typeof(string), "Content encoding."),
        P("ContentLanguage", "content-language", typeof(string), "Content language."),
        P("ContentDisposition", "content-disposition", typeof(string), "Content disposition."),
        P("CacheControl", "content-cache-control", typeof(string), "Cache control."),
        P("ContentMd5Input", "content-md5", typeof(string), "Base64-encoded 16-byte whole-file MD5.")
    ];

    private static ParameterMetadata[] Listing() =>
    [
        P("Prefix", "prefix", typeof(string), "Literal name prefix."),
        P("Marker", "marker", typeof(string), "Opaque continuation marker."),
        P("NumResults", "num-results", typeof(string), "Positive 64-bit limit or *; default all."),
        P("ShowNextMarker", "show-next-marker", typeof(bool), "Append a nextMarker record."),
        P("ExcludeExtendedInfo", "exclude-extended-info", typeof(bool), "Exclude extended Files properties.")
    ];

    private static ParameterMetadata[] NativeAddress() =>
    [
        P("Context", "Context", typeof(StorageContext), "Native public Azure Entra context."),
        P("ShareName", "ShareName", typeof(string), "Share name."),
        P("ShareObject", "ShareObject", typeof(StorageFileShare), "Native share."),
        P("DirectoryObject", "DirectoryObject", typeof(StorageFileDirectory), "Native base directory."),
        P("Path", "Path", typeof(string), "Relative Files path."),
        P("ShareSnapshot", "ShareSnapshot", typeof(string), "Read-only snapshot."),
        P("EnableFileBackupRequestIntent", "EnableFileBackupRequestIntent", typeof(bool), "Explicit backup intent."),
        P("AllowTrailingDot", "AllowTrailingDot", typeof(bool), "Preserve trailing dots."),
        P("Timeout", "Timeout", typeof(int), "Positive per-request service timeout."),
        P("Profile", "Profile", typeof(string), "Named public Azure profile.")
    ];

    private static ParameterMetadata P(string property, string invocation, Type type, string description,
        bool required = false, string[]? aliases = null, string[]? values = null) =>
        new(property, invocation, type, description, required, aliases ?? [], values ?? []);

    private static CommandMetadata Command<T>(string name, ParameterMetadata[] parameters) where T : IProcessor =>
        new(typeof(T).Name, name, [], typeof(T).FullName!, typeof(T).Assembly.GetName().Name!, typeof(T),
            parameters.ToDictionary(parameter => parameter.InvocationName, StringComparer.OrdinalIgnoreCase),
            parameters.SelectMany(parameter => parameter.Aliases.Select(alias => (alias, parameter.InvocationName)))
                .ToDictionary(pair => pair.alias, pair => pair.InvocationName, StringComparer.OrdinalIgnoreCase));
}
