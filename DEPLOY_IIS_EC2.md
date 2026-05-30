# IIS / EC2 Deployment Guide

> Versão em português: [DEPLOY_IIS_EC2.pt-BR.md](DEPLOY_IIS_EC2.pt-BR.md)

## Prerequisites (EC2 Windows Server)

1. Install **ASP.NET Core Hosting Bundle 8.x** (includes ASP.NET Core Runtime + IIS module):
   ```
   https://dotnet.microsoft.com/en-us/download/dotnet/8.0
   → "ASP.NET Core Runtime" → Windows Hosting Bundle
   ```

2. Enable IIS features in Windows:
   ```powershell
   Install-WindowsFeature -Name Web-Server, Web-Asp-Net45, Web-CGI, Web-ISAPI-Ext, Web-ISAPI-Filter -IncludeManagementTools
   ```

3. Restart IIS after installing the Hosting Bundle:
   ```
   iisreset
   ```

## Publish the Application

From the repository root on your **build machine**:

```powershell
dotnet publish src/DatabaseTool.Web/DatabaseTool.Web.csproj `
  -c Release `
  -r win-x64 `
  --no-self-contained `
  -o publish/
```

Or use the included publish profile:

```powershell
dotnet publish src/DatabaseTool.Web/DatabaseTool.Web.csproj `
  /p:PublishProfile=IIS_EC2
```

## IIS Site Setup

1. Open **IIS Manager**.
2. Create a new **Application Pool**:
   - Name: `DatabaseToolPool`
   - .NET CLR version: **No Managed Code**
   - Managed Pipeline Mode: **Integrated**
3. Create a new **Site** (or add an Application under Default Web Site):
   - Physical path: folder where you copied the publish output
   - Application pool: `DatabaseToolPool`
   - Port: 80 (or your desired port)
4. Ensure the App Pool identity has **read + write** access to the physical path folder (needed for `typemapping.json` persistence).

## Copy Publish Output to EC2

```powershell
# Example using AWS CLI / robocopy / SCP
robocopy publish\ \\ec2-server\C$\inetpub\DatabaseTool /E /PURGE
```

Or use AWS CodeDeploy, S3 + PowerShell, or your preferred deployment pipeline.

## Verify

- Navigate to `http://<ec2-public-ip>/` — Home page loads.
- Navigate to `/converter`, `/comparator`, `/query-converter`, `/type-mapping`, `/audit`.

## Logs

- IIS logs: `C:\inetpub\logs\LogFiles\`
- stdout logs (if enabled in `web.config`): `<site-root>\logs\stdout_*.log`

To enable stdout logging for diagnostics, edit `web.config`:
```xml
<aspNetCore stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" ...>
```
Create the `logs` folder manually first.

## Type Mapping Persistence

The file `typemapping.json` is written to the same folder as the `.dll` (`AppContext.BaseDirectory`).  
Ensure the App Pool identity has write permission to that directory.
