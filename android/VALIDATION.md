# Android alpha validation

18 October 2026. This records local checks, not Xiaomi certification or a Play Store release.

## Environment

- Android SDK 36.1 emulator, 1080 × 2400, 420 dpi. No physical Xiaomi 17 Pro was attached.
- Windows bridge built with .NET SDK 8.0.425. Native Kotlin app built with AGP 9.1.0 / Gradle 9.3.1.
- Bridge integration uses loopback HTTPS and the emulator's `10.0.2.2` host route. No LAN firewall exception was created.

## Checked

- Debug app and test APKs compile; Android lint completes with **zero errors**. Remaining warnings include English-only UI strings, custom-view allocation/tooling hints, newer dependency availability, and the deliberately scoped certificate-pinning trust manager. These are not suppressed globally.
- Real Codex quota/history metadata travels from the Windows database through HTTPS to the phone dashboard.
- Wrong certificate fingerprints and bearer keys are rejected. Unknown or expired quotas display N/A, and stale/future-dated observations are not presented as fresh.
- Pairing secrets survive an Android Keystore encrypt/decrypt round trip, and plaintext bearer keys are absent from the stored preferences value.
- Instrumentation uses isolated test preferences. An explicitly requested emulator-only path also exercises the real Connections form.
- Installing an updated APK retains the paired PC profile. Pairing is committed before success is shown.
- Overlay permission granted on the emulator; the in-app switch starts a real foreground service and draws a compact pill. Tap expands it; the provider shortcut changes selection and handles a missing app gracefully. Long press stops the overlay.
- Quick Settings tile starts and stops the notch while the app is already open. Service state is checked with bounded waits for Android startup/shutdown.
- Revoking a bridge key immediately rejects the old key on the still-running server (HTTP 401).
- Light/dark dashboard and Display screen inspected visually. Standard system bars remain readable in both themes.
- The bridge reads the existing database in read-only mode. The stable desktop app and its startup registration are unchanged.

## Still required on the Xiaomi 17 Pro

- Real HyperOS floating-window permissions, cutout position, notification controls, and battery/background behavior.
- Installed ChatGPT, Claude, and Gemini launch behavior on the phone's firmware.
- Notification/Quick Settings behavior after lock/unlock, battery saver, and process termination.
- Battery consumption during an extended session, accessibility font sizes, rotation, and network interruptions.
- Other manufacturers, Google Play foreground-service review, remote syncing, and production release signing.

The app is a sideloadable debug alpha. Its build is signed using the local Android debug keystore. CI artifacts use their own debug key; they cannot update a differently signed local installation in place. Production signing and a stable release-update channel remain future work.
