@setlocal
@echo off
@call "C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\Common7\Tools\VsDevCmd.bat" > nul
cd /d %~dp0\..
msbuild /m /p:debugtype=portable /p:platform=x64 /clp:verbosity=minimal /clp:summary /t:restore %* Collections.Sync.sln
msbuild /m /p:debugtype=portable /p:platform=x64 /p:GenerateFullPaths=true /clp:verbosity=minimal /clp:summary %* Collections.Sync.sln
