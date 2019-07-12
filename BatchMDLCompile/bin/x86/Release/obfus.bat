@echo off
title obfuscate

"C:\Program Files (x86)\Remotesoft\Obfuscator\bin\obfuscator.exe" -exe -out "BatchMDLCompileWithLogs_ob.exe" "BatchMDLCompileWithLogs.exe"
copy "BatchMDLCompileWithLogs_ob.exe" "Public\BatchMDLCompileWithLogs.exe" /y
del BatchMDLCompileWithLogs_ob.exe
pause