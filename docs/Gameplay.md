# Gameplay and Progression

## Progression fields
`Progression` contains:
- `Count`: current score.
- `Multiplier`: reset multiplier (starts at 1).
- `BestScore`: best historical score.
- `TotalClickValue`: sum of item click bonuses.

## Click formula
Each click updates the score with:
```
newCount = Count + Multiplier + TotalClickValue
```
The value is clamped to `int.MaxValue` and never goes below 0.

## Reset
- Cost is exponential based on multiplier:
```
resetCost = floor(100 * 1.5^(Multiplier - 1))
```
- If `Count < resetCost`, reset is denied.
- On reset:
  - `Count = 0`
  - `TotalClickValue = 0`
  - `Multiplier += 1`
  - All inventory entries are deleted
  - `BestScore` is updated if previous `Count` was higher
- A `PlayerReset` SignalR event is broadcasted with the previous score.

## Passive income
Every 30 seconds, all players get `+1` point (`Count += 1`).
Online users receive a `ScoreUpdate` event with their new score.

## Items and inventory
- Items are seeded from `GameServerApi/items.json`.
- Purchase rules:
  - You must have enough `Count` to pay `Price`.
  - `Quantity` cannot exceed `MaxQuantity`.
  - On purchase: `Count -= Price`, `TotalClickValue += ClickValue`.
- Items with `Price > 10000` trigger a system announcement in chat.
- Note: `Skynet` has a negative `ClickValue` (-1), which reduces the click bonus.
