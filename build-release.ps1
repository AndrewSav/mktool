param ([string]$rid = "win-x64",[string]$tag = "dev")

$ErrorActionPreference = "Stop"
$global:ProgressPreference = "SilentlyContinue"
$name = "mktool"

$targetFramework = "net6.0"

$targetFolder = Join-Path $PSScriptRoot "Build"
$debugBin = Join-Path $PSScriptRoot "bin\Debug\$targetFramework"

$releasePublish = Join-Path $PSScriptRoot "bin\Release\$targetFramework\$rid\publish"

if (!$tag) {  
  $project = Join-Path $PSScriptRoot "$name.csproj"
  $tag = ([xml](Get-Content $project)).Project.PropertyGroup[1].AssemblyVersion
}

$targetSelfContainedZip = Join-Path $targetFolder "$name-$rid-$tag.zip"
$targetSelfContainedTgz = Join-Path $targetFolder "$name-$rid-$tag.tgz"

mkdir $targetFolder -force | Out-Null

dotnet publish -c Release -r $rid -p:PublishTrimmed=true -p:PublishSingleFile=true
Compress-Archive "$releasePublish/*" $targetSelfContainedZip -Force
Push-Location
cd $releasePublish
tar -czf $targetSelfContainedTgz *
Pop-Location
