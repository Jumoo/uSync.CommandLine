<#
.SYNOPSIS
    Builds and optionally pushes the uSync Command line packages
.PARAMETER Path
    The path to the .
.PARAMETER LiteralPath
    Specifies a path to one or more locations. Unlike Path, the value of 
    LiteralPath is used exactly as it is typed. No characters are interpreted 
    as wildcards. If the path includes escape characters, enclose it in single
    quotation marks. Single quotation marks tell Windows PowerShell not to 
    interpret any characters as escape sequences.	
#>
param(
	[Parameter(Mandatory, HelpMessage="Version string to build package")]
	[string]
	[Alias("v")] $version, # version to use

	[Parameter(HelpMessage="Optional suffix to put on package (e.g beta001)")]
	[string]
	$suffix, 

	[Parameter(HelpMessage="Version of uSync to use (default will be take from package.config")]
	[string]
	$uSync,

	[Parameter(HelpMessage="Override the uSync.CommandLine dependency - when releasing out of band updated")]
	[string]
	$depends, 

	[parameter(HelpMessage="Build configuration (default release)")]
	[string]
	$env = 'release', 

	[Parameter(HelpMessage="Push to azure devops package feed")]
	[switch] $push = $false)

# get version
$versionString = $version

if (![string]::IsNullOrWhiteSpace($suffix)) {
	$versionString = -join($version, '-', $suffix)
}

$major = $version.substring(0, $version.lastIndexOf('.'))

$outfolder = ".\$major\$version\$versionString"

if (![string]::IsNullOrWhiteSpace($suffix) -and $suffix.indexOf('.') -ne -1) 
{
	$suffixFolder = $suffix.substring(0, $suffix.indexOf('.'));
	$outFolder = ".\$major\$version\$version-$suffixFolder\$versionString"
}

# workout uSync version from packages.xml ?
$packages = "..\uSync.Commands\uSync.Commands.csproj"
[xml]$packagefile = Get-Content $packages
$uSyncXml = $packagefile.Project.ItemGroup.PackageReference | Where-Object { $_.Include -eq "uSync" }  
$uSyncVersion = $uSyncXml.version

"----------------------------------"
Write-Host "Version    :" $versionString
Write-Host "uSync      :" $uSyncVersion
Write-Host "Config     :" $env
"----------------------------------" ; ""
if ([string]::IsNullOrWhiteSpace($uSyncVersion)) {
	Break
}

# dotnet build .. -c $env /p:ContinuousIntegrationBuild=true,version=$versionString
dotnet restore .. 

# for RCLs we are not currently doing this, we might but not yet. 
# rm ..\build-files\App_Plugins\ -Recurse
# &gulp minify --release $versionString

$buildParams = "ContinuousIntegrationBuild=true,version=$versionString"

""; "##### Packaging"; "----------------------------------" ; ""

dotnet pack ..\uSync\uSync.csproj -c $env -o $outFolder /p:$buildParams --no-restore # --no-build
dotnet pack ..\uSync.Commands\uSync.Commands.csproj -c $env -o $outFolder /p:$buildParams --no-restore # --no-build
dotnet pack ..\uSync.Commands.Core\uSync.Commands.Core.csproj -c $env -o $outFolder /p:$buildParams --no-restore # --no-build
dotnet pack ..\uSync.Commands.Server\uSync.Commands.Server.csproj -c $env -o $outFolder /p:$buildParams --no-restore # --no-build
# dotnet pack ..\uSync.Complete.Commands\uSync.Complete.Commands.csproj -c $env -o $outFolder /p:$buildParams --no-restore # --no-build


""; "##### Copying to LocalGit folder"; "----------------------------------" ; ""
Copy-Item -Path $outFolder\*.nupkg -Destination C:\Source\localgit


if ($push) {
    ""; "##### Pushing to our nighly package feed"; "----------------------------------" ; ""
	nuget push "$outFolder\*.nupkg" -ApiKey AzureDevOps -src https://pkgs.dev.azure.com/jumoo/Public/_packaging/nightly/nuget/v3/index.json
	
	Remove-Item ".\last-push-*" 
    Out-File -FilePath ".\last-push-$versionString.txt" -InputObject $versionString
}

Write-Host "Complete : $versionString"
Remove-Item ".\last-build-*" 
Out-File -FilePath ".\last-build-$versionString.txt" -InputObject $versionString