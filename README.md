# Incremental Game

## 1. Project Overview

**Incremental Game** is a backend driven incremental game project developed with **ASP.NET Core**.  
Players generate progression through clicks, unlock upgrades, earn achievements, and reset their progression to gain long term advantages.

The project focuses on **game state management**, **incremental mechanics**, and **clean backend architecture**.

### Purpose & Main Features
- Incremental progression based on user actions.
- Upgrade and multiplier system to accelerate progress.
- Achievement tracking based on player milestones.
- Persistent game state stored on the server.
- REST API designed to be consumed by a frontend client.

### Target Audience / Use Case
- Students learning ASP.NET Core and Web API architecture.
- Developers interested in incremental game mechanics.
- Educational project demonstrating clean backend design.

---

## 2. Project Architecture

The application follows a **layered and modular architecture**, where each module has a clear responsibility.

### Main Components

- **UI Layer (External Frontend):**
  External client responsible for user interactions such as clicks, resets, and upgrade purchases.

- **Game Logic:**  
  Central orchestration layer that manages the game loop, progression rules, and interactions between subsystems.

- **Resource System:**  
  Handles resource accumulation, spending, and progression values.

- **Upgrade System:**  
  Manages upgrades that affect gameplay, such as multipliers and efficiency boosts.

- **Achievement System:**  
  Tracks milestones and unlocks achievements based on player actions.

- **Persistence:**  
  Responsible for saving and loading player progression and state.

---

## 3. Core Domain Model

The core domain consists of several high-level classes:

- **Game:** The main controller that manages the game loop and coordinates all subsystems.
- **ResourceManager:** Handles resource creation, accumulation, and spending.
- **UpgradeManager:** Controls upgrade availability and application.
- **AchievementManager:** Tracks and unlocks achievements.
- **Resource, Upgrade, Achievement:** Represent individual resources, upgrades, and achievements.

---
## 4. Game Logic and Usage Workflow

The gameplay is based on incremental mechanics where user actions and background systems
work together to increase player progression.

- The player interacts with the game through the frontend (click actions, upgrades, resets).
- Each click increases the player’s progression score.
- Rate limiting prevents excessive clicking and ensures fair progression.
- Passive income periodically increases the score, even without user interaction.
- Upgrades improve progression efficiency and resource generation.
- Achievements are unlocked when specific milestones are reached.
- Players can reset their progression to gain long-term benefits.
- Game state and progression are persisted on the server and automatically restored.

## 5. Video Demonstration

A video demonstration of the Incremental Game can be found at the following link:
[Watch the video demo](https://drive.google.com/drive/u/2/folders/1H2B4-ieyk8vUOfzr60JzK3XHozkGOb9S)

---

## 6. How to Build & Compile the Project

### Prerequisites

- .NET SDK 7.0 or later
- Modern web browser (for frontend testing)

### Build Instructions

1. **Clone the repository:**
   ```bash
   git clone https://github.com/H-raf0/Incremental_Game.git
   cd Incremental_Game
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Build the project (if applicable):**
   ```bash
   dotnet build
   ```
## Database Management 

This project uses Entity Framework Core for database management.

1. **Create a new migration:**
    ```bash
    dotnet ef migrations add InitialisationDeLaDB
    ```
2. **Apply migrations to the database**
    ```bash
    dotnet ef database update
    ```

3. **Drop the database**
    ```bash
    dotnet ef database drop --force
    ```
---

## 7. How to Run and Use the Project

### Running the API

1. **Start the development server:**
   ```bash
   dotnet run
   ```

2. **Access the game:**
   - Open your browser and navigate to `http://localhost:5000`.

---

## 8. Project Structure

```
Incremental_Game/
├── GameServerApi/
├── GameServerApi.Tests/
├── .tools/
├── coveragereport/
├── class-diagram.png
├── IncrementalGame.sln
└── README.md

```
## Contributors

[BAANI Maroia](https://github.com/briw4),  [ISMAILI M'HAMDI Mouad](https://github.com/mouadismaili),   [EL ALLALI Achraf](https://github.com/H-raf0)
