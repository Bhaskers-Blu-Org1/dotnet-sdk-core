﻿if(Test-Path -Path coverage)
{
  Remove-Item .\coverage -recurse
}

dotnet restore

if((Test-Path -Path packages))
{
    Remove-Item .\packages -recurse
}

New-Item -path . -name packages -itemtype directory
nuget install -Verbosity quiet -OutputDirectory packages -Version 4.6.519 OpenCover
nuget install -Verbosity quiet -OutputDirectory packages -Version 2.4.5.0 ReportGenerator

New-Item -path . -name coverage -itemtype directory

$openCover = '.\packages\OpenCover.4.6.519\tools\OpenCover.Console.exe'

ForEach ($folder in (Get-ChildItem -Path .\test -Directory)) 
{
    $targetArgs = '-targetargs: test ' + $folder.FullName + ' -c Release -f netcoreapp2.0'
    $filter = '-filter:+[IBM.Cloud.SDK.Core*]*-[*Tests*]*-[*Example*]*'
    & $openCover '-target:C:\\Program Files\\dotnet\\dotnet.exe' $targetArgs '-register:user' $filter '-oldStyle' '-mergeoutput' '-hideskipped:File' '-searchdirs:$testdir\\bin\\release\\netcoreapp2.0' '-output:coverage\\coverage.xml'
}

$reportGenerator = '.\packages\ReportGenerator.2.4.5.0\tools\ReportGenerator.exe'
& $reportGenerator -reports:coverage\coverage.xml -targetdir:coverage -verbosity:Error

Remove-Item .\packages -recurse