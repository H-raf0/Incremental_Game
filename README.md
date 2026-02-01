# Incremental Game

Backend d'un jeu incremental en ASP.NET Core. Les joueurs progressent via des clics, des upgrades, des achievements et des resets, avec un etat persistant cote serveur.

## Table des matieres
- [Apercu](#apercu)
- [Architecture](#architecture)
- [Demarrage rapide](#demarrage-rapide)
- [Configuration](#configuration)
- [Authentification](#authentification)
- [Documentation API et SignalR](#documentation-api-et-signalr)
- [Gameplay et progression](#gameplay-et-progression)
- [Base de donnees](#base-de-donnees)
- [Tests](#tests)
- [Architecture (schema)](#architecture-schema)
- [Structure du projet](#structure-du-projet)
- [Video demo](#video-demo)
- [Contributeurs](#contributeurs)

## Apercu
Le projet met l'accent sur la gestion d'etat, les mecaniques incrementales et une architecture backend claire.

Fonctionnalites principales :
- Progression par clic avec rate limiting.
- Revenu passif periodique.
- Systemes d'upgrades et multiplicateurs.
- Achievements bases sur des paliers.
- Persistance serveur.
- API REST consommee par un client frontend.

## Architecture
Architecture modulaire avec responsabilites claires :
- **UI (Frontend externe)** : clics, resets, achats.
- **Game Logic** : boucle de jeu et regles de progression.
- **Resource/Inventory** : gestion des objets et bonus.
- **Achievement System** : suivi des paliers.
- **Persistence** : sauvegarde et restauration.
- **SignalR** : chat et evenements temps reel.

## Demarrage rapide
Prerequis :
- .NET SDK 7.0+

Build :
```bash
dotnet restore
dotnet build
```

Run :
```bash
dotnet run --project GameServerApi
```

Ensuite : `http://localhost:5000`

## Configuration
Details complets dans `docs/Configuration.md`.

Points importants :
- Le JWT est configure en dur dans `GameServerApi/Services/JwtService.cs` et `GameServerApi/Program.cs`.
- CORS est limite a `https://csharp.nouvet.fr`, `http://localhost:3000`, `http://localhost:5173`.
- SQLite utilise `ProjectDB.db` a la racine.

## Authentification
1. `POST /api/User/Register` ou `POST /api/User/Login`.
2. Recuperer le `token` JWT.
3. Ajouter `Authorization: Bearer <token>` sur les routes protegees.

## Documentation API et SignalR
- API complete : `docs/API.md`
- SignalR (ChatHub) : `docs/SignalR.md`

## Gameplay et progression
- Regles de progression, formules, reset, items, revenu passif : `docs/Gameplay.md`

## Base de donnees
- Migrations EF Core, seed items : `docs/Database.md`

## Tests
- Lancer les tests : `docs/Testing.md`
- Pour lancer les tests en collectant les données de couverture, utilisez l’option `--collect` :
  ```bash
  dotnet test --collect:"XPlat Code Coverage"
  ```
-   Pour générer le rapport :
    ```bash
    reportgenerator -reports:TestResults/**/coverage.cobertura.xml -targetdir:coveragereport -reporttypes:Html
    ```

  Rapport HTML : `GameServerApi.Tests/coveragereport/index.html`

## Architecture (schema)
- Schema d'architecture : `docs/Architecture.md`

## Structure du projet
```
Incremental_Game/
├── GameServerApi/
├── GameServerApi.Tests/
├── docs/
├── coveragereport/
├── class-diagram.png
├── IncrementalGame.sln
└── README.md
```

## Video demo
[Watch the video demo](https://drive.google.com/file/d/16LysV0LNoWsGCyy_PP92Z3JF8Q2hkecP/view?usp=sharing)

## Contributeurs
[BAANI Maroia](https://github.com/briw4), [ISMAILI M'HAMDI Mouad](https://github.com/mouadismaili), [EL ALLALI Achraf](https://github.com/H-raf0)
