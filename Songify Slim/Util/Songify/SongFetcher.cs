﻿using Newtonsoft.Json;
using Songify_Slim.Models;
using Songify_Slim.Util.General;
using Songify_Slim.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using Songify_Slim.Util.Spotify.SpotifyAPI.Web.Models;
using Unosquare.Swan.Formatters;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using TwitchLib.Api.Helix.Models.Soundtrack;
using System.Reflection;
using System.Xml.Linq;

namespace Songify_Slim.Util.Songify
{
    /// <summary>
    ///     This class is for retrieving data of currently playing songs
    /// </summary>
    public class SongFetcher
    {
        private static int _id;
        private readonly List<string> _browsers = new() { "chrome", "msedge", "opera" };
        private readonly List<string> _audioFileyTypes = new() { ".3gp", ".aa", ".aac", ".aax", ".act", ".aiff", ".alac", ".amr", ".ape", ".au", ".awb", ".dss", ".dvf", ".flac", ".gsm", ".iklax", ".ivs", ".m4a", ".m4b", ".m4p", ".mmf", ".mp3", ".mpc", ".msv", ".nmf", ".ogg", ".oga", ".mogg", ".opus", ".ra", ".rm", ".raw", ".rf64", ".sln", ".tta", ".voc", ".vox", ".wav", ".wma", ".wv", ".webm", ".8svx", ".cda" };
        private AutomationElement _parent;
        private static string[] _songinfo;
        private static SongInfo _previousSonginfo;
        private static TrackInfo _songInfo;
        private static bool _trackChanged;
        private bool updating = false;

        /// <summary>
        ///     A method to fetch the song that's currently playing on Spotify.
        ///     returns null if unsuccessful and custom pause text is not set.
        /// </summary>
        /// <returns>Returns String-Array with Artist, Title, Extra</returns>
        internal Task<SongInfo> FetchDesktopPlayer(string player)
        {
            Process[] processes = Process.GetProcessesByName(player);
            foreach (Process process in processes)
                if (process.ProcessName == player && !string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    // If the process name is "Spotify" and the window title is not empty
                    string wintitle = process.MainWindowTitle;
                    string artist = "", title = "", extra;

                    switch (player)
                    {
                        case "Spotify":
                            // Checks if the title is Spotify Premium or Spotify Free in which case we don't want to fetch anything
                            if (wintitle != "Spotify" && wintitle != "Spotify Premium" && wintitle != "Spotify Free" &&
                                wintitle != "Drag")
                            {
                                // Splitting the win title which is always Artist - Title
                                _songinfo = wintitle.Split(new[] { " - " }, StringSplitOptions.None);
                                try
                                {
                                    artist = _songinfo[0].Trim();
                                    title = _songinfo[1].Trim();
                                    _songInfo = GlobalObjects.CurrentSong = new TrackInfo
                                    {
                                        Artists = artist,
                                        Title = title,
                                        Albums = null,
                                        SongId = null,
                                        DurationMs = 0,
                                        IsPlaying = false,
                                        Url = null,
                                        DurationPercentage = 0,
                                        DurationTotal = 0,
                                        Progress = 0,
                                        Playlist = null
                                    };
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogExc(ex);
                                }
                                if (wintitle.Contains(" - "))
                                {
                                    artist = wintitle.Split(Convert.ToChar("-"))[0].Trim();
                                    title = wintitle.Split(Convert.ToChar("-"))[1].Trim();
                                }

                                GlobalObjects.CurrentSong = new TrackInfo
                                {
                                    Artists = artist,
                                    Title = title,
                                    Albums = null,
                                    SongId = null,
                                    DurationMs = 0,
                                    IsPlaying = false,
                                    Url = null,
                                    DurationPercentage = 0,
                                    DurationTotal = 0,
                                    Progress = 0,
                                    Playlist = null
                                };
                                UpdateWebServerResponse();
                                return Task.FromResult(new SongInfo { Artist = artist, Title = title });
                            }
                            // the win title gets changed as soon as spotify is paused, therefore I'm checking
                            //if custom pause text is enabled and if so spit out custom text

                            if (Settings.Settings.CustomPauseTextEnabled)
                                return Task.FromResult(new SongInfo { Artist = "", Title = "", Extra = "" });
                            break;

                        case "vlc":
                            //Splitting the win title which is always Artist - Title
                            if (string.IsNullOrEmpty(wintitle) || wintitle == "vlc")
                                return Task.FromResult(_previousSonginfo);

                            if (!wintitle.Contains(" - VLC media player"))
                                return Task.FromResult(Settings.Settings.CustomPauseTextEnabled
                                    ? new SongInfo { Artist = Settings.Settings.CustomPauseText, Title = "", Extra = "" }
                                    : new SongInfo { Artist = "", Title = "", Extra = "" });

                            wintitle = wintitle.Replace(" - VLC media player", "");

                            try
                            {
                                foreach (string item in _audioFileyTypes.Where(item => wintitle.Contains(item)))
                                {
                                    wintitle = wintitle.Replace(item, "");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogExc(ex);
                            }
                            finally
                            {
                                artist = wintitle;
                                title = "";
                                extra = "";
                            }

                            _songinfo = wintitle.Split(new[] { " - " }, StringSplitOptions.None);

                            _previousSonginfo = new SongInfo { Artist = artist, Title = title, Extra = extra };
                            if (wintitle.Contains(" - "))
                            {
                                artist = wintitle.Split(Convert.ToChar("-"))[0].Trim();
                                title = wintitle.Split(Convert.ToChar("-"))[1].Trim();
                            }

                            GlobalObjects.CurrentSong = new TrackInfo
                            {
                                Artists = artist,
                                Title = title,
                                Albums = null,
                                SongId = null,
                                DurationMs = 0,
                                IsPlaying = false,
                                Url = null,
                                DurationPercentage = 0,
                                DurationTotal = 0,
                                Progress = 0,
                                Playlist = null
                            };
                            UpdateWebServerResponse();

                            return Task.FromResult(_previousSonginfo);

                        case "foobar2000":
                            // Splitting the win title which is always Artist - Title
                            if (wintitle.StartsWith("foobar2000"))
                            {
                                if (Settings.Settings.CustomPauseTextEnabled)
                                    return Task.FromResult(new SongInfo
                                    {
                                        Artist = Settings.Settings.CustomPauseText,
                                        Title = "",
                                        Extra = ""
                                    });
                            }

                            wintitle = wintitle.Replace(" [foobar2000]", "");
                            try
                            {
                                foreach (string item in _audioFileyTypes.Where(item => wintitle.Contains(item)))
                                {
                                    wintitle = wintitle.Replace(item, "");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogExc(ex);
                            }
                            finally
                            {
                                artist = wintitle;
                                title = "";
                                extra = "";
                            }
                            _previousSonginfo = new SongInfo { Artist = artist, Title = title, Extra = extra };
                            if (wintitle.Contains(" - "))
                            {
                                artist = wintitle.Split(Convert.ToChar("-"))[0].Trim();
                                title = wintitle.Split(Convert.ToChar("-"))[1].Trim();
                            }

                            GlobalObjects.CurrentSong = new TrackInfo
                            {
                                Artists = artist,
                                Title = title,
                                Albums = null,
                                SongId = null,
                                DurationMs = 0,
                                IsPlaying = false,
                                Url = null,
                                DurationPercentage = 0,
                                DurationTotal = 0,
                                Progress = 0,
                                Playlist = null
                            };
                            UpdateWebServerResponse();
                            return Task.FromResult(_previousSonginfo);
                    }
                }

            return Task.FromResult<SongInfo>(null);
        }

        /// <summary>
        ///     A method to fetch the song that's currently playing on Youtube.
        ///     returns empty string if unsuccessful and custom pause text is not set.
        ///     Currently supported browsers: Google Chrome
        /// </summary>
        /// <param name="website"></param>
        /// <returns>Returns String with Youtube Video Title</returns>
        public string FetchBrowser(string website)
        {
            string browser = "";

            // chrome, opera, msedge
            foreach (string s in _browsers.Where(s => Process.GetProcessesByName(s).Length > 0))
            {
                browser = s;
                break;
            }

            Process[] procsBrowser = Process.GetProcessesByName(browser);

            foreach (Process procBrowser in procsBrowser)
            {
                // the chrome process must have a window
                if (procBrowser.MainWindowHandle == IntPtr.Zero) continue;

                AutomationElement elm =
                    _parent == null ? AutomationElement.FromHandle(procBrowser.MainWindowHandle) : _parent;

                if (_id == 0)
                    // find the automation element
                    try
                    {
                        AutomationElementCollection elementCollection = elm.FindAll(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
                        foreach (AutomationElement elem in elementCollection)
                            // if the tab item Name contains Youtube
                            switch (website)
                            {
                                case "YouTube":
                                    if (elem.Current.Name.Contains("YouTube"))
                                    {
                                        _id = elem.Current.ControlType.Id;
                                        _parent = TreeWalker.RawViewWalker.GetParent(elem);
                                        UpdateWebServerResponse();
                                        // Regex pattern to replace the notification in front of the tab (1) - (99+)
                                        return FormattedString("YouTube", Regex.Replace(elem.Current.Name, @"^\([\d]*(\d+)[\d]*\+*\)", ""));
                                    }

                                    break;

                                case "Deezer":
                                    if (elem.Current.Name.Contains("Deezer"))
                                    {
                                        _id = elem.Current.ControlType.Id;
                                        _parent = TreeWalker.RawViewWalker.GetParent(elem);
                                        UpdateWebServerResponse();
                                        return FormattedString("Deezer", elem.Current.Name);
                                    }
                                    break;
                            }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogExc(ex);
                        // Chrome has probably changed something, and above walking needs to be modified. :(
                        // put an assertion here or something to make sure you don't miss it
                    }
                else
                    try
                    {
                        AutomationElement element =
                            elm.FindFirst(TreeScope.Descendants,
                                new PropertyCondition(AutomationElement.ControlTypeProperty,
                                    ControlType.LookupById(_id)));

                        // if the tab item Name contains Youtube
                        switch (website)
                        {
                            case "YouTube":
                                if (element == null)
                                    break;
                                if (element.Current.Name.Contains("YouTube"))
                                {
                                    //_id = element.Current.ControlType.Id;
                                    //_parent = TreeWalker.RawViewWalker.GetParent(element);
                                    // Regex pattern to replace the notification in front of the tab (1) - (99+)
                                    string formattedString = FormattedString("YouTube", Regex.Replace(element.Current.Name, @"^\([\d]*(\d+)[\d]*\+*\)", ""));
                                    //try splitting the formatted string to Artist and Title
                                    _songInfo = GlobalObjects.CurrentSong = new TrackInfo
                                    {
                                        Artists = formattedString.Contains("-") ? formattedString.Split('-')[0].Trim() : formattedString,
                                        Title = formattedString.Contains("-") ? formattedString.Split('-')[1].Trim() : formattedString,
                                        Albums = null,
                                        SongId = null,
                                        DurationMs = 0,
                                        IsPlaying = false,
                                        Url = null,
                                        DurationPercentage = 0,
                                        DurationTotal = 0,
                                        Progress = 0,
                                        Playlist = null
                                    };
                                    UpdateWebServerResponse();
                                    return formattedString;
                                }

                                break;

                            case "Deezer":
                                if (element.Current.Name.Contains("Deezer"))
                                {
                                    _id = element.Current.ControlType.Id;
                                    _parent = TreeWalker.RawViewWalker.GetParent(element);
                                    string formattedString = FormattedString("Deezer", element.Current.Name);
                                    //try splitting the formatted string to Artist and Title
                                    _songInfo = GlobalObjects.CurrentSong = new TrackInfo
                                    {
                                        Artists = formattedString.Contains("-") ? formattedString.Split('-')[0].Trim() : formattedString,
                                        Title = formattedString.Contains("-") ? formattedString.Split('-')[1].Trim() : formattedString,
                                        Albums = null,
                                        SongId = null,
                                        DurationMs = 0,
                                        IsPlaying = false,
                                        Url = null,
                                        DurationPercentage = 0,
                                        DurationTotal = 0,
                                        Progress = 0,
                                        Playlist = null
                                    };
                                    UpdateWebServerResponse();
                                    return formattedString;
                                }
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
            }

            return "";
        }

        private static string FormattedString(string player, string temp)
        {
            string s = temp;
            int index;
            switch (player)
            {
                case "YouTube":
                    index = s.LastIndexOf("- YouTube", StringComparison.Ordinal);
                    // Remove everything after the last "-" int the string
                    // which is "- Youtube" and info that music is playing on this tab
                    if (index > 0)
                        s = s.Substring(0, index);
                    s = s.Trim();
                    break;
                case "Deezer":
                    //string temp = Regex.Replace(elem.Current.Name, @"^\([\d]*(\d+)[\d]*\+*\)", "");
                    index = s.LastIndexOf("- Deezer", StringComparison.Ordinal);
                    // Remove everything after the last "-" int the string
                    // which is "- Youtube" and info that music is playing on this tab
                    if (index > 0)
                        s = s.Substring(0, index);
                    s = s.Trim();
                    break;
            }

            return s;
        }

        /// <summary>
        ///     A method to fetch the song that is currently playing via NightBot Song Request.
        ///     Returns null if unsuccessful and custom pause text is not set.
        ///     Returns Error Message if NightBot ID is not set
        /// </summary>

        public async Task<TrackInfo> FetchSpotifyWeb()
        {
            // If the spotify object hast been created (successfully authed)
            if (updating)
                return null;
            updating = true;
            if (ApiHandler.Spotify == null)
            {
                if (!string.IsNullOrEmpty(Settings.Settings.SpotifyAccessToken) &&
                    !string.IsNullOrEmpty(Settings.Settings.SpotifyRefreshToken))
                {
                    await ApiHandler.DoAuthAsync();
                }
                return null;
            }
            // gets the current playing songinfo
            _songInfo = ApiHandler.GetSongInfo();
            try
            {

                if (GlobalObjects.CurrentSong == null || (GlobalObjects.CurrentSong.SongId != _songInfo.SongId && _songInfo.SongId != null))
                {
                    _trackChanged = true;

                    if (GlobalObjects.CurrentSong != null)
                        Logger.LogStr($"CORE: Previous Song {GlobalObjects.CurrentSong.Artists} - {GlobalObjects.CurrentSong.Title}");
                    if (_songInfo.SongId != null)
                        Logger.LogStr($"CORE: Now Playing {_songInfo.Artists} - {_songInfo.Title}");

                    RequestObject previous = GlobalObjects.ReqList.FirstOrDefault(o => o.Trackid == GlobalObjects.CurrentSong.SongId);
                    RequestObject current = GlobalObjects.ReqList.FirstOrDefault(o => o.Trackid == _songInfo.SongId);

                    GlobalObjects.CurrentSong = _songInfo;

                    //if current track is on skiplist, skip it
                    if (GlobalObjects.SkipList.Find(o => o.Trackid == _songInfo.SongId) != null)
                    {
                        await System.Windows.Application.Current.Dispatcher.Invoke(async () =>
                        {
                            GlobalObjects.SkipList.Remove(
                                GlobalObjects.SkipList.Find(o => o.Trackid == _songInfo.SongId));
                            await ApiHandler.SkipSong();
                        });
                    }

                    //if current is not null, mark it as played in the database
                    if (current != null)
                    {
                        dynamic payload = new
                        {
                            uuid = Settings.Settings.Uuid,
                            key = Settings.Settings.AccessKey,
                            queueid = current.Queueid,
                        };
                        await WebHelper.QueueRequest(WebHelper.RequestMethod.Patch, Json.Serialize(payload));
                    }

                    //if Previous is not null then try to remove it from the internal queue (ReqList)
                    if (previous != null)
                    {
                        Logger.LogStr($"QUEUE: Trying to remove {previous.Artist} - {previous.Title}");
                        do
                        {
                            await Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                GlobalObjects.ReqList.Remove(previous);
                            });
                            Thread.Sleep(250);
                        } while (GlobalObjects.ReqList.Contains(previous));

                        Logger.LogStr($"QUEUE: Removed {previous.Artist} - {previous.Title} requested by {previous.Requester} from the queue.");

                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            foreach (Window window in Application.Current.Windows)
                                            {
                                                if (window.GetType() != typeof(WindowQueue))
                                                    continue;
                                                //(qw as Window_Queue).dgv_Queue.ItemsSource.
                                                (window as WindowQueue)?.dgv_Queue.Items.Refresh();
                                            }
                                        });
                    }
                }

                if (_trackChanged)
                {
                    _trackChanged = false;

                    if (_songInfo.SongId != null && !string.IsNullOrEmpty(Settings.Settings.SpotifyPlaylistId))
                    {
                        GlobalObjects.IsInPlaylist = await CheckInLikedPlaylist(GlobalObjects.CurrentSong);
                        await WebHelper.QueueRequest(WebHelper.RequestMethod.Get);
                    }

                    GlobalObjects.QueueUpdateQueueWindow();

                    // Insert the Logic from mainwindow's WriteSong method here since it's easier to handel the song info here
                    await WriteSongInfo(_songInfo);
                }

                if (GlobalObjects.ForceUpdate)
                {
                    await WriteSongInfo(_songInfo);
                    GlobalObjects.ForceUpdate = false;
                }

                UpdateWebServerResponse();
            }
            catch (Exception e)
            {
                updating = false;
                Logger.LogExc(e);
            }
            updating = false;
            return _songInfo ?? new TrackInfo { IsPlaying = false };
        }

        private async Task WriteSongInfo(TrackInfo songInfo)
        {
            string artist = songInfo.Artists;
            string title = songInfo.Title;
            string albumUrl = _songInfo.Albums != null && _songInfo.Albums.Count != 0 ? _songInfo.Albums[0].Url : "";

            if (artist.Contains("Various Artists, "))
            {
                artist = artist.Replace("Various Artists, ", "").Trim();
            }

            string _songPath = GlobalObjects.RootDirectory + "/Songify.txt";
            string _coverTemp = GlobalObjects.RootDirectory + "/tmp.png";
            string _coverPath = GlobalObjects.RootDirectory + "/cover.png";

            if (!_songInfo.IsPlaying)
            {
                // read the text file
                if (!File.Exists(_songPath)) File.Create(_songPath).Close();
                IOManager.WriteOutput(_songPath, Settings.Settings.CustomPauseText);

                if (Settings.Settings.SplitOutput) IOManager.WriteSplitOutput(Settings.Settings.CustomPauseText, title, "");

                IOManager.DownloadCover(null, _coverPath);

                Application.Current.MainWindow?.Dispatcher.Invoke(DispatcherPriority.Normal, new Action((() =>
                {
                    ((MainWindow)Application.Current.MainWindow).TxtblockLiveoutput.Text = Settings.Settings.CustomPauseText;
                })));
                return;
            }

            string currentSongOutput = Settings.Settings.OutputString;
            string currentSongOutputTwitch = Settings.Settings.BotRespSong;
            // this only is used for Spotify because here the artist and title are split
            // replace parameters with actual info
            currentSongOutput = currentSongOutput.Format(
                artist => _songInfo.Artists,
                title => _songInfo.Title,
                extra => "",
                uri => songInfo.SongId,
                url => _songInfo.Url
            ).Format();

            currentSongOutputTwitch = currentSongOutputTwitch.Format(
                artist => _songInfo.Artists,
                title => _songInfo.Title,
                extra => "",
                uri => songInfo.SongId,
                url => _songInfo.Url
            ).Format();
            RequestObject rq = null;

            if (GlobalObjects.ReqList.Count > 0)
            {

                rq = GlobalObjects.ReqList.FirstOrDefault(x => x.Trackid == _songInfo.SongId);
                if (rq != null)
                {
                    currentSongOutput = currentSongOutput.Replace("{{", "");
                    currentSongOutput = currentSongOutput.Replace("}}", "");
                    currentSongOutput = currentSongOutput.Replace("{req}", rq.Requester);

                    currentSongOutputTwitch = currentSongOutputTwitch.Replace("{{", "");
                    currentSongOutputTwitch = currentSongOutputTwitch.Replace("}}", "");
                    currentSongOutputTwitch = currentSongOutputTwitch.Replace("{req}", rq.Requester);
                    GlobalObjects.Requester = rq.Requester;

                }
                else
                {
                    int start = currentSongOutput.IndexOf("{{", StringComparison.Ordinal);
                    int end = currentSongOutput.LastIndexOf("}}", StringComparison.Ordinal) + 2;
                    if (start >= 0) currentSongOutput = currentSongOutput.Remove(start, end - start);
                    start = currentSongOutputTwitch.IndexOf("{{", StringComparison.Ordinal);
                    end = currentSongOutputTwitch.LastIndexOf("}}", StringComparison.Ordinal) + 2;
                    if (start >= 0) currentSongOutputTwitch = currentSongOutputTwitch.Remove(start, end - start);
                    GlobalObjects.Requester = "";
                }
            }
            else
            {
                try
                {
                    int start = currentSongOutput.IndexOf("{{", StringComparison.Ordinal);
                    int end = currentSongOutput.LastIndexOf("}}", StringComparison.Ordinal) + 2;
                    if (start >= 0) currentSongOutput = currentSongOutput.Remove(start, end - start);
                    start = currentSongOutputTwitch.IndexOf("{{", StringComparison.Ordinal);
                    end = currentSongOutputTwitch.LastIndexOf("}}", StringComparison.Ordinal) + 2;
                    if (start >= 0) currentSongOutputTwitch = currentSongOutputTwitch.Remove(start, end - start);
                    GlobalObjects.Requester = "";
                }
                catch (Exception ex)
                {
                    Logger.LogExc(ex);
                }
            }

            // Cleanup the string (remove double spaces, trim and add trailing spaces for scroll)
            currentSongOutput = CleanFormatString(currentSongOutput);

            // read the text file
            if (!File.Exists(_songPath))
            {
                try
                {
                    File.Create(_songPath).Close();
                }
                catch (Exception e)
                {
                    Logger.LogExc(e);
                    return;
                }
            }

            //if (new FileInfo(_songPath).Length == 0) File.WriteAllText(_songPath, currentSongOutput);
            string temp = File.ReadAllText(_songPath);

            // if the text file is different to _currentSongOutput (fetched song) or update is forced
            if (temp.Trim() != currentSongOutput.Trim())
                // Clear the SkipVotes list in TwitchHandler Class
                TwitchHandler.ResetVotes();

            // write song to the text file
            try
            {
                IOManager.WriteOutput(_songPath, currentSongOutput);
            }
            catch (Exception)
            {
                Logger.LogStr($"File {_songPath} couldn't be accessed.");
            }


            if (Settings.Settings.SplitOutput) IOManager.WriteSplitOutput(_songInfo.Artists, _songInfo.Title, "", rq?.Requester);

            // if upload is enabled
            if (Settings.Settings.Upload)
                try
                {
                    WebHelper.UploadSong(currentSongOutput.Trim().Replace(@"\n", " - ").Replace("  ", " "), albumUrl);
                }
                catch (Exception ex)
                {
                    Logger.LogExc(ex);
                    // if error occurs write text to the status asynchronous
                    Application.Current.MainWindow?.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                    {
                        ((MainWindow)Application.Current.MainWindow).LblStatus.Content = "Error uploading Song information";
                    }));
                }

            //Write History
            if (Settings.Settings.SaveHistory && !string.IsNullOrEmpty(currentSongOutput.Trim()) &&
                currentSongOutput.Trim() != Settings.Settings.CustomPauseText)
            {

                int unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

                //save the history file
                string historyPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) + "/" +
                                     "history.shr";
                XDocument doc;
                if (!File.Exists(historyPath))
                {
                    doc = new XDocument(new XElement("History",
                        new XElement("d_" + DateTime.Now.ToString("dd.MM.yyyy"))));
                    doc.Save(historyPath);
                }

                doc = XDocument.Load(historyPath);
                if (!doc.Descendants("d_" + DateTime.Now.ToString("dd.MM.yyyy")).Any())
                    doc.Descendants("History").FirstOrDefault()
                        ?.Add(new XElement("d_" + DateTime.Now.ToString("dd.MM.yyyy")));

                XElement elem = new("Song", currentSongOutput.Trim());
                elem.Add(new XAttribute("Time", unixTimestamp));
                XElement x = doc.Descendants("d_" + DateTime.Now.ToString("dd.MM.yyyy")).FirstOrDefault();
                XNode lastNode = x.LastNode;
                if (lastNode != null)
                {
                    if (currentSongOutput.Trim() != ((XElement)lastNode).Value)
                        x?.Add(elem);
                }
                else
                {
                    x?.Add(elem);
                }
                doc.Save(historyPath);
            }

            //Upload History
            if (Settings.Settings.UploadHistory && !string.IsNullOrEmpty(currentSongOutput.Trim()) &&
                currentSongOutput.Trim() != Settings.Settings.CustomPauseText)
            {

                int unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

                // Upload Song
                try
                {
                    WebHelper.UploadHistory(currentSongOutput.Trim().Replace(@"\n", " - ").Replace("  ", " "), unixTimestamp);
                }
                catch (Exception ex)
                {
                    Logger.LogExc(ex);
                    // Writing to the statusstrip label
                    Application.Current.MainWindow?.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                    {
                        ((MainWindow)Application.Current.MainWindow).LblStatus.Content = "Error uploading history";
                    }));
                }
            }

            // Update Song Queue, Track has been played. All parameters are optional except track id, playedd and o. o has to be the value "u"
            //if (rTrackId != null) WebHelper.UpdateWebQueue(rTrackId, "", "", "", "", "1", "u");

            // Send Message to Twitch if checked
            if (Settings.Settings.AnnounceInChat)
            {
                TwitchHandler.SendCurrSong();
            }

            //Save Album Cover
            if (Settings.Settings.DownloadCover) IOManager.DownloadCover(albumUrl, _coverPath);


            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow main = Application.Current.MainWindow as MainWindow;
                main?.img_cover.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                {
                    main.SetTextPreview(currentSongOutput.Trim().Replace(@"\n", " - ").Replace("  ", " "));
                }));
            });
        }

        private static string CleanFormatString(string currentSongOutput)
        {
            RegexOptions options = RegexOptions.None;
            Regex regex = new("[ ]{2,}", options);
            currentSongOutput = regex.Replace(currentSongOutput, " ");
            currentSongOutput = currentSongOutput.Trim();

            // Add trailing spaces for better scroll
            if (Settings.Settings.AppendSpaces)
                currentSongOutput = currentSongOutput.PadRight(currentSongOutput.Length + Settings.Settings.SpaceCount);

            return currentSongOutput;
        }

        private static void UpdateWebServerResponse()
        {
            if (_songInfo == null)
            {
                _songInfo = GlobalObjects.CurrentSong ?? new TrackInfo();
            }
            string j = Json.Serialize(_songInfo);
            dynamic obj = JsonConvert.DeserializeObject<dynamic>(j);
            IDictionary<string, object> dictionary = obj.ToObject<IDictionary<string, object>>();
            dictionary["IsInLikedPlaylist"] = GlobalObjects.IsInPlaylist;
            dictionary["Requester"] = GlobalObjects.Requester;
            dictionary["GoalTotal"] = Settings.Settings.RewardGoalAmount;
            dictionary["GoalCount"] = GlobalObjects.RewardGoalCount;
            dictionary["QueueCount"] = GlobalObjects.ReqList.Count;
            dictionary["Queue"] = GlobalObjects.ReqList;
            string updatedJson = JsonConvert.SerializeObject(dictionary, Formatting.Indented);
            GlobalObjects.ApiResponse = updatedJson;
        }

        private static async Task<bool> CheckInLikedPlaylist(TrackInfo trackInfo)
        {
            Debug.WriteLine("Check Playlist");
            if (trackInfo.SongId == null)
                return false;
            string id = trackInfo.SongId;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (string.IsNullOrEmpty(Settings.Settings.SpotifyPlaylistId))
            {
                return false;
            }

            bool firstFetch = true;
            Paging<PlaylistTrack> tracks = null;
            do
            {
                tracks = firstFetch
                    ? await ApiHandler.Spotify.GetPlaylistTracksAsync(Settings.Settings.SpotifyPlaylistId)
                    : await ApiHandler.Spotify.GetPlaylistTracksAsync(Settings.Settings.SpotifyPlaylistId, "", 100,
                        tracks.Offset + tracks.Limit);
                if (tracks.Items.Any(t => t.Track.Id == id))
                {
                    return true;
                }
                firstFetch = false;
            } while (tracks.HasNextPage());
            return false;
        }
    }
}