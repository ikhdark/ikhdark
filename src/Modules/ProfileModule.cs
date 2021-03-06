﻿using Discord;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using R6DB_Bot.Models;
using R6DB_Bot.Extensions;
using R6DB_Bot.Enums;
using System.Text.RegularExpressions;
using R6DB_Bot.Services;

namespace R6DB_Bot.Modules
{
    [Name("Profile Information")]
    public class ProfileModule : ModuleBase<SocketCommandContext>
    {
        public string baseUrl = "";
        public string xAppId = "";

        private readonly CommandService _service;
        private readonly IConfigurationRoot _config;
        private static string Prefix = "!";

        private RegionEnum regionEnum;
        private PlatformEnum platformEnum;

        public ProfileModule(CommandService service, IConfigurationRoot config)
        {
            _service = service;
            _config = config;

            baseUrl = _config["r6db_url"];
            xAppId = _config["x-app-id"];
            
            platformEnum = PlatformEnum.PC;
        }

        [Command("profile pc"), Alias("p pc"), Name("Profile PC")]
        [Priority(1)]
        [Summary("Get profile information about the player")]
        public async Task GetPCProfile([Remainder]string text)
        {
            platformEnum = PlatformEnum.PC;
            await GetPlayerProfile(text);
        }

        [Command("profile xbox"), Alias("p xbox"), Name("Profile XBOX")]
        [Priority(1)]
        [Summary("Get profile information about the player")]
        public async Task GetXBOXProfile([Remainder]string text)
        {
            platformEnum = PlatformEnum.XBOX;
            await GetPlayerProfile(text);
        }

        [Command("profile ps4"), Alias("p ps4"), Name("Profile PS4")]
        [Priority(1)]
        [Summary("Get profile information about the player")]
        public async Task GetPS4Profile([Remainder]string text)
        {
            platformEnum = PlatformEnum.PS4;
            await GetPlayerProfile(text);
        }

        [Command("profile"), Alias("p"), Name("Profile")]
        [Priority(0)]
        [Summary("Get profile information about the player")]
        public async Task GetPlayerProfile([Remainder]string text)
        {
            var model = await PlayerService.GetPlayerInfoFromR6DB(text, baseUrl, xAppId);
            if (model?.guessed != null && model.guessed.IsGuessed)
            {
                await ReplyAsync($"We found **{model.guessed.PlayersFound}** likely results for the name **{text}** if the following stats are not the once you are looking for, please be more specific with the name/region/platform.");
            }

            await SendPlayerInformationMessage(model);
        }

        private async Task SendPlayerInformationMessage(PlayerModel model)
        {
            var rankNr = 0;
            var builder = new EmbedBuilder();

            var region = regionEnum.GetAttribute<RegionInformation>().Description;            
            var platform = platformEnum.GetAttribute<PlatformInformation>().Description;

            var placementInfo = "";
            if(model?.placements != null)
            {
                placementInfo = Environment.NewLine  +
                                "**Global Rank:** "  + model?.placements?.global ?? " not placed " + Environment.NewLine +
                                "**Europe Rank:** "  + model?.placements?.emea ?? " not placed " + Environment.NewLine +
                                "**America Rank:** " + model?.placements?.ncsa ?? " not placed " + Environment.NewLine +
                                "**Asia Rank:** "    + model?.placements?.apac ?? " not placed " + Environment.NewLine;
            }

            builder.AddField("General Information", "**Level:** " + model?.level + placementInfo);


            builder.AddField("Technical Information", "**ID:** " + model?.id + Environment.NewLine +
                                                      "**UserID:** " + model?.userId ?? "Unkown" + Environment.NewLine +
                                                      "**Profile Added:** " + model?.created_at.ToString("dd MMMM yyyy hh:mm:ss") + Environment.NewLine +
                                                      "**Last Played:** " + model?.lastPlayed.last_played?.ToString("dd MMMM yyyy hh:mm:ss") + Environment.NewLine);           

            if(model?.aliases != null)
            {
                var aliases = "";
                foreach(var alias in model?.aliases)
                {
                    aliases += alias.name + Environment.NewLine  + "    `" + alias.created_at.ToString("dd MMMM yyyy hh:mm:ss") + "`" + Environment.NewLine + Environment.NewLine;
                }
                builder.AddField("Aliases", aliases);
            }

            builder.ImageUrl = "https://ubistatic-a.akamaihd.net/0058/prod/assets/images/season5-rank20.f31680a7.svg";
            builder.Description = region + " Player Profile information on " + platform + " for **" + model.name + "**";

            builder.Author = new EmbedAuthorBuilder
            {
                IconUrl = "https://i.redd.it/iznunq2m8vgy.png",
                Name = platform + " " + region + " Player Profile",
                Url = "http://r6db.com/player/" + model.id
            };

            builder.Footer = new EmbedFooterBuilder
            {
                IconUrl = "https://i.redd.it/iznunq2m8vgy.png",
                Text = "Created by Dakpan#6955"
            };

            builder.ThumbnailUrl = GetRankImage(rankNr);
            builder.Timestamp = DateTime.UtcNow;
            builder.Url = "http://r6db.com/player/" + model.id;

            builder.WithColor(Color.Orange);

            await ReplyAsync(string.Empty, false, builder);
        }

        private string ToReadableString(TimeSpan span)
        {
            string formatted = string.Format("{0}{1}{2}{3}",
                span.Duration().Days > 0 ? string.Format("{0:0} day{1} ", span.Days, span.Days == 1 ? String.Empty : "s") : string.Empty,
                span.Duration().Hours > 0 ? string.Format("{0:0} hour{1} " + Environment.NewLine, span.Hours, span.Hours == 1 ? String.Empty : "s") : string.Empty,
                span.Duration().Minutes > 0 ? string.Format("{0:0} minute{1} ", span.Minutes, span.Minutes == 1 ? String.Empty : "s") : string.Empty,
                span.Duration().Seconds > 0 ? string.Format("{0:0} second{1} ", span.Seconds, span.Seconds == 1 ? String.Empty : "s") : string.Empty);
            
            if (string.IsNullOrEmpty(formatted))
            {
                formatted = "0 seconds";
            }

            return formatted;
        }

        private int CeilingRankMMR(int? rank_nr)
        {
            rank_nr = rank_nr ?? 0;
            var rankEnum = (RankEnum)rank_nr;
            var info = rankEnum.GetAttribute<RankInformation>();
            return info.ELO;
        }

        private string ToReadableRank(int? rank_nr)
        {
            rank_nr = rank_nr ?? 0;
            var rankEnum = (RankEnum)rank_nr;
            var info = rankEnum.GetAttribute<RankInformation>();
            return info.Description;
        }

        private string GetRankImage(int? rank_nr)
        {
            rank_nr = rank_nr ?? 0;
            var rankEnum = (RankEnum)rank_nr;
            var info = rankEnum.GetAttribute<RankInformation>();
            return info.URL;
        }

        private string GetRatio(int? min, int? max)
        {
            if(min == 0  || min == null || max == 0 || max == null)
            {
                return "0";
            }
            return ((decimal)min / (decimal)max).ToString("N2");
        }
    }
}
