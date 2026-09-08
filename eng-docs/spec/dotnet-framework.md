# .NET

## Dotnet 10

Azclips is built on .NET 10. This gives us access to the latest C# language features and performance improvements, as well as long-term support. However, it also means it has a minimum requirement of .NET 10 runtime. For CLI users, the runtime needs to be bundled with the application. For PowerShell users, either

1. The PowerShell host must be running on .NET 10, i.e. PowerShell 7.6 or later - reduced size.
2. We must bundle the .NET 10 runtime with the module - broader compatibility.
