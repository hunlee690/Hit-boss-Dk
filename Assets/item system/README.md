# Item system

- `ItemManager` owns the customization catalog, player ownership, purchases, and saved equipped items.
- `ShopManager` contains the small list of items offered for sale. Select an item, Coins or Gems, and its price in the Inspector.
- Every customization item stays visible. Locked items can be previewed but cannot be equipped.
- Add or rename an item's stable `Item ID` only before release. Saved ownership uses this ID.
- `SpinManager` supports any number of coin and gem spins. Each spin has its own cost, item rewards, and probability weights.
- Owned rewards are removed from the active wheel, so a spin never charges the player for a duplicate item.
