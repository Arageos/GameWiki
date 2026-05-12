using System.Text.Json.Serialization;

namespace GameWiki.Services
{
    public class RawgGameListItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Released { get; set; }
        public List<RawgGenre> Genres { get; set; }
        public List<RawgPlatformWrapper> Platforms { get; set; }

        [JsonPropertyName("background_image")]
        public string BackgroundImage { get; set; }
    }

    public class RawgGameDetails
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Released { get; set; }

        [JsonPropertyName("description_raw")]
        public string DescriptionRaw { get; set; }

        [JsonPropertyName("background_image")]
        public string BackgroundImage { get; set; }

        public double Rating { get; set; }

        [JsonPropertyName("ratings_count")]
        public int RatingsCount { get; set; }

        public List<RawgGenre> Genres { get; set; }
        public List<RawgPlatformWrapper> Platforms { get; set; }
    }

    public class RawgGenre
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class RawgPlatformWrapper
    {
        public RawgPlatformInfo Platform { get; set; }
    }

    public class RawgPlatformInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class RawgListResponse
    {
        public List<RawgGameListItem> Results { get; set; }
        public string Next { get; set; }
    }

    public class RawgService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public RawgService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _apiKey = config["Rawg:ApiKey"];
        }

        public async Task<RawgListResponse> GetGamesAsync(int page = 1, int pageSize = 20, string search = "")
        {
            var url = $"https://api.rawg.io/api/games?key={_apiKey}&page={page}&page_size={pageSize}";
            if (!string.IsNullOrEmpty(search))
                url += $"&search={Uri.EscapeDataString(search)}";

            return await _http.GetFromJsonAsync<RawgListResponse>(url);
        }

        public async Task<RawgGameDetails> GetGameDetailsAsync(int rawgId)
        {
            var url = $"https://api.rawg.io/api/games/{rawgId}?key={_apiKey}";
            return await _http.GetFromJsonAsync<RawgGameDetails>(url);
        }
    }
}