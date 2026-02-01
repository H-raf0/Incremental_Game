# Architecture

## Vue d'ensemble (Mermaid)
```mermaid
flowchart LR
  Client[Frontend Client]
  API[ASP.NET Core API]
  Auth[Auth/JWT]
  Game[GameService]
  Inv[InventoryService]
  User[UserService]
  Passive[PassiveIncomeService]
  SignalR[SignalR Hub]
  DB[(SQLite DB)]
  Items[items.json]

  Client -->|HTTP REST| API
  Client -->|WebSocket| SignalR

  API --> Auth
  API --> Game
  API --> Inv
  API --> User

  Game --> DB
  Inv --> DB
  User --> DB
  Passive --> DB
  Inv --> Items

  Passive --> SignalR
  Game --> SignalR
  Inv --> SignalR
  SignalR --> Client
```

## Flux principal (resume)
- Auth: Register/Login -> JWT -> routes protegees.
- Gameplay: Click/Reset/BestScore via `GameService`.
- Inventaire: achats via `InventoryService` + `items.json`.
- Temps reel: notifications via SignalR (chat, high score, reset, score update).
