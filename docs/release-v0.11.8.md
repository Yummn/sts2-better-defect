# BetterDefect v0.11.8

Fixes transformed Iteration leaving a transparent, unselectable card or stale hand slot on slower Android devices.

## Changes

- Waits until the full nested draw lifecycle has completed before exhausting the first Status card.
- Waits one Godot process frame after the exhaust animation before returning from the draw command.
- Cleans only stale or empty hand holders after that frame, leaving every valid hand card and its artwork untouched.
- Updates startup diagnostics and offline regression coverage.

## Validation

- PC v0.107.1 live test passed: Dazed exhausted, extra draw completed, immediate and settled hand state both contained 7 models / 7 holders / 7 active holders, with 0 ghost holders and 0 Dazed visual nodes; a subsequent Zap resolved.
- Android v0.103.2 binary compiled separately against v103 references.
- Offline audit: 178/178.

## Assets

- `更好的故障机器人-v0.11.8-手机-v103.zip` — Android game v0.103.x.
- `更好的故障机器人-v0.11.8-电脑-v107.1.zip` — PC game v0.107.1.
