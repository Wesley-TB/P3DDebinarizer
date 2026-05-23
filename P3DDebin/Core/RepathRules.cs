using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace P3DDebin.Core;

// Lista reutilizavel de regras de troca de caminho.
// Formato do arquivo .txt (uma regra por linha):
//
//   caminho\antigo => caminho\novo           (prefixo, literal)
//   regex: ^z\\(old)\\ => z\\new\\           (regex case-insensitive, suporta $1, $2...)
//
// Linhas vazias e iniciadas por # sao ignoradas.
public static class RepathRules
{
    private const string Separator   = "=>";
    private const string RegexPrefix = "regex:";

    public sealed record Rule(string From, string To, bool IsRegex);

    public static IReadOnlyList<Rule> Load(string path)
    {
        var rules = new List<Rule>();

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            bool isRegex = false;
            if (line.StartsWith(RegexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                isRegex = true;
                line = line[RegexPrefix.Length..].TrimStart();
            }

            int sep = line.IndexOf(Separator, StringComparison.Ordinal);
            if (sep < 0)
                continue;

            string from = line[..sep].Trim();
            string to   = line[(sep + Separator.Length)..].Trim();

            if (!isRegex)
            {
                from = from.TrimStart('\\');
                to   = to.TrimStart('\\');
            }

            if (from.Length > 0)
                rules.Add(new Rule(from, to, isRegex));
        }

        return rules;
    }

    public static void Save(string path, IEnumerable<Rule> rules)
    {
        var lines = new List<string>
        {
            "# Lista de troca de caminhos - P3D Debinarizer",
            "# Literal:  caminho\\antigo => caminho\\novo",
            "# Regex  :  regex: ^z\\\\(old)\\\\ => z\\\\new\\\\",
            string.Empty
        };

        lines.AddRange(rules.Select(r =>
            r.IsRegex
                ? $"{RegexPrefix} {r.From} {Separator} {r.To}"
                : $"{r.From} {Separator} {r.To}"));

        File.WriteAllLines(path, lines);
    }

    // Expande regras (literal ou regex) contra os caminhos reais, gerando os
    // pares exatos (caminho antigo -> caminho novo) a aplicar.
    // A primeira regra que casa em cada caminho vence (mesma semantica antiga).
    public static List<(string From, string To)> Expand(
        IEnumerable<Rule> rules,
        IEnumerable<string> paths)
    {
        var ruleList = rules.ToList();
        var result = new List<(string, string)>();

        foreach (string original in paths)
        {
            foreach (Rule rule in ruleList)
            {
                if (TryApply(rule, original, out string updated)
                    && !string.Equals(updated, original, StringComparison.Ordinal))
                {
                    result.Add((original, updated));
                    break;
                }
            }
        }

        return result;
    }

    // Aplica uma regra a um caminho. Retorna true se houve match (mesmo que o
    // resultado seja identico ao input — o caller filtra mudancas reais).
    public static bool TryApply(Rule rule, string input, out string output)
    {
        if (rule.IsRegex)
        {
            try
            {
                var re = new Regex(rule.From, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!re.IsMatch(input))
                {
                    output = input;
                    return false;
                }
                output = re.Replace(input, rule.To);
                return true;
            }
            catch (ArgumentException)
            {
                // Padrao regex invalido: trata como no-op.
                output = input;
                return false;
            }
        }

        if (input.StartsWith(rule.From, StringComparison.OrdinalIgnoreCase))
        {
            output = rule.To + input[rule.From.Length..];
            return true;
        }

        output = input;
        return false;
    }
}
