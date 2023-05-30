# uSync.Commands.Server

uSycnc commands library for Umbraco. 

This is the package to install on the Umbraco site to Enable the uSync command line to run against your site. 

```
dotnet add package uSync.Commands.server
```

> Note: this is the root command it doesn't include the uSync specific commands use uSync.Commands to also get them.

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

> TIP: You can generate a key with the usync key-gen command
> ```
> uSync key-gen
> ```
> this will give you the correct config values to add the key to your site. 


## UserId
When using Key based authentication Umbraco will authenticate the commands against the default user (-1) unless you set the UserId value - this will then set the permissions for the user to be the same as for the user id you set.


# Commands
the core commands included with this package.

## info
return information about an umbraco installation 

## Ping
Ping a server until it is up (at least until Umbraco controllers respond).

## Rebuild-DBCache
Rebuild the database cache 

## Rebuild-Index
Rebuild an examine index on the server

## Reload MemCache
Reload the in-memory cache of items

## Test 
Run a test command 10 times (test the looping of commands.)
