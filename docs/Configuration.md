# Configuration

## Ports and URLs
The development profile uses:
- API base URL: `http://localhost:5000`
- Scalar UI: `http://localhost:5000/scalar/v1`

See `GameServerApi/Properties/launchSettings.json`.

## JWT settings
The code currently uses hard-coded JWT values:
- `JwtService` has a constant secret key.
- `Program.cs` sets `ValidIssuer` and `ValidAudience` to `localhost:5000`.

If you want to change these, update both:
- `GameServerApi/Services/JwtService.cs`
- `GameServerApi/Program.cs`

Appsettings contains JWT values but they are not read in the current code.

## CORS
CORS is restricted to:
- `https://csharp.nouvet.fr`
- `http://localhost:3000`
- `http://localhost:5173`

Adjust in `GameServerApi/Program.cs`.

## Database
SQLite database file: `ProjectDB.db` (root of the repo).
Configured in `GameServerApi/Models/Data/ApplicationDbContext.cs`.
