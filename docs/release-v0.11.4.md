# BetterDefect v0.11.4

## Compatibility

- Android: Slay the Spire 2 v0.103.2
- PC: Slay the Spire 2 v0.107.1
- BaseLib is not required

## Changes

- Fixes the remaining random Android ARM64 startup crash caused by a dense sequence of native Harmony detours.
- Adds a detour-free card-play bridge to the companion v103 APK. BetterDefect registers one dispatcher for transformed and historical card effects instead of patching each card's `OnPlay`.
- Installs the remaining 34 Android Harmony classes one at a time with a 750 ms initial delay and 250 ms interval.
- Repairs the Android Harmony compatibility router's static initialization order.
- Keeps saved card-art choices, disabled states, dynamic odds, transformation choices and SpireBank/game saves intact.

## Verification

- PC v0.107.1 build: succeeded.
- Android v0.103.2 build: succeeded.
- Offline source/registry/behavior audit: 170/170 passed.
- REDMI K80 Pro repeated cold-start test: 5/5 reached the main menu, 34/34 patch classes completed, no SIGSEGV/SIGABRT.
- Final packaged v0.11.4 was installed and cold-started once more successfully.

## Android installation

1. Install `Slay-the-Spire-2-v0.103.2-Android-Harmony-Stable-Bridge.apk` with `adb install -r`.
2. Import or copy `更好的故障机器人-v0.11.4-手机-v103.zip`.
3. Do not uninstall the game before updating, because uninstalling clears internal saves and MOD settings.
