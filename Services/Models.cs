namespace Lexicube.Services;

public enum TileState { Absent = 0, Present = 1, Correct = 2 }

public sealed class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public sealed class GameRecord
{
    public Guid UserId { get; set; }
    /// <summary>Puzzle day, yyyy-MM-dd in the game's time zone.</summary>
    public string Date { get; set; } = "";
    public List<string> Guesses { get; set; } = new();
    public DateTime StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public bool Solved { get; set; }
}

public sealed record GuessView(string Word, TileState[] States);

/// <summary>What a player's browser session is allowed to know about their game.</summary>
public sealed record GameView(
    string Date,
    int PuzzleNumber,
    IReadOnlyList<GuessView> Guesses,
    bool Finished,
    bool Solved,
    string? Answer,          // only populated once the game is over
    DateTime StartedUtc,
    DateTime? FinishedUtc);

public sealed record SubmitResult(bool Ok, string? Error, GameView? Game);

public sealed record LeaderRow(int Rank, Guid UserId, string Username, int Guesses, TimeSpan Time);

public sealed record AllTimeRow(int Rank, Guid UserId, string Username, int Wins, int Played, double AvgGuesses);

public sealed record PlayerStats(int Played, int Wins, int CurrentStreak, int MaxStreak, int[] Distribution)
{
    public int WinPercent => Played == 0 ? 0 : (int)Math.Round(100.0 * Wins / Played);
}
