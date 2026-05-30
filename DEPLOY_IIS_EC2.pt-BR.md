# Guia de Deploy IIS / EC2

> Versão em inglês: [DEPLOY_IIS_EC2.md](DEPLOY_IIS_EC2.md)

## Pré-requisitos (EC2 Windows Server)

1. Instale o **ASP.NET Core Hosting Bundle 8.x** (inclui o ASP.NET Core Runtime + módulo IIS):
   ```
   https://dotnet.microsoft.com/en-us/download/dotnet/8.0
   → "ASP.NET Core Runtime" → Windows Hosting Bundle
   ```

2. Habilite os recursos do IIS no Windows:
   ```powershell
   Install-WindowsFeature -Name Web-Server, Web-Asp-Net45, Web-CGI, Web-ISAPI-Ext, Web-ISAPI-Filter -IncludeManagementTools
   ```

3. Reinicie o IIS após instalar o Hosting Bundle:
   ```
   iisreset
   ```

## Publicando a Aplicação

A partir da raiz do repositório, na sua **máquina de build**:

```powershell
dotnet publish src/DatabaseTool.Web/DatabaseTool.Web.csproj `
  -c Release `
  -r win-x64 `
  --no-self-contained `
  -o publish/
```

Ou use o publish profile incluído:

```powershell
dotnet publish src/DatabaseTool.Web/DatabaseTool.Web.csproj `
  /p:PublishProfile=IIS_EC2
```

## Configuração do Site no IIS

1. Abra o **IIS Manager**.
2. Crie um novo **Application Pool**:
   - Nome: `DatabaseToolPool`
   - .NET CLR version: **No Managed Code**
   - Managed Pipeline Mode: **Integrated**
3. Crie um novo **Site** (ou adicione uma Application sob o Default Web Site):
   - Physical path: pasta onde você copiou o output do publish
   - Application pool: `DatabaseToolPool`
   - Port: 80 (ou a porta desejada)
4. Garanta que a identidade do App Pool tenha acesso de **leitura + escrita** à pasta do site (necessário para a persistência do `typemapping.json`).

## Copiando o Publish para o EC2

```powershell
# Exemplo usando AWS CLI / robocopy / SCP
robocopy publish\ \\ec2-server\C$\inetpub\DatabaseTool /E /PURGE
```

Ou use AWS CodeDeploy, S3 + PowerShell, ou seu pipeline de deploy preferido.

## Verificação

- Acesse `http://<ec2-public-ip>/` — a Home deve carregar.
- Acesse `/converter`, `/comparator`, `/query-converter`, `/type-mapping`, `/audit`.

## Logs

- Logs do IIS: `C:\inetpub\logs\LogFiles\`
- Logs stdout (se habilitado em `web.config`): `<raiz-do-site>\logs\stdout_*.log`

Para habilitar log stdout para diagnóstico, edite `web.config`:
```xml
<aspNetCore stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" ...>
```
Crie a pasta `logs` manualmente antes.

## Persistência do Type Mapping

O arquivo `typemapping.json` é escrito na mesma pasta da `.dll` (`AppContext.BaseDirectory`).  
Garanta que a identidade do App Pool tenha permissão de escrita nesse diretório.
