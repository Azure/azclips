## Manual Build Workflow

### Windows

Publish the cli project for Windows x64:

```powershell
dotnet publish src/CLI/Core.Cli.csproj -c Release -r win-x64 --self-contained -o ./publish/win-x64
```

Copy the appxmanifest file to the publish folder:

```powershell
Copy-Item src/CLI/Core.Cli.appxmanifest "./publish/win-x64/AppxManifest.xml"
New-Item -ItemType Directory -Path "./publish/win-x64/Assets/" -Force
Copy-Item -Path assets/* -Destination "./publish/win-x64/Assets/" -Recurse
```

Create the appx package using the MakeAppx tool:

```powershell
& "C:\Program Files (x86)\Windows Kits\10\bin\<Your SDK Version>\x64\makeappx.exe" pack /d .\publish\win-x64\ /p "./bin/az.msix" /nv
```

(Optional) Create a local certificate for signing the package:

```powershell
New-SelfSignedCertificate -Type Custom -KeyUsage DigitalSignature -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") -Subject "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" -FriendlyName "AzclipsSelfSignedCertificate"
```

Sign the package using the SignTool:

```powershell
& "C:\Program Files (x86)\Windows Kits\10\bin\<Your SDK Version>\x64\signtool.exe" sign /fd SHA256 /sha1 "<Your Certificate Thumbprint Here>" .\bin\az.msix
```

To Install, you should trust the certificate used to sign the package first. You can export the certificate from the certificate store and then double click to install it.

```powershell
# Export to a .cer file
Export-Certificate -Cert "Cert:\CurrentUser\My\<Your Certificate Thumbprint Here>" -FilePath "$env:TEMP\azclips-cert.cer"

# Import to Trusted Root (requires admin privileges)
Import-Certificate -FilePath "$env:TEMP\azclips-cert.cer" -CertStoreLocation Cert:\LocalMachine\Root
```