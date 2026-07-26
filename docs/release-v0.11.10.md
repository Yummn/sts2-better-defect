## BetterDefect v0.11.10

This release fixes card transformations overriding enchantment-owned card properties.

### Fix

- Transformations now establish the transformed base/upgrade values first.
- If the card has an enchantment, BetterDefect invokes the game's official `EnchantmentModel.ModifyCard()` refresh route after transformation normalization.
- Enchantments therefore remain the final modifier for energy cost and local keywords such as Exhaust, Ethereal, Innate, Retain and Eternal.
- The refresh is limited to BetterDefect transformation-eligible cards (plus Compact's generated Fuel), avoiding extra work on unrelated cards.

### Validation

- PC v0.107.1 live automated test:
  - A remove-Exhaust enchantment stayed effective after upgrading transformed Double Energy.
  - Official Tezcatara's Ember kept transformed Feral at zero cost after its normal upgrade.
- PC v0.107.1 and Android v0.103.2 compiled separately.
- Dual-binary offline audit: 187/187.

Use the platform-labelled archive. Do not mix the PC and Android DLLs.
