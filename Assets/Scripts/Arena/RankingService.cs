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
    public int difficulty;
    public long timestamp;

    public ArenaDifficulty Difficulty => (ArenaDifficulty)Mathf.Clamp(difficulty, 0, 2);
    public DateTime LocalTime => timestamp <= 0
        ? DateTime.MinValue
        : DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
}

[Serializable]
public sealed class RankingFileData
{
    public List<RankingEntry> entries = new List<RankingEntry>();
}

/// <summary>
/// Ranking persistente (ranking.json em persistentDataPath) com top 5 por dificuldade.
/// Regras: apelido de exatamente 3 caracteres A–Z/0–9; uma entrada por apelido em cada
/// dificuldade; empate é resolvido pelo registro mais recente.
/// </summary>
public static class RankingService
{
    public enum SubmitStatus { InvalidNickname, Unchanged, Saved, Error }

    public const int MaximumEntries = 5;
    private static readonly Regex NicknamePattern = new Regex("^[A-Z0-9]{3}$", RegexOptions.CultureInvariant);
    private static string FilePath => Path.Combine(Application.persistentDataPath, "ranking.json");

    public static string RankingFilePath => FilePath;

    public static bool TryNormalizeNickname(string raw, out string normalized)
    {
        normalized = (raw ?? string.Empty).Trim().ToUpperInvariant();
        return NicknamePattern.IsMatch(normalized);
    }

    /// <summary>Top 5 de uma dificuldade. Em empate, o registro mais recente fica na frente.</summary>
    public static bool TryGetEntries(ArenaDifficulty difficulty, out List<RankingEntry> entries, out string error)
    {
        List<RankingEntry> all;
        if (!TryGetAllEntries(out all, out error)) { entries = new List<RankingEntry>(); return false; }
        entries = all.Where(e => e.Difficulty == difficulty).ToList();
        return true;
    }

    /// <summary>Todos os registros válidos (no máximo 5 por dificuldade).</summary>
    public static bool TryGetAllEntries(out List<RankingEntry> entries, out string error)
    {
        entries = new List<RankingEntry>();
        error = null;
        if (!File.Exists(FilePath)) return true;

        try
        {
            RankingFileData data = JsonUtility.FromJson<RankingFileData>(File.ReadAllText(FilePath));
            if (data == null || data.entries == null)
            { error = "O arquivo de ranking está inválido; ele foi preservado."; return false; }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (RankingEntry e in data.entries)
            {
                string normalized;
                // Dificuldade e data são tolerantes: arquivos antigos não tinham os campos.
                // Apelido e pontuação não são: são o conteúdo do ranking.
                if (e == null || !TryNormalizeNickname(e.nickname, out normalized) || e.score < 0)
                { error = "O arquivo de ranking contém dados inválidos; ele foi preservado."; entries.Clear(); return false; }

                e.nickname = normalized;
                // Arquivos antigos não tinham dificuldade nem timestamp: viram "Fácil", sem data.
                if (e.timestamp < 0) e.timestamp = 0;
                entries.Add(e);
            }

            entries = OrderAndTrim(entries);
            return true;
        }
        catch (Exception ex)
        {
            error = "Não foi possível ler o ranking: " + ex.Message;
            return false;
        }
    }

    /// <summary>Top 5 de todas as dificuldades juntas (apenas leitura/apresentação).</summary>
    public static bool TryGetTopEntriesOverall(out List<RankingEntry> entries, out string error)
    {
        bool ok = TryGetAllEntries(out entries, out error);
        if (ok) entries = entries.OrderByDescending(e => e.score).Take(MaximumEntries).ToList();
        return ok;
    }

    public static SubmitStatus Submit(string rawNickname, int score, ArenaDifficulty difficulty, out string message)
    {
        message = null;
        string nickname;
        if (!TryNormalizeNickname(rawNickname, out nickname) || score < 0)
        {
            message = "Informe um nome válido com exatamente 3 letras ou números (A–Z, 0–9).";
            return SubmitStatus.InvalidNickname;
        }

        List<RankingEntry> entries;
        if (!TryGetAllEntries(out entries, out message)) return SubmitStatus.Error;

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int sameNickname = entries.FindIndex(e => e.Difficulty == difficulty && e.nickname == nickname);

        if (sameNickname >= 0)
        {
            // Só substitui quando o jogador melhora o próprio recorde naquela dificuldade.
            if (score <= entries[sameNickname].score) return SubmitStatus.Unchanged;
            entries.RemoveAt(sameNickname);
        }

        entries.Add(new RankingEntry
        {
            nickname = nickname,
            score = score,
            difficulty = (int)difficulty,
            timestamp = timestamp
        });

        List<RankingEntry> ordered = OrderAndTrim(entries);
        bool included = ordered.Any(e => e.Difficulty == difficulty && e.nickname == nickname);
        if (!included) return SubmitStatus.Unchanged; // Fora do top 5: nada muda no arquivo.

        if (TryWrite(ordered, out message)) return SubmitStatus.Saved;
        return SubmitStatus.Error;
    }

    /// <summary>Remove um registro (útil para testes e para um futuro botão "limpar ranking").</summary>
    public static bool TryClear(out string message)
    {
        message = null;
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            return true;
        }
        catch (Exception ex)
        {
            message = "Não foi possível apagar o ranking: " + ex.Message;
            return false;
        }
    }

    private static List<RankingEntry> OrderAndTrim(List<RankingEntry> entries)
    {
        // Ordem: maior pontuação primeiro; em empate, o registro mais recente vence.
        // Depois disso, no máximo 5 por dificuldade.
        return entries
            .GroupBy(e => e.Difficulty)
            .SelectMany(group => group
                .OrderByDescending(e => e.score)
                .ThenByDescending(e => e.timestamp)
                .Take(MaximumEntries))
            .OrderByDescending(e => e.score)
            .ThenByDescending(e => e.timestamp)
            .ToList();
    }

    private static bool TryWrite(List<RankingEntry> ordered, out string message)
    {
        message = null;
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

            return true;
        }
        catch (Exception ex)
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            message = "Não foi possível salvar o ranking: " + ex.Message;
            return false;
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
