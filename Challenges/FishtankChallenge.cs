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
    public class FishtankChallenge : Challenge, IRegionSpecificChallenge, IChallengeHooks
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

        public int targetAmount;
        public string targetRegion;

        public void ApplyHooks()
        {
            On.Player.Update += Player_Update;
        }
        public void RemoveHooks()
        {
            On.Player.Update -= Player_Update;
        }

        private void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);
            try
            {
                if (!completed && self.room != null && self.room.water && self.room.world.name == targetRegion)
                {
                    int amount = 0;
                    foreach(AbstractCreature hazerCreature in self.room.abstractRoom.creatures.FindAll(c => c.creatureTemplate.type == CreatureTemplate.Type.Hazer))
                    {
                        if (hazerCreature.realizedCreature.Submersion > 0.9f)
                            amount++;
                    }
                    if (amount >= targetAmount)
                        CompleteChallenge();
                }
            }
            catch(Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
            }
        }

        private string SelectRegion()
        {
            List<string> regions = ApplicableRegions;
            if (regions is null)
                regions = new List<string>();
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
            return ECEUtilities.FilterAndSelectRegion("Fishtank", regions);
        }

        public override Challenge Generate()
        {
            string regionAcronym = SelectRegion();
            if (regionAcronym == "")
                return null;
            else
            return new FishtankChallenge
            {
                targetAmount = (int)(ExpeditionData.challengeDifficulty * 7f) + 1,
                targetRegion = regionAcronym
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Fishtank");
        }

        public override string ToString()
        {
            return string.Concat(new string[]
           {
                "FishtankChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.targetAmount),
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
                this.targetAmount = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.targetRegion = array[1];
                this.completed = (array[2] == "1");
                this.hidden = (array[3] == "1");
                this.revealed = (array[4] == "1");
                if (ECEUtilities.IsRegionForbidden("Fishtank", targetRegion))
                    targetRegion = SelectRegion();
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                this.targetAmount = 2;
                this.targetRegion = "SU";
                completed = revealed = hidden = false;
            }
            
            this.UpdateDescription();
        }

        public override void UpdateDescription()
        {

            this.description = ChallengeTools.IGT.Translate("Put <target_amount> hazers in the same body of water in <target_region>"
                .Replace("<target_amount>", ValueConverter.ConvertToString<int>(this.targetAmount))
                .Replace("<target_region>", Region.GetRegionFullName(targetRegion, ExpeditionData.slugcatPlayer)));
            base.UpdateDescription();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is FishtankChallenge);
        }

        public override int Points()
        {
            return (int)(20 * (targetAmount-1) * (this.hidden ? 2 : 1));
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            List<string> regions = ApplicableRegions;
            if (ECEUtilities.FilterAndSelectRegion("Fishtank", regions) == "")
                return false;
            return base.ValidForThisSlugcat(slugcat);
        }


    }
}
