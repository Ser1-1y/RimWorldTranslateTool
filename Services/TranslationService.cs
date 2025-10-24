using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace RimWorldModTranslate.Services
{
    public static class TranslationService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

        public static async Task<string?> TranslateTextAsync(string text, string? apiToken = null, string? fromLanguage = null, string? toLanguage = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Check internet connectivity first
            if (!await IsInternetAvailable())
            {
                throw new TranslationException("No internet connection available. Please check your network settings.");
            }

            // Use free token if no token provided
            var token = string.IsNullOrWhiteSpace(apiToken) ? "vcru" : apiToken;
            
            // Try multiple endpoints
            var endpoints = new[]
            {
                "https://apicase.ru/api/translate",
                "https://api.apicase.ru/translate",
                "http://apicase.ru/api/translate"
            };

            Exception? lastException = null;
            
            foreach (var endpoint in endpoints)
            {
                try
                {
                    var encodedText = HttpUtility.UrlEncode(text);
                    var url = $"{endpoint}?token={token}&text={encodedText}";
                    
                    // Add language parameters if provided
                    if (!string.IsNullOrWhiteSpace(fromLanguage))
                        url += $"&from={fromLanguage}";
                    if (!string.IsNullOrWhiteSpace(toLanguage))
                        url += $"&to={toLanguage}";

                    var response = await Http.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);

                    if (doc.RootElement.TryGetProperty("translated", out var translated) && 
                        translated.GetBoolean() &&
                        doc.RootElement.TryGetProperty("text", out var translatedText))
                    {
                        return translatedText.GetString();
                    }

                    return null;
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    // Try next endpoint
                    continue;
                }
                catch (JsonException ex)
                {
                    throw new TranslationException($"Invalid API response: {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    throw new TranslationException($"Translation failed: {ex.Message}", ex);
                }
            }

            // If all endpoints failed, try LibreTranslate as fallback
            try
            {
                return await TranslateWithLibreTranslate(text, fromLanguage, toLanguage);
            }
            catch (Exception libreEx)
            {
                // Try MyMemory API as last resort
                try
                {
                    return await TranslateWithMyMemory(text, fromLanguage, toLanguage);
                }
                catch (Exception myMemoryEx)
                {
                    throw new TranslationException($"All translation services failed. Apicase: {lastException?.Message}, LibreTranslate: {libreEx.Message}, MyMemory: {myMemoryEx.Message}", lastException ?? libreEx);
                }
            }
        }

        private static async Task<string?> TranslateWithLibreTranslate(string text, string? fromLanguage, string? toLanguage)
        {
            try
            {
                var encodedText = HttpUtility.UrlEncode(text);
                var from = fromLanguage ?? "en";
                var to = toLanguage ?? "ru";
                var url = $"https://libretranslate.com/translate?q={encodedText}&source={from}&target={to}&format=text";

                var response = await Http.PostAsync(url, null);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);

                if (doc.RootElement.TryGetProperty("translatedText", out var translatedText))
                {
                    return translatedText.GetString();
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new TranslationException($"LibreTranslate failed: {ex.Message}", ex);
            }
        }

        private static async Task<string?> TranslateWithMyMemory(string text, string? fromLanguage, string? toLanguage)
        {
            try
            {
                var encodedText = HttpUtility.UrlEncode(text);
                var from = fromLanguage ?? "en";
                var to = toLanguage ?? "ru";
                var url = $"https://api.mymemory.translated.net/get?q={encodedText}&langpair={from}|{to}";

                var response = await Http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);

                if (doc.RootElement.TryGetProperty("responseData", out var responseData) &&
                    responseData.TryGetProperty("translatedText", out var translatedText))
                {
                    var result = translatedText.GetString();
                    // MyMemory sometimes returns the original text if translation fails
                    return result != text ? result : null;
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new TranslationException($"MyMemory failed: {ex.Message}", ex);
            }
        }

        private static async Task<bool> IsInternetAvailable()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync("https://www.google.com");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Maps language names to ISO codes for Apicase API
        /// </summary>
        public static string GetLanguageCode(string languageName)
        {
            return languageName?.ToLowerInvariant() switch
            {
                "russian" or "ru" => "ru",
                "english" or "en" => "en",
                "french" or "fr" => "fr",
                "german" or "de" => "de",
                "spanish" or "es" => "es",
                _ => "en" // Default to English
            };
        }
    }

    public class TranslationException : Exception
    {
        public TranslationException(string message) : base(message) { }
        public TranslationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
