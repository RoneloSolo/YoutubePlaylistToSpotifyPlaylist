using System;
using System.Linq;
using System.Collections.Generic;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System.Threading.Tasks;
using System.Diagnostics;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;

namespace YTtoSP {
    internal class Program {

        private const string SPOTIFY_CLIENT_ID = "";
        private const string USER_ID = "";
        private const string YOUTUBE_KEY = "";
        private const string YOUTUBE_PLAYLIST_ID = "";
        private const string WEBBROWSER_DIR = "";
        private const string NEW_PLAYLIST_NAME = "";

        private static EmbedIOAuthServer _server = new EmbedIOAuthServer(new Uri("http://localhost:8888/callback"), 8888);

        public static async Task Main() {
            await _server.Start();

            _server.ImplictGrantReceived += OnImplicitGrantReceived;
            _server.ErrorReceived += OnErrorReceived;

            var request = new LoginRequest(_server.BaseUri, SPOTIFY_CLIENT_ID, LoginRequest.ResponseType.Token) {
                Scope = new List<string> { Scopes.UserReadEmail, Scopes.PlaylistModifyPublic }
            };
            Process.Start(WEBBROWSER_DIR,request.ToUri().ToString());
            Console.ReadKey();
        }

        // Got verification.
        private static async Task OnImplicitGrantReceived(object sender, ImplictGrantResponse response) {
            await _server.Stop();
            var spotify = new SpotifyClient(response.AccessToken);
            var playlistName = new SpotifyAPI.Web.FullPlaylist();
            try {
                var tracksToAdd = new List<string>();
                var playlistCreateRequest = new PlaylistCreateRequest(NEW_PLAYLIST_NAME);
                var playlist = await spotify.Playlists.Create(USER_ID, playlistCreateRequest);

                var youtubeVideos = await GetYoutubePlaylist(YOUTUBE_PLAYLIST_ID , int.MaxValue);
                foreach (var video in youtubeVideos) {
                    var searchRequest = new SearchRequest(SearchRequest.Types.Track, video) {
                        Limit = 1
                    };

                    var searchResult = await spotify.Search.Item(searchRequest);

                    if (searchResult.Tracks.Total > 0) {
                        tracksToAdd.Add(searchResult.Tracks.Items[0].Uri);
                        Console.WriteLine($"Adding: {searchResult.Tracks.Items[0].Uri}");
                    }

                    if(tracksToAdd.Count >= 99) {
                        var _playlistAddItemsRequest = new PlaylistAddItemsRequest(tracksToAdd);
                        Console.WriteLine("Adding Items");
                        await spotify.Playlists.AddItems(playlist.Id, _playlistAddItemsRequest);
                        tracksToAdd.Clear();
                    }
                }
                
                var playlistAddItemsRequest = new PlaylistAddItemsRequest(tracksToAdd);
                Console.WriteLine("Adding Items");
                await spotify.Playlists.AddItems(playlist.Id, playlistAddItemsRequest);
            }
            catch(APIException e) { Console.WriteLine(e.Message);}
        }

        private async static Task<List<string>> GetYoutubePlaylist(string url, int maxResults) {
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = YOUTUBE_KEY,
                ApplicationName = "YT2SP"
            });
            var playlistRequest = youtubeService.PlaylistItems.List("snippet");
            playlistRequest.PlaylistId = url;
            playlistRequest.MaxResults = maxResults;

            var nextPageToken = "";
            var youtubeVideos = new List<string>();
            while (nextPageToken != null) {
                playlistRequest.PageToken = nextPageToken;
                var playlistResponse = await playlistRequest.ExecuteAsync();
                youtubeVideos.AddRange(playlistResponse.Items.Select(item => item.Snippet.Title));
                nextPageToken = playlistResponse.NextPageToken;
            }
            return youtubeVideos;
        }

        private static async Task OnErrorReceived(object sender, string error, string state) {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await _server.Stop();
        }
    }
}