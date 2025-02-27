using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Songify_Slim.Util.Spotify.SpotifyAPI.Web.Models
{
    public class Image
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("width")]
        public double Width { get; set; }

        [JsonProperty("height")]
        public double Height { get; set; }
    }

    public class ErrorResponse : BasicModel
    { }

    public class Error
    {
        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class PlaylistTrackCollection
    {
        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }
    }

    public class Followers
    {
        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }
    }

    public class PlaylistTrack
    {
        [JsonProperty("added_at")]
        public DateTime AddedAt { get; set; }

        [JsonProperty("added_by")]
        public PublicProfile AddedBy { get; set; }

        [JsonProperty("track")]
        public FullTrack Track { get; set; }

        [JsonProperty("is_local")]
        public bool IsLocal { get; set; }
    }

    /// <summary>
    ///     Delete-Track Wrapper
    /// </summary>
    /// <param name="uri">An Spotify-URI</param>
    /// <param name="positions">Optional positions</param>
    public class DeleteTrackUri(string uri, params int[] positions)
    {
        [JsonProperty("uri")]
        public string Uri { get; set; } = uri;

        [JsonProperty("positions")]
        public List<int> Positions { get; set; } = [.. positions];

        public bool ShouldSerializePositions()
        {
            return Positions.Count > 0;
        }
    }

    public class Copyright
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class LinkedFrom
    {
        [JsonProperty("external_urls")]
        public Dictionary<string, string> ExternalUrls { get; set; }

        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("uri")]
        public string Uri { get; set; }
    }

    public class SavedTrack
    {
        [JsonProperty("added_at")]
        public DateTime AddedAt { get; set; }

        [JsonProperty("track")]
        public FullTrack Track { get; set; }
    }

    public class SavedAlbum
    {
        [JsonProperty("added_at")]
        public DateTime AddedAt { get; set; }

        [JsonProperty("album")]
        public FullAlbum Album { get; set; }
    }

    public class Cursor
    {
        [JsonProperty("after")]
        public string After { get; set; }

        [JsonProperty("before")]
        public string Before { get; set; }
    }

    public class Context
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("external_urls")]
        public Dictionary<string, string> ExternalUrls { get; set; }

        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}