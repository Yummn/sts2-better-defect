using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using System.Reflection;
using System.Text.Json;

namespace BetterDefect;

/// <summary>
/// BetterDefect-only persistence for encyclopedia card transformations.
/// Dynamic reward odds and disabled-card state intentionally live in the
/// standalone DynamicCardOdds mod from v0.11.35 onward.
/// </summary>
internal static class BdCardUpgradeState
{
    public const int NormalPointLimit = 25;
    public const int OverclockPointLimit = 35;
    public const int MaxCardPointBudget = 50;

    private const string RuntimeFolderName = "BetterDefect";
    private const string StateFileName = "BetterDefect.CardUpgrades.state.dat";
    private const string LegacyStateFileName = "BetterDefect.DynamicOdds.weights.dat";
    private const string LegacyJsonFileName = "BetterDefect.DynamicOdds.weights.json";
    private const string RemovedAmplifyId = "CARD.BD_AMPLIFY";

    private static readonly object StateLock = new();
    private static readonly Dictionary<Type, string> CardKeyByType = new();
    private static UpgradeState? _state;

    public static void InitializeStorage()
    {
        lock (StateLock)
        {
            _state = LoadState();
            _state.Normalize();
            SaveState(_state);
            MainFile.Logger.Info($"[BetterDefect] card-transformation state ready: {GetStatePath()}; enabled={_state.UpgradedCards.Count}/{MaxCardPointBudget}.");
        }
    }

    public static int GetVersionUpgradeCount()
    {
        lock (StateLock)
        {
            var state = _state ??= LoadState();
            state.Normalize();
            return state.UpgradedCards.Count;
        }
    }

    public static int GetUsedCardPointCount() => GetVersionUpgradeCount();

    public static bool IsCardVersionUpgraded(CardModel? card)
    {
        if (card is null || !BdCardVersionUpgrades.IsEligible(card))
            return false;

        try
        {
            var key = CardKey(card);
            lock (StateLock)
            {
                var state = _state ??= LoadState();
                return state.UpgradedCards.Contains(key, StringComparer.Ordinal);
            }
        }
        catch { return false; }
    }

    public static bool ToggleCardVersionUpgrade(CardModel? card)
    {
        if (card is null || !BdCardVersionUpgrades.IsEligible(card))
            return false;

        try
        {
            var key = CardKey(card);
            lock (StateLock)
            {
                var state = _state ??= LoadState();
                state.Normalize();
                var enabled = state.UpgradedCards.Contains(key, StringComparer.Ordinal);
                if (enabled)
                {
                    state.UpgradedCards.RemoveAll(value => string.Equals(value, key, StringComparison.Ordinal));
                    MainFile.Logger.Info($"[BetterDefect] card transformation disabled for {SafeId(card)}; one point refunded.");
                }
                else
                {
                    if (state.UpgradedCards.Count >= MaxCardPointBudget)
                    {
                        MainFile.Logger.Warn($"[BetterDefect] cannot transform {SafeId(card)}: point budget is full ({state.UpgradedCards.Count}/{MaxCardPointBudget}).");
                        return false;
                    }

                    state.UpgradedCards.Add(key);
                    MainFile.Logger.Info($"[BetterDefect] card transformation enabled for {SafeId(card)}; points={state.UpgradedCards.Count}/{MaxCardPointBudget}.");
                }

                state.LastUpdatedUtc = DateTimeOffset.UtcNow;
                SaveState(state);
                return true;
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to toggle card transformation: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static UpgradeState LoadState()
    {
        var path = GetStatePath();
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<UpgradeState>(File.ReadAllText(path), JsonOptions) ?? new UpgradeState();
                loaded.Normalize();
                return loaded;
            }

            foreach (var legacyPath in GetLegacyStatePaths())
            {
                if (!File.Exists(legacyPath))
                    continue;

                using var document = JsonDocument.Parse(File.ReadAllText(legacyPath));
                var migrated = new UpgradeState();
                if (TryGetProperty(document.RootElement, "UpgradedCards", out var cards) && cards.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in cards.EnumerateArray())
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                            migrated.UpgradedCards.Add(value);
                    }
                }

                migrated.MigratedFromDynamicOdds = true;
                migrated.Normalize();
                SaveState(migrated);
                MainFile.Logger.Info($"[BetterDefect] migrated card transformations without disabled/odds data: {legacyPath} -> {path}; enabled={migrated.UpgradedCards.Count}.");
                return migrated;
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to load card-transformation state '{path}': {ex.GetType().Name}: {ex.Message}");
        }

        return new UpgradeState();
    }

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static void SaveState(UpgradeState state)
    {
        try
        {
            state.Normalize();
            var path = GetStatePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonOptions));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to save card-transformation state: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static IEnumerable<string> GetLegacyStatePaths()
    {
        var result = new List<string>();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || result.Contains(path, StringComparer.OrdinalIgnoreCase))
                return;
            result.Add(path);
        }

        try
        {
            var data = GetDataDirectory();
            Add(Path.Combine(data, LegacyStateFileName));
            Add(Path.Combine(data, LegacyJsonFileName));
        }
        catch { }
        try
        {
            var mod = GetModDirectory();
            Add(Path.Combine(mod, LegacyStateFileName));
            Add(Path.Combine(mod, LegacyJsonFileName));
        }
        catch { }
        return result;
    }

    private static string GetStatePath() => Path.Combine(GetDataDirectory(), StateFileName);

    private static string GetDataDirectory()
    {
        try
        {
            var userData = OS.GetUserDataDir();
            if (!string.IsNullOrWhiteSpace(userData))
                return Path.Combine(userData, RuntimeFolderName);
        }
        catch { }

        try
        {
            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
                return Path.Combine(appData, "SlayTheSpire2", RuntimeFolderName);
        }
        catch { }

        return Path.Combine(GetModDirectory(), "Data", "Runtime");
    }

    private static string GetModDirectory()
    {
        try
        {
            var location = Assembly.GetExecutingAssembly().Location;
            var directory = string.IsNullOrWhiteSpace(location) ? null : Path.GetDirectoryName(location);
            if (!string.IsNullOrWhiteSpace(directory))
                return directory;
        }
        catch { }
        return AppContext.BaseDirectory;
    }

    private static string CardKey(CardModel card)
    {
        var type = card.GetType();
        lock (CardKeyByType)
        {
            if (CardKeyByType.TryGetValue(type, out var cached))
                return cached;
        }

        string key;
        try { key = card.CanonicalInstance.Id.ToString(); }
        catch
        {
            try { key = card.Id.ToString(); }
            catch { key = type.FullName ?? type.Name; }
        }

        lock (CardKeyByType)
            CardKeyByType[type] = key;
        return key;
    }

    private static string SafeId(CardModel card)
    {
        try { return card.Id.ToString(); }
        catch { return card.GetType().Name; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private sealed class UpgradeState
    {
        public int Version { get; set; } = 1;
        public List<string> UpgradedCards { get; set; } = new();
        public bool MigratedFromDynamicOdds { get; set; }
        public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

        public void Normalize()
        {
            Version = Math.Max(Version, 1);
            UpgradedCards ??= new List<string>();
            UpgradedCards = UpgradedCards
                .Where(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, RemovedAmplifyId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Take(MaxCardPointBudget)
                .ToList();
        }
    }
}
