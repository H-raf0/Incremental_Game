# Database

This project uses Entity Framework Core with SQLite.

## Migrations
Create a migration:
```bash
dotnet ef migrations add InitialisationDeLaDB --project GameServerApi
```

Apply migrations:
```bash
dotnet ef database update --project GameServerApi
```

Drop database:
```bash
dotnet ef database drop --force --project GameServerApi
```

## Seeding items
Seed items from `GameServerApi/items.json`:
```bash
curl http://localhost:5000/api/Inventory/Seed
```
