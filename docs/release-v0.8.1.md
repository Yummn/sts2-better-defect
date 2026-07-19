# BetterDefect v0.8.1

This release audits card text against the actual PC v107.1 and mobile v103 behavior routes.

## Fixed

- Rocket Punch text now follows the v0.100 historical switch.
- Tesla Coil no longer previews two Lightning triggers unless its v0.105 switch is active.
- Fuel no longer displays “draw 0 cards” under Compact's v0.108 behavior.
- Scrape explains its different local/final energy-cost checks.
- Fission displays only its current remove-or-evoke behavior.
- Core Surge and Fission no longer duplicate the engine-generated Exhaust line.
- Amplify now actually expires at the end of the current player turn and consumes stacks through the native card-play-count hook.

## Verification

- 26 restored registrations and 14 historical-version mappings audited.
- Offline source/behavior suite: 66 passed, 0 failed.
- PC v107.1 build: 0 errors.
- Android/mobile v103 build: 0 errors.
- Both DLLs decompiled and checked for the dynamic descriptions and Amplify expiry route.
- Steam was not launched or modified during this offline pass.

Use `BetterDefect-v0.8.1.zip` for mobile v103 and `BetterDefect-v0.8.1-PC-v107.1.zip` for PC v107.1.
