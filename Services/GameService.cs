namespace Lexicube.Services;

/// <summary>
/// All game rules run here, on the server. The browser never receives the
/// answer until the player's game is over, so it can't be read out of the page.
/// </summary>
public sealed class GameService(WordService words, DataStore store)
{
    /// <summary>Raised whenever someone finishes a puzzle, so open leaderboards refresh live.</summary>
    public event Action? LeaderboardChanged;

    public bool UserExists(Guid userId) => store.Read(s => s.FindUser(userId) is not null);

    /// <summary>Returns today's game for the player, creating it (and starting the clock) on first visit.</summary>
    public GameView GetOrStart(Guid userId)
    {
        var day = words.Today;
        var key = WordService.Key(day);
        var rec = store.Read(s => s.FindGame(userId, key));
        if (rec is null)
        {
            rec = store.Write(s =>
            {
                var existing = s.FindGame(userId, key);
                if (existing is not null) return existing;
                var g = new GameRecord { UserId = userId, Date = key, StartedUtc = DateTime.UtcNow };
                s.AddGame(g);
                return g;
            });
        }
        return store.Read(_ => View(rec, day));
    }

    public SubmitResult Submit(Guid userId, string puzzleDate, string guess)
    {
        guess = (guess ?? "").Trim().ToUpperInvariant();
        var day = words.Today;
        var key = WordService.Key(day);

        if (puzzleDate != key) return new(false, "A new puzzle is out — reloading.", null);
        if (guess.Length != WordService.WordLength || guess.Any(c => c is < 'A' or > 'Z'))
            return new(false, "Not enough letters", null);
        if (!words.IsValidGuess(guess)) return new(false, "Not in word list", null);

        var answer = words.AnswerFor(day);
        var finishedNow = false;

        var result = store.Write(s =>
        {
            var rec = s.FindGame(userId, key);
            if (rec is null) return new SubmitResult(false, "Game not found — reloading.", null);
            if (rec.FinishedUtc is not null) return new SubmitResult(false, "You've already finished today's puzzle.", null);

            rec.Guesses.Add(guess);
            if (guess == answer) rec.Solved = true;
            if (rec.Solved || rec.Guesses.Count >= WordService.MaxGuesses)
            {
                rec.FinishedUtc = DateTime.UtcNow;
                finishedNow = true;
            }
            return new SubmitResult(true, null, View(rec, day));
        });

        if (finishedNow)
        {
            store.Flush();
            LeaderboardChanged?.Invoke();
        }
        return result;
    }

    private GameView View(GameRecord rec, DateOnly day)
    {
        var answer = words.AnswerFor(day);
        var finished = rec.FinishedUtc is not null;
        return new GameView(
            rec.Date,
            words.PuzzleNumber(day),
            rec.Guesses.Select(g => new GuessView(g, WordService.Score(g, answer))).ToList(),
            finished,
            rec.Solved,
            finished ? answer : null,
            rec.StartedUtc,
            rec.FinishedUtc);
    }

    /// <summary>Today's solvers: fewest guesses first, fastest time breaks ties.</summary>
    public (IReadOnlyList<LeaderRow> Rows, int PlayersToday) TodayBoard(int take = 25)
    {
        var key = WordService.Key(words.Today);
        return store.Read(s =>
        {
            var todays = s.Games.Where(g => g.Date == key).ToList();
            var rows = todays
                .Where(g => g.Solved && g.FinishedUtc is not null)
                .Select(g => (g, time: g.FinishedUtc!.Value - g.StartedUtc))
                .OrderBy(x => x.g.Guesses.Count).ThenBy(x => x.time).ThenBy(x => x.g.FinishedUtc)
                .Take(take)
                .Select((x, i) => new LeaderRow(i + 1, x.g.UserId, s.FindUser(x.g.UserId)?.Username ?? "?", x.g.Guesses.Count, x.time))
                .ToList();
            return ((IReadOnlyList<LeaderRow>)rows, todays.Count(g => g.Guesses.Count > 0));
        });
    }

    /// <summary>All-time: most wins first, lower average guesses breaks ties.</summary>
    public IReadOnlyList<AllTimeRow> AllTimeBoard(int take = 25) =>
        store.Read(s => s.Games
            .Where(g => g.FinishedUtc is not null)
            .GroupBy(g => g.UserId)
            .Select(grp =>
            {
                var wins = grp.Where(g => g.Solved).ToList();
                return (id: grp.Key, wins: wins.Count, played: grp.Count(),
                        avg: wins.Count == 0 ? 0 : wins.Average(g => g.Guesses.Count));
            })
            .Where(x => x.wins > 0)
            .OrderByDescending(x => x.wins).ThenBy(x => x.avg)
            .Take(take)
            .Select((x, i) => new AllTimeRow(i + 1, x.id, s.FindUser(x.id)?.Username ?? "?", x.wins, x.played, x.avg))
            .ToList());

    public PlayerStats StatsFor(Guid userId)
    {
        var today = words.Today;
        return store.Read(s =>
        {
            var done = s.Games
                .Where(g => g.UserId == userId && g.FinishedUtc is not null)
                .ToDictionary(g => DateOnly.ParseExact(g.Date, "yyyy-MM-dd"));

            var dist = new int[WordService.MaxGuesses];
            foreach (var g in done.Values.Where(g => g.Solved)) dist[g.Guesses.Count - 1]++;

            // Longest run of consecutive solved days.
            int max = 0, run = 0;
            DateOnly? prev = null;
            foreach (var d in done.Keys.Order())
            {
                if (!done[d].Solved) { run = 0; prev = d; continue; }
                run = prev is not null && prev.Value.AddDays(1) == d && run > 0 ? run + 1 : 1;
                max = Math.Max(max, run);
                prev = d;
            }

            // Current streak: counts back from today, or from yesterday if today isn't finished yet.
            var cursor = done.ContainsKey(today) ? today : today.AddDays(-1);
            var current = 0;
            while (done.TryGetValue(cursor, out var g) && g.Solved) { current++; cursor = cursor.AddDays(-1); }

            return new PlayerStats(done.Count, done.Values.Count(g => g.Solved), current, max, dist);
        });
    }
}
