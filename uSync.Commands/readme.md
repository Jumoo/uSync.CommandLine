# uSync.Commands

The uSync specific command library for the uSync command line.

This package includes the core uSync command line libraries and specific ones for uSync (e.g uSync-Import). 

```
dotnet install package uSync.Commands
```

## Commands 
Included in this package are :

```
uSync-Export
uSync-Import
uSync-Report
```

### Options

| Option | Details
|-|-|
| Set | Handler set to use (default is 'default')
| Groups | Handler groups to use (default is the current UIEnabledGroups)
| Folder | Folder to run command agaisnt (default is /uSync/v9)
| Verbose | True if you want to get the full results json back not a summary

## Example

### 1. Default Import
Run a usync import against the usync folder (as if you have pressed the import everything button in the dashboard)

```
uSync run usync-import  [url] -user <user> -pass <pass>
```

### Import just settings 

```
uSync run usync-import -p groups=settings [url] -user <user> -pass <pass>
```

### Import content from a diffrent folder 
```
uSync run usync-import -p folder=uSync\content [url] -user <user> -pass <pass>
```

# Configuration
By default the command line library is disabled for your site, to enable it you need to add values to the appsettings.json file for your site.

```json
 "uSync": {
    "Commands": {
      "Enabled": "hmac,basic",
      "key": "HMAC-KEY-VALUE",
      "UserId" : -1
    }
  }
```

## Options
 
### Enabled
Turns on the Authentication methods for the command line - acceptible values are hmac (key based) and basic (username and password based)

### Key 
The security key to use when using hmac (key based) authentication, this is the key that you would then need to use on the command line when running remote commands against your site. 

```
usync run [command] https://you-site.com/umbraco -k <KEY-VALUE-HERE>
```

## UserId
When using Key based authentication Umbraco will authenticate the commands against the default user (-1) unless you set the UserId value - this will then set the permissions for the user to be the same as for the user id you set.



## Configuration

# Configuration
By default the command line library is disabled for your site, to enable it you need to add values to the appsettings.json file for your site.

```json
 "uSync": {
    "Commands": {
      "Enabled": "hmac,basic",
      "key": "HMAC-KEY-VALUE",
      "UserId" : -1
    }
  }
```

## Options
 
### Enabled
Turns on the Authentication methods for the command line - acceptible values are hmac (key based) and basic (username and password based)

### Key 
The security key to use when using hmac (key based) authentication, this is the key that you would then need to use on the command line when running remote commands against your site. 

```
usync run [command] https://you-site.com/umbraco -k <KEY-VALUE-HERE>
```

## UserId
When using Key based authentication Umbraco will authenticate the commands against the default user (-1) unless you set the UserId value - this will then set the permissions for the user to be the same as for the user id you set.

