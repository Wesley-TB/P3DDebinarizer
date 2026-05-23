using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace P3DDebin.Core;

// Lista reutilizavel de regras de troca de caminho (prefixo).
// Formato do arquivo .txt:  caminho\antigo => caminho\novo   (uma por linha,
// linhas vazias e iniciadas por # sao ignoradas).
public static class RepathRules
{
    private const string Separator = "=>";

    public static IReadOnlyList<(string From, string To)> Load(string path)
    {
        var rules = new List<(string, string)>();

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            int sep = line.IndexOf(Separator, StringComparison.Ordinal);
            if (sep < 0)
                continue;

            string from = line[..sep].Trim().TrimStart('\\');
            string to   = line[(sep + Separator.Length)..].Trim().TrimStart('\\');

            if (from.Length > 0)
                rules.Add((from, to));
        }

        return rules;
    }

    public static void Save(string path, IEnumerable<(string From, string To)> rules)
    {
        var lines = new List<string>
        {
            "# Lista de troca de caminhos - P3D Debinarizer",
            "# Formato:  caminho\\antigo => caminho\\novo",
            string.Empty
        };

        lines.AddRange(rules.Select(r => $"{r.From} {Separator} {r.To}"));
        File.WriteAllLines(path, lines);
    }

    // Expande regras de prefixo contra os caminhos reais de um modelo,
    // gerando os pares exatos (caminho antigo -> caminho novo) a aplicar.
    public static List<(string From, string To)> Expand(
        IEnumerable<(string From, string To)> rules,
        IEnumerable<string> paths)
    {
        var ruleList = rules.ToList();
        var result = new List<(string, string)>();

        foreach (string original in paths)
        {
            foreach (var (from, to) in ruleList)
            {
                if (original.StartsWith(from, StringComparison.OrdinalIgnoreCase))
                {
                    string updated = to + original[from.Length..];
                    if (!string.Equals(updated, original, StringComparison.Ordinal))
                        result.Add((original, updated));
                    break; // primeira regra que casa vence
                }
            }
        }

        return result;
    }
}
