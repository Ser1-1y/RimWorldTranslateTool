using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace RimWorldModTranslate.Services
{
    public enum TranslationProvider
    {
        Apicase,
        GoogleTranslate,
        DeepL,
        Yandex,
        LibreTranslate,
        MyMemory
    }

    public class TranslationRequest
    {
        public string Text { get; set; } = "";
        public string FromLanguage { get; set; } = "en";
        public string ToLanguage { get; set; } = "ru";
        public TranslationProvider Provider { get; set; } = TranslationProvider.Apicase;
        public string? ApiKey { get; set; }
    }

    public class TranslationResponse
    {
        public bool Success { get; set; }
        public string? TranslatedText { get; set; }
        public string? ErrorMessage { get; set; }
        public TranslationProvider Provider { get; set; }
    }

    public static class UnifiedTranslationService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

        public static async Task<TranslationResponse> TranslateAsync(TranslationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = "Text to translate cannot be empty"
                };
            }

            // Check internet connectivity first
            if (!await IsInternetAvailable())
            {
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = "No internet connection available. Please check your network settings."
                };
            }

            // Try the requested provider first
            var response = await TryProvider(request);
            if (response.Success)
            {
                return response;
            }

            // If the requested provider fails, try fallback providers
            var fallbackProviders = GetFallbackProviders(request.Provider);
            foreach (var provider in fallbackProviders)
            {
                var fallbackRequest = new TranslationRequest
                {
                    Text = request.Text,
                    FromLanguage = request.FromLanguage,
                    ToLanguage = request.ToLanguage,
                    Provider = provider,
                    ApiKey = request.ApiKey
                };
                var fallbackResponse = await TryProvider(fallbackRequest);
                if (fallbackResponse.Success)
                {
                    return fallbackResponse;
                }
            }

            return new TranslationResponse
            {
                Success = false,
                ErrorMessage = "All translation providers failed. Please check your internet connection and API keys."
            };
        }

        private static async Task<TranslationResponse> TryProvider(TranslationRequest request)
        {
            try
            {
                return request.Provider switch
                {
                    TranslationProvider.Apicase => await TranslateWithApicase(request),
                    TranslationProvider.GoogleTranslate => await TranslateWithGoogle(request),
                    TranslationProvider.DeepL => await TranslateWithDeepL(request),
                    TranslationProvider.Yandex => await TranslateWithYandex(request),
                    TranslationProvider.LibreTranslate => await TranslateWithLibreTranslate(request),
                    TranslationProvider.MyMemory => await TranslateWithMyMemory(request),
                    _ => new TranslationResponse { Success = false, ErrorMessage = "Unknown provider" }
                };
            }
            catch (Exception ex)
            {
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = $"{request.Provider} failed: {ex.Message}",
                    Provider = request.Provider
                };
            }
        }

        private static async Task<TranslationResponse> TranslateWithApicase(TranslationRequest request)
        {
            var endpoints = new[]
            {
                "https://apicase.ru/api/translate",
                "https://api.apicase.ru/translate",
                "http://apicase.ru/api/translate"
            };

            var token = string.IsNullOrWhiteSpace(request.ApiKey) ? "vcru" : request.ApiKey;

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var encodedText = HttpUtility.UrlEncode(request.Text);
                    var url = $"{endpoint}?token={token}&text={encodedText}";
                    
                    if (!string.IsNullOrWhiteSpace(request.FromLanguage))
                    {
                        url += $"&from={request.FromLanguage}";
                    }
                    if (!string.IsNullOrWhiteSpace(request.ToLanguage))
                    {
                        url += $"&to={request.ToLanguage}";
                    }

                    var response = await Http.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);

                    if (doc.RootElement.TryGetProperty("translated", out var translated) && 
                        translated.GetBoolean() &&
                        doc.RootElement.TryGetProperty("text", out var translatedText))
                    {
                        return new TranslationResponse
                        {
                            Success = true,
                            TranslatedText = translatedText.GetString(),
                            Provider = TranslationProvider.Apicase
                        };
                    }
                }
                catch (HttpRequestException)
                {
                    // Try next endpoint
                    continue;
                }
            }

            return new TranslationResponse
            {
                Success = false,
                ErrorMessage = "Apicase API failed on all endpoints",
                Provider = TranslationProvider.Apicase
            };
        }

        private static async Task<TranslationResponse> TranslateWithGoogle(TranslationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = "Google Translate API key is required",
                    Provider = TranslationProvider.GoogleTranslate
                };
            }

            var url = "https://translation.googleapis.com/language/translate/v2";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("key", request.ApiKey),
                new KeyValuePair<string, string>("q", request.Text),
                new KeyValuePair<string, string>("source", request.FromLanguage),
                new KeyValuePair<string, string>("target", request.ToLanguage),
                new KeyValuePair<string, string>("format", "text")
            });

            var response = await Http.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("translations", out var translations) &&
                translations.GetArrayLength() > 0)
            {
                var translation = translations[0];
                if (translation.TryGetProperty("translatedText", out var translatedText))
                {
                    return new TranslationResponse
                    {
                        Success = true,
                        TranslatedText = translatedText.GetString(),
                        Provider = TranslationProvider.GoogleTranslate
                    };
                }
            }

            return new TranslationResponse
            {
                Success = false,
                ErrorMessage = "Google Translate API returned invalid response",
                Provider = TranslationProvider.GoogleTranslate
            };
        }

        private static async Task<TranslationResponse> TranslateWithDeepL(TranslationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = "DeepL API key is required",
                    Provider = TranslationProvider.DeepL
                };
            }

            var url = "https://api-free.deepl.com/v2/translate";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("auth_key", request.ApiKey),
                new KeyValuePair<string, string>("text", request.Text),
                new KeyValuePair<string, string>("source_lang", request.FromLanguage.ToUpper()),
                new KeyValuePair<string, string>("target_lang", request.ToLanguage.ToUpper())
            });

            var response = await Http.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            if (doc.RootElement.TryGetProperty("translations", out var translations) &&
                translations.GetArrayLength() > 0)
            {
                var translation = translations[0];
                if (translation.TryGetProperty("text", out var translatedText))
                {
                    return new TranslationResponse
                    {
                        Success = true,
                        TranslatedText = translatedText.GetString(),
                        Provider = TranslationProvider.DeepL
                    };
                }
            }

            return new TranslationResponse
            {
                Success = false,
                ErrorMessage = "DeepL API returned invalid response",
                Provider = TranslationProvider.DeepL
            };
        }

        private static async Task<TranslationResponse> TranslateWithYandex(TranslationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = "Yandex API key is required",
                    Provider = TranslationProvider.Yandex
                };
            }

            var url = "https://translate.yandex.net/api/v1.5/tr.json/translate";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("key", request.ApiKey),
                new KeyValuePair<string, string>("text", request.Text),
                new KeyValuePair<string, string>("lang", $"{request.FromLanguage}-{request.ToLanguage}")
            });

            var response = await Http.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            if (doc.RootElement.TryGetProperty("text", out var textArray) &&
                textArray.GetArrayLength() > 0)
            {
                return new TranslationResponse
                {
                    Success = true,
                    TranslatedText = textArray[0].GetString(),
                    Provider = TranslationProvider.Yandex
                };
            }

            return new TranslationResponse
            {
                Success = false,
                ErrorMessage = "Yandex API returned invalid response",
                Provider = TranslationProvider.Yandex
            };
        }

        private static async Task<TranslationResponse> TranslateWithLibreTranslate(TranslationRequest request)
        {
            var encodedText = HttpUtility.UrlEncode(request.Text);
            var url = $"https://libretranslate.com/translate?q={encodedText}&source={request.FromLanguage}&target={request.ToLanguage}&format=text";

            var response = await Http.PostAsync(url, null);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            if (doc.RootElement.TryGetProperty("translatedText", out var translatedText))
            {
                return new TranslationResponse
                {
                    Success = true,
                    TranslatedText = translatedText.GetString(),
                    Provider = TranslationProvider.LibreTranslate
                };
            }

            return new TranslationResponse
            {
                Success = false,
                ErrorMessage = "LibreTranslate API returned invalid response",
                Provider = TranslationProvider.LibreTranslate
            };
        }

        private static async Task<TranslationResponse> TranslateWithMyMemory(TranslationRequest request)
        {
            var encodedText = HttpUtility.UrlEncode(request.Text);
            var url = $"https://api.mymemory.translated.net/get?q={encodedText}&langpair={request.FromLanguage}|{request.ToLanguage}";

            var response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            if (doc.RootElement.TryGetProperty("responseData", out var responseData) &&
                responseData.TryGetProperty("translatedText", out var translatedText))
            {
                var result = translatedText.GetString();
                // MyMemory sometimes returns the original text if translation fails
                if (result != request.Text)
                {
                    return new TranslationResponse
                    {
                        Success = true,
                        TranslatedText = result,
                        Provider = TranslationProvider.MyMemory
                    };
                }
            }

            return new TranslationResponse
            {
                Success = false,
                ErrorMessage = "MyMemory API returned invalid response",
                Provider = TranslationProvider.MyMemory
            };
        }

        private static List<TranslationProvider> GetFallbackProviders(TranslationProvider primary)
        {
            return primary switch
            {
                TranslationProvider.Apicase => new List<TranslationProvider> 
                { 
                    TranslationProvider.LibreTranslate, 
                    TranslationProvider.MyMemory 
                },
                TranslationProvider.GoogleTranslate => new List<TranslationProvider> 
                { 
                    TranslationProvider.DeepL, 
                    TranslationProvider.LibreTranslate, 
                    TranslationProvider.MyMemory 
                },
                TranslationProvider.DeepL => new List<TranslationProvider> 
                { 
                    TranslationProvider.GoogleTranslate, 
                    TranslationProvider.LibreTranslate, 
                    TranslationProvider.MyMemory 
                },
                TranslationProvider.Yandex => new List<TranslationProvider> 
                { 
                    TranslationProvider.LibreTranslate, 
                    TranslationProvider.MyMemory 
                },
                _ => new List<TranslationProvider> 
                { 
                    TranslationProvider.Apicase, 
                    TranslationProvider.LibreTranslate, 
                    TranslationProvider.MyMemory 
                }
            };
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
        /// Maps language names to ISO codes for various APIs
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
                "chinese" or "zh" => "zh",
                "japanese" or "ja" => "ja",
                "korean" or "ko" => "ko",
                "portuguese" or "pt" => "pt",
                "italian" or "it" => "it",
                "dutch" or "nl" => "nl",
                "polish" or "pl" => "pl",
                "czech" or "cs" => "cs",
                "hungarian" or "hu" => "hu",
                "romanian" or "ro" => "ro",
                "bulgarian" or "bg" => "bg",
                "croatian" or "hr" => "hr",
                "slovak" or "sk" => "sk",
                "slovenian" or "sl" => "sl",
                "estonian" or "et" => "et",
                "latvian" or "lv" => "lv",
                "lithuanian" or "lt" => "lt",
                "finnish" or "fi" => "fi",
                "swedish" or "sv" => "sv",
                "norwegian" or "no" => "no",
                "danish" or "da" => "da",
                "ukrainian" or "uk" => "uk",
                "belarusian" or "be" => "be",
                "turkish" or "tr" => "tr",
                "arabic" or "ar" => "ar",
                "hebrew" or "he" => "he",
                "hindi" or "hi" => "hi",
                "thai" or "th" => "th",
                "vietnamese" or "vi" => "vi",
                "indonesian" or "id" => "id",
                "malay" or "ms" => "ms",
                "tagalog" or "tl" => "tl",
                _ => "en" // Default to English
            };
        }

        /// <summary>
        /// Gets available translation providers
        /// </summary>
        public static List<TranslationProvider> GetAvailableProviders()
        {
            return new List<TranslationProvider>
            {
                TranslationProvider.Apicase,
                TranslationProvider.GoogleTranslate,
                TranslationProvider.DeepL,
                TranslationProvider.Yandex,
                TranslationProvider.LibreTranslate,
                TranslationProvider.MyMemory
            };
        }
    }
}
