# uSync Command Line library

The uSync command line library has two parts 

## uSync command line
the uSync command line is a dotnet tool that you install locally (or in your CI/CD pipeline!). to run commands remotely against an umbraco site. 

```
dotnet tool install uSync.Cli -g
```

when the tool is installed (and the Umbraco site is configured) you can run remote commands against the Umbrac site. 

for example
```
uSync run info -s http://localhost:44382/umbraco -user <username> -pass <password>
```

Will return information about your site

```
{
  "version": "10.2.0",
  "level": "Run",
  "role": 1,
  "servers": "https://localhost:44382/",
  "environment": "Development",
  "applicatioName": "uSync.Site",
  "contentRootPath": "C:\\Source\\Testings\\uSync.CommandLine\\uSync.Site"
}
```

## uSync Command Library (for Umbraco)

In-order for the uSync command line to successfully connect with a site - that site needs to have the uSync command library installed **and configured**

```
dotnet install package uSync.Commands
```

by default the command line is disabeld you need to turn it on for the authentication methods you wish to use. 

```json
"uSync": {
  "Commands": {
    "Enabled": "hmac,basic",
    "key": "HMAC-KEY-VALUE",
    "UserId" : -1
  }
}
```

*.. see [uSync.Commands.Server](./uSync.Commands.Server/readme.md) readme for details.*

