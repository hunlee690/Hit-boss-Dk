# Item system

- `ItemManager` owns the customization catalog, player ownership, purchases, and saved equipped items.
- `ShopManager` contains the small list of items offered for sale. Select an item, Coins or Gems, and its price in the Inspector.
- Every customization item stays visible. Locked items can be previewed but cannot be equipped.
- Add or rename an item's stable `Item ID` only before release. Saved ownership uses this ID.
- `SpinManager` supports any number of coin and gem spins. Each spin has its own cost, item rewards, and probability weights.
- Owned item rewards stay visible. If one wins, its Inspector-configured coin compensation is awarded instead.
- Spin rewards can be customization items, coins, or gems. The wheel creates one labelled section for every currently eligible reward.
- Coin-spin and gem-spin sections show horizontal spin cards in the shop; clicking a card opens that exact wheel.
- Use `Main Menu Spin Buttons` on the Spin Manager Inspector to assign the Daily Spin and Free Gem Spin buttons.
