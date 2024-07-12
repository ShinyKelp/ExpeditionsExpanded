using Expedition;
using Menu.Remix;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace ExpeditionsExpanded
{
    public class WaterproofChallenge : Challenge, IRegionSpecificChallenge
    {
        public List<string> baseAllowedRegions = SlugcatStats.SlugcatStoryRegions(ExpeditionData.slugcatPlayer);
        public List<string> ApplicableRegions
        {
            get => baseAllowedRegions;
            set
            {
                if (value is null)
                    return;
                baseAllowedRegions.Clear();
                baseAllowedRegions.AddRange(value);
            }
        }
        public int totalFrames;
        int currentFrames;
        public string targetRegion;
        public WaterproofChallenge() 
        {
            On.Player.Update += Player_Update;
        }

        ~WaterproofChallenge() 
        {
            On.Player.Update -= Player_Update;
        }

        private void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);
            try
            {
                if (completed)
                    return;
                if (self.room != null && self.room.water && self.submerged && self.room.world.name == targetRegion)
                {
                    currentFrames++;
                    if (currentFrames >= totalFrames)
                        CompleteChallenge();
                }
                else if (currentFrames != 0)
                    currentFrames = 0;
            }
            catch(Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
            }
        }

        public override Challenge Generate()
        {
            List<string> regions = ApplicableRegions;
            if (regions is null)
                return null;
            else
            {
                regions.Remove("UW");
                regions.Remove("SS");
                regions.Remove("SI");
                regions.Remove("LC");
                if (ExpeditionData.slugcatPlayer == MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Spear ||
                    ExpeditionData.slugcatPlayer == MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Artificer)
                    regions.Remove("GW");
                regions.Remove("DS");
            }

            if (!ExpeditionsExpandedMod.SelectRegionAfterUserFilters("WaterproofChallenge", out string regionAcronym, regions))
                return null;
            return new WaterproofChallenge
            {
                totalFrames = (int)(ExpeditionData.challengeDifficulty * 6f * 40f) + 40*32,
                targetRegion = regionAcronym
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Waterproof");
        }

        public override string ToString()
        {
            return string.Concat(new string[]
           {
                "WaterproofChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.totalFrames),
                "><",
                targetRegion,
                "><",
                this.completed ? "1" : "0",
                "><",
                this.hidden ? "1" : "0",
                "><",
                this.revealed ? "1" : "0"
           });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                this.totalFrames = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.targetRegion = array[1];
                this.completed = (array[2] == "1");
                this.hidden = (array[3] == "1");
                this.revealed = (array[4] == "1");
            }
            catch (Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
                this.totalFrames = 32;
                this.targetRegion = "SU";
                this.completed = false;
                this.hidden = false;
                this.revealed = false;
            }
            this.UpdateDescription();
        }

        public override void UpdateDescription()
        {

            this.description = ChallengeTools.IGT.Translate("Stay <target_amount> seconds underwater in <target_region>"
                .Replace("<target_amount>", ValueConverter.ConvertToString<int>(this.totalFrames / 40))
                .Replace("<target_region>", Region.GetRegionFullName(targetRegion, ExpeditionData.slugcatPlayer)));
            base.UpdateDescription();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is WaterproofChallenge chal && chal.targetRegion == targetRegion);
        }

        public override int Points()
        {
            return (int)(totalFrames / 20 - 14) * (this.hidden ? 2 : 1);
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            List<string> regions = ApplicableRegions;
            if (!ExpeditionsExpandedMod.ApplyRegionUserFilters("WaterproofChallenge", ref regions))
                return false;
            if (ExpeditionsExpandedMod.HasCustomPlayer1)
                return true;
            return slugcat != MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Rivulet && slugcat != MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Artificer;
        }

    }
}
