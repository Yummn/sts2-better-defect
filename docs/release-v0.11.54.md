# BetterDefect v0.11.54

## Changed

- The transformed **Hello World** still offers three random common Defect cards at turn start.
- The card-choice screen can now be skipped. Skipping adds no card and combat continues normally.

## Verification

- Android v110.1 live combat test observed a three-card common choice with `canSkip=true`.
- The automated selector skipped the choice and the hand remained unchanged (`5 -> 5`).
- Android v103 IL compatibility build was statically verified to pass `true` to `CardSelectCmd.FromChooseACardScreen`.
- Android v110.1 and PC v107.1 source builds completed successfully.
