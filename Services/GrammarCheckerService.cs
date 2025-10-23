using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldModTranslate.Services
{
    public static class GrammarCheckerService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

        public static async Task<List<string>> CheckGrammarAsync(string text, string language)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return issues;

            var langCode = language.ToLowerInvariant() switch
            {
                "russian" or "ru" => "ru",
                _ => "en-US"
            };

            try
            {
                var response = await Http.PostAsync(
                    $"https://api.languagetool.org/v2/check",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["text"] = text,
                        ["language"] = langCode
                    }));

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);

                if (!doc.RootElement.TryGetProperty("matches", out var matches))
                    return issues;

                foreach (var m in matches.EnumerateArray())
                {
                    string? message = m.GetProperty("message").GetString();
                    string? context = m.TryGetProperty("context", out var ctxElem)
                        ? ctxElem.GetProperty("text").GetString()
                        : null;

                    string? replacement = null;
                    if (m.TryGetProperty("replacements", out var repls) && repls.GetArrayLength() > 0)
                    {
                        replacement = repls[0].GetProperty("value").GetString();
                    }

                    string? offsetStr = null;
                    if (m.TryGetProperty("offset", out var offset) && m.TryGetProperty("length", out var length))
                    {
                        try
                        {
                            var start = offset.GetInt32();
                            var len = length.GetInt32();
                            if (start + len <= text.Length)
                            {
                                offsetStr = text.Substring(start, len);
                            }
                        }
                        catch { }
                    }

                    // Build a human-readable message:
                    var msg = message ?? "Unknown issue";

                    if (!string.IsNullOrEmpty(offsetStr))
                        msg += $": “{offsetStr}”";
                    
                    if (!string.IsNullOrEmpty(context))
                        msg += $" ({context})";

                    if (!string.IsNullOrEmpty(replacement))
                        msg += $" → “{replacement}”";

                    issues.Add(msg);
                }
            }
            catch (Exception ex)
            {
                issues.Add($"Grammar check failed: {ex.Message}");
            }

            return issues;
        }
    }
}
