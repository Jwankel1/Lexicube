using System.Security.Cryptography;
using System.Text;

namespace Lexicube.Services;

/// <summary>
/// Owns the word lists and decides which word is the answer on a given day.
/// The answer order is a shuffle keyed by a private seed, so knowing the
/// (public) answer list doesn't reveal tomorrow's word.
/// </summary>
public sealed class WordService
{
    public const int WordLength = 5;
    public const int MaxGuesses = 6;

    private readonly string[] _schedule;
    private readonly HashSet<string> _valid;
    private readonly TimeZoneInfo _zone;
    private readonly DateOnly _firstDay;

    public WordService(IConfiguration config, IWebHostEnvironment env, ILogger<WordService> log)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dir)) dir = Path.Combine(env.ContentRootPath, "Data");

        var answers = Load(Path.Combine(dir, "answers.txt"));
        _valid = new HashSet<string>(Load(Path.Combine(dir, "valid.txt")));
        foreach (var a in answers) _valid.Add(a);

        var seed = config["Game:Seed"] ?? "lexicube";
        _schedule = answers
            .OrderBy(w => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed + "|" + w))), StringComparer.Ordinal)
            .ToArray();

        try { _zone = TimeZoneInfo.FindSystemTimeZoneById(config["Game:TimeZone"] ?? "America/New_York"); }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Unknown Game:TimeZone, falling back to UTC");
            _zone = TimeZoneInfo.Utc;
        }

        _firstDay = DateOnly.TryParse(config["Game:FirstPuzzleDate"], out var d) ? d : new DateOnly(2026, 1, 1);
        log.LogInformation("Loaded {Answers} answers and {Valid} valid guesses", answers.Length, _valid.Count);
    }

    private static string[] Load(string path) =>
        File.ReadAllLines(path)
            .Select(l => l.Trim().ToUpperInvariant())
            .Where(l => l.Length == WordLength && l.All(c => c is >= 'A' and <= 'Z'))
            .Distinct()
            .ToArray();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone));

    public static string Key(DateOnly day) => day.ToString("yyyy-MM-dd");

    public int PuzzleNumber(DateOnly day) => day.DayNumber - _firstDay.DayNumber + 1;

    public string AnswerFor(DateOnly day)
    {
        var n = day.DayNumber - _firstDay.DayNumber;
        var i = ((n % _schedule.Length) + _schedule.Length) % _schedule.Length;
        return _schedule[i];
    }

    public bool IsValidGuess(string word) => _valid.Contains(word);

    /// <summary>UTC instant at which the next puzzle unlocks.</summary>
    public DateTime NextPuzzleUtc()
    {
        var midnight = Today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(midnight, _zone);
    }

    /// <summary>
    /// Standard two-pass scoring: exact matches claim their letters first, then
    /// remaining letters are marked "present" only while unclaimed copies remain.
    /// </summary>
    public static TileState[] Score(string guess, string answer)
    {
        var result = new TileState[WordLength];
        var remaining = new int[26];

        for (var i = 0; i < WordLength; i++)
        {
            if (guess[i] == answer[i]) result[i] = TileState.Correct;
            else remaining[answer[i] - 'A']++;
        }
        for (var i = 0; i < WordLength; i++)
        {
            if (result[i] == TileState.Correct) continue;
            var k = guess[i] - 'A';
            if (remaining[k] > 0) { result[i] = TileState.Present; remaining[k]--; }
        }
        return result;
    }
}
