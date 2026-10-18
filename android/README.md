# AI Quota for Android — first alpha

Android 14+ native Kotlin companion, focused on the **Xiaomi 17 Pro / HyperOS**. Standard Android APIs are used; there is no device-name lock. Broader-device certification and Xiaomi hardware validation are not yet complete. The rear Dynamic Back Display is outside this prototype.

## Implemented

- Overview with real Codex windows, countdowns, exact local reset times, source age, and seven-day line history from a paired Windows collector.
- Several paired PC profiles; select the active profile when more than one is available. These are collector aliases, not verified provider account identities. Keep separate profiles/collectors for separate accounts; switching the underlying desktop account does not partition the existing Windows history automatically.
- App toggle: Codex/ChatGPT, Claude, and Gemini selectors, direct installed-app launch, and individual shortcut visibility switches.
- Optional notch-style pill, tap to expand, long press to stop, notification stop action, and a Quick Settings tile. A vertical offset control helps position it below the camera/status bar. The system owns the actual cutout and status bar; this app does not replace them.
- Certificate-pinned HTTPS and a random bearer pairing key. Android pairing secrets are encrypted with an Android Keystore key; cloud backup is disabled. No analytics, browser cookies, API keys, prompts, or responses.

**Claude and Gemini quota connectors are not implemented.** Their selectors support launching apps, with an explicit unavailable state for usage. No demo data or estimated subscription percentages are shown. Codex means Codex usage, not all ChatGPT chat limits.

## Install and pair

1. Install `AIQuota-Android-alpha.apk` on the phone. This is a sideloadable debug alpha, not a Play Store release. Use only a build you trust.
2. Keep the Windows tracker running so it records fresh quota readings.
3. Extract the separate `AIQuotaBridge-Windows-x64` package to a permanent folder. In PowerShell, start it explicitly:

   ```powershell
   .\AIQuotaBridge.exe --allow-lan --host 192.168.1.20 --alias "Personal PC"
   ```

   Replace the host with the PC's private Wi-Fi IPv4 address. The bridge defaults to loopback-only unless `--allow-lan` is supplied. No firewall rules are added automatically; if Windows blocks access, allow this bridge only on the intended private network. Both devices must be on the same network with client isolation disabled.

4. Paste its `aqm1:` code in **Connections → Pair PC** on the phone. Treat the code as a secret: it grants read access to quota metadata. Manual code pairing is implemented; camera/QR pairing is deferred.
5. Use **Display → Show notch display**. Android asks for floating-window permission and, where applicable, notification permission. Declining overlay permission leaves the dashboard usable.
6. On HyperOS, inspect the app's floating-window and battery settings if the system removes the overlay. Labels vary by firmware. No auto-start/battery exemption is silently enabled. The pill does not automatically restart after reboot or a system kill; open the app and enable it again.

Tap a provider selector for its usage; choose **Open app** to launch it. Expanded-pill shortcuts select and open that app in one action. Claude Code itself is a desktop tool, so its shortcut launches Claude's Android app instead. Missing apps produce a message rather than opening an unknown URL.

To stop sharing, close the bridge console (Ctrl+C). To rotate credentials, run `AIQuotaBridge.exe --revoke`, restart it, and pair again. Revocation rejects subsequent requests even if an old bridge process is still running. Forgetting PCs on the phone removes local keys and cached data but does not revoke a copied code on the PC. The PC's bridge key and self-signed private certificate are stored under the current user's `%LOCALAPPDATA%\CodexUsageTracker\MobileBridge`; normal Windows profile permissions apply. Never copy this folder into the repository or app package.

## Scope and limitations

This first alpha reads the existing tracker database **read-only**. No provider login or new inference requests are made by the bridge. If the tracker stops or the PC sleeps, last-observed timestamps expose the stale state. There is no remote cloud relay, automatic account identification, Android home-screen widget, quota alert engine, or background bootstrap after reboot yet. Notifications currently serve only overlay control. The optional phone overlay polls about once per minute while the screen is on and stops polling while it is off; actual background lifetime remains controlled by Android/HyperOS.

Pairing is persistent and revocable, not a one-time enrollment exchange. The phone pins the bridge's exact certificate, verifies its validity, uses a private IPv4 endpoint, and refuses redirects. Do not forward the bridge port to the Internet. Play Store foreground-service policy review remains necessary before distribution through the store.

Cached readings are trimmed after seven days when the app syncs; disabled/closed apps do not run a continuous deletion job. Removing a pairing clears the local cache. No battery/performance claims are made until a Xiaomi 17 Pro hardware pass.

## Build

Use Android Studio with JDK 17 or newer, Android SDK **36.1**, build-tools **36.0.0**, and Gradle **9.3.1**. The project uses AGP **9.1.0** and its built-in Kotlin support, without a third-party UI library.

```powershell
.\build.ps1
```

Outputs are under `app/build/outputs/apk/`. `build.ps1 -Tests` additionally installs the app/test APKs on the selected ADB device and runs isolated on-device smoke tests. To exercise TLS pinning, authentication, and real quota parsing too, run the integration runner with `-e pairing <code>` from a loopback bridge configured with `--host 10.0.2.2` for the emulator. Do not publish the code or instrumentation command log.

The Windows bridge builds independently:

```powershell
dotnet publish ../src/CodexUsageTracker.Bridge/CodexUsageTracker.Bridge.csproj -c Release --self-contained true -r win-x64
```

The existing desktop app does not automatically start a network server. Provider adapters and mobile distribution are separate from the stable WPF build.

See [validation and remaining device checks](VALIDATION.md) for the exact testing scope. The alpha APK is about 2.5 MiB; keeping the UI native avoids a large cross-platform runtime.
