# SignalR (ChatHub)

Hub URL: `/hub/chat`

Authentication: the server accepts JWT tokens in the query string for WebSocket connections:
`/hub/chat?access_token=<jwt>`

## Client -> Server methods
- `Login(int userId)`
  - Registers the connection for online tracking.
- `SendMessage(string user, string message)`
  - Broadcasts a chat message to all clients.

## Server -> Client events
- `UpdateUserCount(int count)`
  - Broadcasted on connect/disconnect and on `Login`.
- `ReceiveMessage(string user, string message)`
  - Chat messages and system announcements.
- `ScoreUpdate(int count)`
  - Sent to connected users during passive income distribution.
- `PlayerReset(string username, int previousCount)`
  - Broadcasted when a player resets.
- `NewHighScore(string username, int score)`
  - Broadcasted when a new high score is reached.

## Minimal JS example
```js
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5000/hub/chat?access_token=YOUR_JWT")
  .withAutomaticReconnect()
  .build();

connection.on("UpdateUserCount", count => console.log("online:", count));
connection.on("ReceiveMessage", (user, message) => console.log(user, message));
connection.on("ScoreUpdate", count => console.log("score:", count));
connection.on("PlayerReset", (user, score) => console.log("reset:", user, score));
connection.on("NewHighScore", (user, score) => console.log("highscore:", user, score));

await connection.start();
await connection.invoke("Login", 1);
await connection.invoke("SendMessage", "alice", "hello");
```
