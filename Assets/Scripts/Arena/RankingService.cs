using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public sealed class RankingEntry
{
    public string nickname;
    public int score;
}

[Serializable]
public sealed class RankingFileData
{
    public List<RankingEntry> entries = new List<RankingEntry>();
}

/// <summary>Persistência isolada do save didático. Empates preservam a ordem já gravada.</summary>
public static class RankingService
{
    public enum SubmitStatus { InvalidNickname, Unchanged, Saved, Error }
    public const int MaximumEntries = 5;
    private static readonly Regex NicknamePattern = new Regex("^[A-Z0-9]{3}$", RegexOptions.CultureInvariant);
    private static string FilePath => Path.Combine(Application.persistentDataPath, "ranking.json");

    public static bool TryNormalizeNickname(string raw, out string normalized)
    {
        normalized = (raw ?? string.Empty).Trim().ToUpperInvariant();
        return NicknamePattern.IsMatch(normalized);
    }

    public static bool TryGetEntries(out List<RankingEntry> entries, out string error)
    {
        entries = new List<RankingEntry>();
        error = null;
        if (!File.Exists(FilePath)) return true;
        try
        {
            RankingFileData data = JsonUtility.FromJson<RankingFileData>(File.ReadAllText(FilePath));
            if (data == null || data.entries == null) { error = "O arquivo de ranking está inválido; ele foi preservado."; return false; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (RankingEntry e in data.entries)
            {
                string normalized;
                if (e == null || !TryNormalizeNickname(e.nickname, out normalized) || !seen.Add(normalized) || e.score < 0)
                { error = "O arquivo de ranking contém dados inválidos; ele foi preservado."; entries.Clear(); return false; }
                entries.Add(new RankingEntry { nickname = normalized, score = e.score });
            }
            entries = entries.OrderByDescending(e => e.score).Take(MaximumEntries).ToList();
            return true;
        }
        catch (Exception ex) { error = "Não foi possível ler o ranking: " + ex.Message; return false; }
    }

    public static SubmitStatus Submit(string rawNickname, int score, out string message)
    {
        message = null;
        string nickname;
        if (!TryNormalizeNickname(rawNickname, out nickname) || score < 0)
        { message = "Informe um nome válido com exatamente 3 letras ou números (A–Z, 0–9)."; return SubmitStatus.InvalidNickname; }
        List<RankingEntry> entries;
        if (!TryGetEntries(out entries, out message)) return SubmitStatus.Error;

        int existing = entries.FindIndex(e => e.nickname == nickname);
        if (existing >= 0)
        {
            if (score <= entries[existing].score) return SubmitStatus.Unchanged;
            entries.RemoveAt(existing);
        }
        entries.Add(new RankingEntry { nickname = nickname, score = score });
        // LINQ OrderBy é estável: em empates, as entradas previamente armazenadas vêm antes.
        List<RankingEntry> ordered = entries.OrderByDescending(e => e.score).Take(MaximumEntries).ToList();
        bool included = ordered.Any(e => e.nickname == nickname);
        if (!included) return SubmitStatus.Unchanged;

        string temp = FilePath + ".tmp";
        string backup = FilePath + ".bak";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(temp, JsonUtility.ToJson(new RankingFileData { entries = ordered }, true));
            if (File.Exists(FilePath))
            {
                try { File.Replace(temp, FilePath, backup, true); if (File.Exists(backup)) File.Delete(backup); }
                catch (PlatformNotSupportedException) { ReplaceWithBackup(temp, backup); }
                catch (IOException) { ReplaceWithBackup(temp, backup); }
            }
            else File.Move(temp, FilePath);
            return SubmitStatus.Saved;
        }
        catch (Exception ex)
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            message = "Não foi possível salvar o ranking: " + ex.Message;
            return SubmitStatus.Error;
        }
    }

    private static void ReplaceWithBackup(string temp, string backup)
    {
        if (File.Exists(backup)) File.Delete(backup);
        File.Move(FilePath, backup);
        try { File.Move(temp, FilePath); File.Delete(backup); }
        catch { if (!File.Exists(FilePath) && File.Exists(backup)) File.Move(backup, FilePath); throw; }
    }
}
