# API Reference

Base URL (dev): `http://localhost:5000`

All responses use JSON. Errors follow this shape:

```json
{
  "message": "Human readable error",
  "code": "ERROR_CODE"
}
```

## Auth
- `POST /api/User/Register` and `POST /api/User/Login` return `{ token, user }`.
- Use the token on protected routes: `Authorization: Bearer <token>`.

## Rate limiting
- `fixed`: global limiter for Register/Login (10000 req / 10s total).
- `perUser`: per-user limiter for Click/Buy (10 req / 10s per user).

## User endpoints

### Register
`POST /api/User/Register` (anonymous, rate limited)

Request:
```json
{
  "username": "alice",
  "password": "secret123"
}
```

Response:
```json
{
  "token": "<jwt>",
  "user": { "id": 1, "username": "alice", "role": 0 }
}
```

### Login
`POST /api/User/Login` (anonymous, rate limited)

Request:
```json
{
  "username": "alice",
  "password": "secret123"
}
```

Response:
```json
{
  "token": "<jwt>",
  "user": { "id": 1, "username": "alice", "role": 0 }
}
```

### Logout
`POST /api/User/Logout` (auth required)

Response:
```json
{ "message": "Logged out successfully" }
```

### Get all users (public)
`GET /api/User/All` (anonymous)

Response:
```json
[
  { "id": 1, "username": "alice", "role": 0 }
]
```

### Get user by id
`GET /api/User/{id}` (auth required)

### Search users
`GET /api/User/Search/{name}` (auth required)

### Get all admins
`GET /api/User/AllAdmin` (auth required, admin only)

### Update user
`PUT /api/User/{id}` (auth required, admin only)

Request:
```json
{
  "username": "newname",
  "password": "newpass123",
  "role": 1
}
```

### Delete user
`DELETE /api/User/{id}` (auth required, admin only)


## Game endpoints

### Initialize progression
`GET /api/Game/Initialize` (auth required)

Response:
```json
{ "id": 1, "userId": 1, "count": 0, "multiplier": 1, "bestScore": 0, "totalClickValue": 0 }
```

### Get progression
`GET /api/Game/Progression` (auth required)

### Click
`GET /api/Game/Click` (auth required, rate limited)

Response:
```json
{ "count": 123, "multiplier": 2 }
```

### Reset cost
`GET /api/Game/ResetCost` (auth required)

Response:
```json
{ "cost": 100 }
```

### Reset
`POST /api/Game/Reset` (auth required)

### Best score
`GET /api/Game/BestScore` (auth required)

Response:
```json
{ "userId": 5, "bestScore": 9999 }
```


## Inventory endpoints

### Seed items
`GET /api/Inventory/Seed` (anonymous)

### List items
`GET /api/Inventory/Items` (anonymous)

Response:
```json
[
  { "id": 1, "name": "Cursor", "price": 10, "maxQuantity": 100, "clickValue": 1 }
]
```

### Buy item
`POST /api/Inventory/Buy/{itemId}` (auth required, rate limited)

Response:
```json
{ "id": 7, "userId": 1, "itemId": 2, "quantity": 3 }
```

### User inventory
`GET /api/Inventory/UserInventory` (auth required)

Response:
```json
[
  { "id": 7, "userId": 1, "itemId": 2, "quantity": 3 }
]
```
