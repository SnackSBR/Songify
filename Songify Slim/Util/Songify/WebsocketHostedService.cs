using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Core.Models.ChannelPoints;
using Songify_Slim.Util.General;
using Songify_Slim.Util.Spotify;
using static Songify_Slim.Util.General.Enums;
using System.Diagnostics;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateRedemptionStatus;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using System.Windows;
using Songify_Slim.Views;
using System.Windows.Media;
using MahApps.Metro.IconPacks;

namespace Songify_Slim.Util.Songify
{
    public class WebsocketHostedService : IHostedService
    {
        private readonly ILogger<WebsocketHostedService> _logger;
        private readonly EventSubWebsocketClient _eventSubWebsocketClient;


        public WebsocketHostedService(ILogger<WebsocketHostedService> logger, EventSubWebsocketClient eventSubWebsocketClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _eventSubWebsocketClient = eventSubWebsocketClient ?? throw new ArgumentNullException(nameof(eventSubWebsocketClient));
            _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
            _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
            _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
            _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;

            _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += ChannelPointsCustomRewardRedemptionAdd;
            _eventSubWebsocketClient.StreamOnline += StreamOnline;
            _eventSubWebsocketClient.StreamOffline += StreamOffline;
        }

        private Task StreamOffline(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Stream.StreamOfflineArgs args)
        {
            Logger.LogStr("TWITCH API: Stream is down");
            Settings.Settings.IsLive = false;
            return Task.CompletedTask;
        }

        private Task StreamOnline(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Stream.StreamOnlineArgs args)
        {
            Logger.LogStr("TWITCH API: Stream is up");
            Settings.Settings.IsLive = true;
            return Task.CompletedTask;
        }

        private async Task ChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs args)
        {
            var redemption = args.Notification.Payload.Event;
            RedemptionReward reward = redemption.Reward;
            string trackId;

            List<CustomReward> managableRewards = await TwitchHandler.GetChannelRewards(true);
            bool isManagable = managableRewards.Find(r => r.Id == reward.Id) != null;

            if (Settings.Settings.TwRewardId.Any(o => o == reward.Id))
            {
                Logger.LogStr($"PUBSUB: Channel reward {reward.Title} redeemed by {redemption.UserName}");
                List<int> userlevel = GlobalObjects.TwitchUsers.First(o => o.UserId == redemption.UserId).UserLevels;
                Logger.LogStr($"{redemption.UserName}s userlevel = {userlevel.Last()} ({Enum.GetName(typeof(TwitchUserLevels), userlevel.Last())})");
                string msg;
                if (!userlevel.Contains(Settings.Settings.TwSrUserLevel))
                {
                    msg = $"Sorry, only {Enum.GetName(typeof(TwitchUserLevels), Settings.Settings.TwSrUserLevel)} or higher can request songs.";
                    //Send a Message to the user, that his Userlevel is too low
                    if (Settings.Settings.RefundConditons.Any(i => i == 0) && isManagable)
                    {
                        UpdateRedemptionStatusResponse updateRedemptionStatus =
                            await TwitchHandler.TwitchApi.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                                Settings.Settings.TwitchUser.Id, reward.Id,
                                [reward.Id],
                                new UpdateCustomRewardRedemptionStatusRequest
                                { Status = CustomRewardRedemptionStatus.CANCELED });
                        if (updateRedemptionStatus.Data[0].Status == CustomRewardRedemptionStatus.CANCELED)
                        {
                            msg += $" {Settings.Settings.BotRespRefund}";
                        }
                    }

                    if (!string.IsNullOrEmpty(msg))
                        TwitchHandler.SendChatMessage(Settings.Settings.TwChannel, msg);
                    return;
                }

                if (TwitchHandler.IsUserBlocked(redemption.UserName))
                {
                    msg = "You are blocked from making Songrequests";
                    //Send a Message to the user, that his Userlevel is too low
                    if (Settings.Settings.RefundConditons.Any(i => i == 1) && isManagable)
                    {
                        UpdateRedemptionStatusResponse updateRedemptionStatus =
                            await TwitchHandler.TwitchApi.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                                Settings.Settings.TwitchUser.Id, reward.Id,
                                [reward.Id],
                                new UpdateCustomRewardRedemptionStatusRequest
                                { Status = CustomRewardRedemptionStatus.CANCELED });
                        if (updateRedemptionStatus.Data[0].Status == CustomRewardRedemptionStatus.CANCELED)
                        {
                            msg += $" {Settings.Settings.BotRespRefund}";
                        }
                    }

                    if (!string.IsNullOrEmpty(msg))
                        TwitchHandler.SendChatMessage(Settings.Settings.TwChannel, msg);
                    return;
                }

                // checks if the user has already the max amount of songs in the queue
                if (!Settings.Settings.TwSrUnlimitedSr && TwitchHandler.MaxQueueItems(redemption.UserName, userlevel))
                {
                    // if the user reached max requests in the queue skip and inform requester
                    string response = Settings.Settings.BotRespMaxReq;
                    response = response.Replace("{user}", redemption.UserName);
                    response = response.Replace("{artist}", "");
                    response = response.Replace("{title}", "");
                    response = response.Replace("{maxreq}", $"{(TwitchUserLevels)userlevel.Max()} {TwitchHandler.GetMaxRequestsForUserLevels(userlevel)}");
                    response = response.Replace("{errormsg}", "");
                    response = TwitchHandler.CleanFormatString(response);
                    if (!string.IsNullOrEmpty(response))
                        TwitchHandler.SendChatMessage(Settings.Settings.TwChannel, response);
                    return;
                }

                if (SpotifyApiHandler.Spotify == null)
                {
                    msg = "It seems that Spotify is not connected right now.";
                    //Send a Message to the user, that his Userlevel is too low
                    if (Settings.Settings.RefundConditons.Any(i => i == 2) && isManagable)
                    {
                        UpdateRedemptionStatusResponse updateRedemptionStatus =
                            await TwitchHandler.TwitchApi.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                                Settings.Settings.TwitchUser.Id, reward.Id,
                                [reward.Id],
                                new UpdateCustomRewardRedemptionStatusRequest
                                { Status = CustomRewardRedemptionStatus.CANCELED });
                        if (updateRedemptionStatus.Data[0].Status == CustomRewardRedemptionStatus.CANCELED)
                        {
                            msg += $" {Settings.Settings.BotRespRefund}";
                        }
                    }

                    TwitchHandler.SendChatMessage(Settings.Settings.TwChannel, msg);
                    return;
                }

                // if Spotify is connected and working manipulate the string and call methods to get the song info accordingly
                trackId = await TwitchHandler.GetTrackIdFromInput(redemption.UserInput);
                if (trackId == "shortened")
                {
                    TwitchHandler.SendChatMessage(Settings.Settings.TwChannel,
                        "Spotify short links are not supported. Please type in the full title or get the Spotify URI (starts with \"spotify:track:\")");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(trackId))
                {
                    if (Settings.Settings.SongBlacklist.Any(s => s.TrackId == trackId))
                    {
                        Debug.WriteLine("This song is blocked");
                        TwitchHandler.SendChatMessage(Settings.Settings.TwChannel, "This song is blocked");
                        return;
                    }

                    ReturnObject returnObject = await TwitchHandler.AddSong2(trackId, redemption.UserName);
                    msg = returnObject.Msg;
                    if (Settings.Settings.RefundConditons.Any(i => i == returnObject.Refundcondition) && isManagable)
                    {
                        try
                        {
                            UpdateRedemptionStatusResponse updateRedemptionStatus =
                                await TwitchHandler.TwitchApi.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                                    Settings.Settings.TwitchUser.Id, reward.Id,
                                    [reward.Id],
                                    new UpdateCustomRewardRedemptionStatusRequest
                                    { Status = CustomRewardRedemptionStatus.CANCELED },
                                    Settings.Settings.TwitchAccessToken);
                            if (updateRedemptionStatus.Data[0].Status == CustomRewardRedemptionStatus.CANCELED)
                            {
                                msg += $" {Settings.Settings.BotRespRefund}";
                            }
                        }
                        catch (Exception)
                        {
                            Logger.LogStr(
                                "PUBSUB: Could not refund points. Has the reward been created through the app?");
                        }
                    }
                    else
                    {
                        if (Settings.Settings.FulfillRedemption && isManagable)
                        {
                            try
                            {
                                UpdateCustomRewardRedemptionStatusRequest request = new()
                                {
                                    Status = CustomRewardRedemptionStatus.FULFILLED
                                };

                                await TwitchHandler.TwitchApi.Helix.ChannelPoints.UpdateRedemptionStatusAsync(Settings.Settings.TwitchUser.Id, reward.Id, [redemption.Id], request, Settings.Settings.TwitchAccessToken);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogStr("[FulfillRedemption]: " + ex.Message);
                                Logger.LogStr(ex.StackTrace);
                            }
                        }

                    }

                    if (!string.IsNullOrEmpty(msg))
                    {
                        if (msg.StartsWith("[announce "))
                        {
                            await TwitchHandler.AnnounceInChat(msg);
                        }
                        else
                        {
                            TwitchHandler.SendChatMessage(Settings.Settings.TwChannel, msg);
                        }
                    }
                }
                else
                {
                    // if no track has been found inform the requester
                    string response = Settings.Settings.BotRespError;
                    response = response.Replace("{user}", redemption.UserName);
                    response = response.Replace("{artist}", "");
                    response = response.Replace("{title}", "");
                    response = response.Replace("{maxreq}", "");
                    response = response.Replace("{errormsg}", "Couldn't find a song matching your request.");

                    //Send a Message to the user, that his Userlevel is too low
                    if (Settings.Settings.RefundConditons.Any(i => i == 7) && isManagable)
                    {
                        try
                        {
                            UpdateRedemptionStatusResponse updateRedemptionStatus =
                                await TwitchHandler.TwitchApi.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                                    Settings.Settings.TwitchUser.Id, reward.Id,
                                    [reward.Id],
                                    new UpdateCustomRewardRedemptionStatusRequest
                                    { Status = CustomRewardRedemptionStatus.CANCELED });
                            if (updateRedemptionStatus.Data[0].Status == CustomRewardRedemptionStatus.CANCELED)
                            {
                                response += $" {Settings.Settings.BotRespRefund}";
                            }
                        }
                        catch (Exception)
                        {
                            Logger.LogStr(
                                "PUBSUB: Could not refund points. Has the reward been created through the app?");
                        }
                    }

                    if (!string.IsNullOrEmpty(response))
                    {
                        if (response.StartsWith("[announce "))
                        {
                            await TwitchHandler.AnnounceInChat(response);
                        }
                        else
                        {
                            TwitchHandler.SendChatMessage(Settings.Settings.TwChannel, response);
                        }
                    }
                }
            }

            if (Settings.Settings.TwRewardSkipId.Any(id => id == reward.Id))
            {
                if (TwitchHandler._skipCooldown)
                    return;
                await SpotifyApiHandler.SkipSong();

                TwitchHandler.SendChatMessage(Settings.Settings.TwChannel, "Skipping current song...");
                TwitchHandler._skipCooldown = true;
                TwitchHandler.SkipCooldownTimer.Start();
            }
        }

        private async Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
        {
            Logger.LogStr($"Websocket {_eventSubWebsocketClient.SessionId} - Error occurred!");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.ConnectAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.DisconnectAsync();
        }

        private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
        {
            Logger.LogStr($"Websocket {_eventSubWebsocketClient.SessionId} connected!");

            if (!e.IsRequestedReconnect)
            {
                // subscribe to topics
                var condition = new Dictionary<string, string> { { "broadcaster_user_id", Settings.Settings.TwitchChannelId }, { "moderator_user_id", Settings.Settings.TwitchChannelId } };
                await TwitchHandler.TwitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1", condition, EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId, accessToken: Settings.Settings.TwitchAccessToken);
                await TwitchHandler.TwitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync("stream.online", "1", condition, EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId, accessToken: Settings.Settings.TwitchAccessToken);
                await TwitchHandler.TwitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync("stream.offline", "1", condition, EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId, accessToken: Settings.Settings.TwitchAccessToken);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window.GetType() != typeof(MainWindow))
                            continue;
                        ((MainWindow)window).IconTwitchPubSub.Foreground = Brushes.GreenYellow;
                        ((MainWindow)window).IconTwitchPubSub.Kind = PackIconBootstrapIconsKind.CheckCircleFill;
                    }
                });
                Logger.LogStr("TWITCH PUBSUB: Connected");
                //SendChatMessage(Settings.Settings.TwChannel, "Connected to PubSub");
                //Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} PubSub: Connected");
            }
        }

        private async Task OnWebsocketDisconnected(object sender, EventArgs e)
        {
            Logger.LogStr($"Websocket {_eventSubWebsocketClient.SessionId} disconnected!");

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.GetType() != typeof(MainWindow))
                        continue;
                    ((MainWindow)window).IconTwitchPubSub.Foreground = Brushes.IndianRed;
                    ((MainWindow)window).IconTwitchPubSub.Kind = PackIconBootstrapIconsKind.TriangleFill;
                }
            });
            //Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} PubSub: Closed");
            Logger.LogStr("PUBSUB: Disconnected");

            // Don't do this in production. You should implement a better reconnect strategy
            while (!await _eventSubWebsocketClient.ReconnectAsync())
            {
                Logger.LogStr("Websocket reconnect failed!");
                await Task.Delay(1000);
            }
        }

        private async Task OnWebsocketReconnected(object sender, EventArgs e)
        {
            Logger.LogStr($"Websocket {_eventSubWebsocketClient.SessionId} reconnected");
        }
    }
}
