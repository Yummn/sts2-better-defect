# BetterDefect v0.11.4 Android startup audit

Date: 2026-07-24  
Device: REDMI K80 Pro  
Game: Android v0.103.2

## Result

| Run | Process alive | Patch queue | Main menu | Fatal signal |
|---:|:---:|:---:|:---:|:---:|
| 1 | yes | 34/34 | yes | none |
| 2 | yes | 34/34 | yes | none |
| 3 | yes | 34/34 | yes | none |
| 4 | yes | 34/34 | yes | none |
| 5 | yes | 34/34 | yes | none |

A final v0.11.4 build was then installed and cold-started again. It reached the main menu in 18.6 seconds, registered the core card-play bridge, completed 34/34 patch classes and remained alive.

## Relevant hashes

- Mobile BetterDefect DLL: `2AA797607D002BE58930B1DFEB0192FA6C36527256BFC6494E2AA823EDC90928`
- Mobile archive: `4A5964A240A2167DED58FC55A5FFA026694E782E4076C3EAFD8FB19AB9A939BD`
- PC archive: `26B936EE528F4CCEDBAF6A64EBDA3CD59D2D669753726CE30C01893B78689AC0`
- Stable-bridge APK: `9232EF40069398233FA5285D67F6CE80DFD7CC563E630492462766132311404E`

## Offline behavior audit

`tests/verify_better_defect_offline.py` passed 170/170 checks. This audit verifies restored-card registration, transformation routes, historical card versions, descriptions, encyclopedia UI scoping, persistent state, power icons and Android-specific hook selection.
