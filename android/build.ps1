param([string]$SdkPath = "$env:LOCALAPPDATA/Android/Sdk", [switch]$Tests)
$ErrorActionPreference = 'Stop'
if (-not $env:JAVA_HOME) {
    $bundledJdk = 'C:/Program Files/Android/Android Studio/jbr'
    if (Test-Path "$bundledJdk/bin/java.exe") { $env:JAVA_HOME = $bundledJdk }
}
if (-not (Test-Path "$SdkPath/platforms/android-36.1")) { throw 'Install Android SDK platform android-36.1 and build-tools 36.0.0 in Android Studio first.' }
$sdkProperty = $SdkPath.Replace('\', '/').Replace(':', '\:')
"sdk.dir=$sdkProperty" | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'local.properties')
$tasks = @(':app:assembleDebug', ':app:assembleDebugAndroidTest', ':app:lintDebug')
& (Join-Path $PSScriptRoot 'gradlew.bat') -p $PSScriptRoot @tasks --console=plain
if ($LASTEXITCODE -ne 0) { throw "Android build failed ($LASTEXITCODE)." }
if ($Tests) {
    & "$SdkPath/platform-tools/adb.exe" install -r "$PSScriptRoot/app/build/outputs/apk/debug/app-debug.apk"
    & "$SdkPath/platform-tools/adb.exe" install -r "$PSScriptRoot/app/build/outputs/apk/androidTest/debug/app-debug-androidTest.apk"
    & "$SdkPath/platform-tools/adb.exe" shell am instrument -w 'com.yxle.aiquota.test/com.yxle.aiquota.SmokeInstrumentation'
}
