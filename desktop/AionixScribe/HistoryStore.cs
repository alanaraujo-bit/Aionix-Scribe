using System.IO;
using System.Text.Json;

namespace AionixScribe;

public sealed record HistoryEntry(string Id, DateTime TimestampUtc, string Text);

/// Histórico local de ditados bem-sucedidos. Armazenamento simples em JSON — não é um sistema de
/// documentos (§26: "o produto é um ditador inteligente, mantenha foco"), só o suficiente para o
/// usuário revisitar/copiar/excluir o que já falou.
public static class HistoryStore
{
    private const int MaxEntries = 200;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AionixScribe", "history.json");

    public static void Add(string text)
    {
        var entries = List().ToList();
        entries.Insert(0, new HistoryEntry(Guid.NewGuid().ToString("N"), DateTime.UtcNow, text));
        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }
        Save(entries);
    }

    public static IReadOnlyList<HistoryEntry> List()
    {
        try
        {
            if (!File.Exists(FilePath)) return Array.Empty<HistoryEntry>();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? new List<HistoryEntry>();
        }
        catch
        {
            return Array.Empty<HistoryEntry>(); // arquivo corrompido não deve derrubar o app
        }
    }

    public static void Delete(string id)
    {
        var entries = List().Where(e => e.Id != id).ToList();
        Save(entries);
    }

    public static void Clear() => Save(new List<HistoryEntry>());

    private static void Save(List<HistoryEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(entries));
    }
}
