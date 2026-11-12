using System.Text.Json;

namespace Lexicube.Services;

/// <summary>
/// Small file-backed store: everything lives in memory and is flushed to one
/// JSON file (atomically) shortly after any change. Zero external dependencies,
/// and plenty for a family / office / club leaderboard. All access goes through
/// this class, so swapping in a database later only touches this file.
/// </summary>
public sealed class DataStore : IDisposable
{
    private sealed class Snapshot
    {
        public List<User> Users { get; set; } = new();
        public List<GameRecord> Games { get; set; } = new();
    }

    private readonly object _gate = new();
    private readonly string _path;
    private readonly ILogger<DataStore> _log;
    private readonly Dictionary<Guid, User> _users = new();
    private readonly Dictionary<string, User> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(Guid, string), GameRecord> _games = new();
    private readonly Timer _flushTimer;
    private bool _dirty;

    public DataStore(IConfiguration config, IWebHostEnvironment env, ILogger<DataStore> log)
    {
        _log = log;
        _path = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Game:DataFile"] ?? "App_Data/lexicube.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        if (File.Exists(_path))
        {
            var snap = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(_path)) ?? new Snapshot();
            foreach (var u in snap.Users) { _users[u.Id] = u; _byName[u.Username] = u; }
            foreach (var g in snap.Games) _games[(g.UserId, g.Date)] = g;
        }
        _log.LogInformation("Data file {Path}: {Users} users, {Games} games", _path, _users.Count, _games.Count);
        _flushTimer = new Timer(_ => Flush(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    /// <summary>Runs <paramref name="work"/> with exclusive access to the data.</summary>
    public T Read<T>(Func<DataStore, T> work) { lock (_gate) return work(this); }

    public T Write<T>(Func<DataStore, T> work)
    {
        lock (_gate) { var r = work(this); _dirty = true; return r; }
    }

    // ---- The members below must only be called from inside Read/Write. ----

    public User? FindUser(Guid id) => _users.GetValueOrDefault(id);
    public User? FindUser(string username) => _byName.GetValueOrDefault(username);
    public void AddUser(User u) { _users[u.Id] = u; _byName[u.Username] = u; }
    public GameRecord? FindGame(Guid userId, string date) => _games.GetValueOrDefault((userId, date));
    public void AddGame(GameRecord g) => _games[(g.UserId, g.Date)] = g;
    public IEnumerable<GameRecord> Games => _games.Values;

    public void Flush()
    {
        try
        {
            lock (_gate)
            {
                if (!_dirty) return;
                var snap = new Snapshot { Users = _users.Values.ToList(), Games = _games.Values.ToList() };
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(snap));
                File.Move(tmp, _path, overwrite: true);
                _dirty = false;
            }
        }
        catch (Exception ex) { _log.LogError(ex, "Could not save {Path}", _path); }
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        Flush();
    }
}
