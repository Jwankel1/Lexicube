# Lexicube

A networked daily five-letter word game in Blazor Server: accounts, one shared puzzle per day,
a live leaderboard, 3D spinning tiles, a win celebration and synthesized sound effects.

## Run it

Needs the .NET 10 SDK (the code only uses APIs that exist since .NET 8, so changing
`<TargetFramework>` in `Lexicube.csproj` to `net8.0` also works).

    dotnet run

Then open http://localhost:5080 — other devices on your network use `http://<this-pc's-ip>:5080`.

## Before sharing it

Edit `appsettings.json`:

| Setting | What it does |
| --- | --- |
| `Game:Seed` | Private phrase that decides the order of daily answers. **Change it**, then leave it alone (changing it later reshuffles future puzzles). |
| `Game:TimeZone` | When "today" rolls over for everyone. Default `America/New_York`. |
| `Game:FirstPuzzleDate` | The day that counts as Puzzle #1. |
| `Game:DataFile` | Where accounts and results are saved. Back this file up. |
| `Urls` | Address and port to listen on. |

For use beyond your home network, put it behind HTTPS (a reverse proxy such as Caddy, nginx or IIS),
because sign-in sends passwords to the server.

## How it works

- **Rules** – six guesses, five letters, guesses must be real words, and duplicate letters are scored
  the standard way (exact matches first, then leftovers). See `WordService.Score`.
- **Word lists** – `Data/answers.txt` (2,300 everyday words, no plurals or crude words) are the possible
  answers; `Data/valid.txt` (about 12,500) are accepted guesses. Both are plain text, one word per line – edit freely.
- **No peeking** – scoring happens on the server; the answer isn't sent to a browser until that player has finished.
- **Leaderboard** – today's solvers ranked by fewest guesses, then fastest time (the clock starts when you first
  open the day's puzzle). It updates live for everyone when someone finishes. There is also an all-time tab.
- **Accounts** – username + password (PBKDF2-SHA256 hashed), cookie sign-in, lockout after repeated failures.
- **Storage** – one JSON file via `Services/DataStore.cs`, no database to install. Fine for a family, club or office;
  for hundreds of daily players, replace that one class with a database.
- **Effects** – `wwwroot/app.css` (3D tiles, animations) and `wwwroot/lexi.js` (Web Audio sounds, confetti, keyboard).

## Layout

    Program.cs                    startup, sign-in cookie, sign-out endpoint
    Services/WordService.cs       word lists, daily answer, scoring
    Services/GameService.cs       game rules, leaderboard, stats
    Services/AuthService.cs       registration and login
    Services/DataStore.cs         JSON persistence
    Components/Pages/Home.razor   the game screen
    Components/Pages/Login.razor, Register.razor
