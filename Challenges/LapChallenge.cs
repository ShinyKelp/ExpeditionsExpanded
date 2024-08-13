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
    public class LapChallenge : Challenge, IRegionSpecificChallenge, IChallengeHooks
    {

        List<string> baseAllowedRegions = SlugcatStats.SlugcatStoryRegions(ExpeditionData.slugcatPlayer);
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

        public string targetRegion;
        string lastExitedGate = "_";
        int timesEntered = 0;
        bool doubleCycle = false;
        bool enteredLastCycle = false;

        public void ApplyHooks()
        {
            On.OverWorld.GateRequestsSwitchInitiation += OverWorld_GateRequestsSwitchInitiation;
            ECEUtilities.OnAllPlayersDied += LapChallenge_AllPlayersDied;
            ECEUtilities.OnHibernated += LapChallenge_Hibernated;
        }
        public void RemoveHooks()
        {
            On.OverWorld.GateRequestsSwitchInitiation -= OverWorld_GateRequestsSwitchInitiation;
            ECEUtilities.OnAllPlayersDied -= LapChallenge_AllPlayersDied;
            ECEUtilities.OnHibernated -= LapChallenge_Hibernated;
        }

        private void LapChallenge_Hibernated()
        {
            timesEntered = 0;
        }

        private void LapChallenge_AllPlayersDied()
        {
            if (!completed)
            {
                timesEntered = 0;
                enteredLastCycle = false;
            }
        }



        private void OverWorld_GateRequestsSwitchInitiation(On.OverWorld.orig_GateRequestsSwitchInitiation orig, OverWorld self, RegionGate reportBackToGate)
        {
            try
            {
                if (!completed)
                {
                    string currentRegion = Region.GetVanillaEquivalentRegionAcronym(self.activeWorld.name);
                    string[] array = Regex.Split(reportBackToGate.room.abstractRoom.name, "_");
                    if (array.Length == 3)
                    {
                        string region;
                        if (currentRegion == array[1])
                        {
                            region = array[2];
                        }
                        else
                            region = array[1];
                        if (region == targetRegion)
                        {
                            timesEntered++;
                            if (timesEntered == 2 || (doubleCycle && enteredLastCycle && lastExitedGate != reportBackToGate.room.abstractRoom.name))
                                CompleteChallenge();
                            enteredLastCycle = true;
                        }
                        else if (currentRegion == targetRegion)
                        {
                            lastExitedGate = reportBackToGate.room.abstractRoom.name;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, reportBackToGate);
            }
        }

        private string SelectRegion()
        {
            List<string> regions = ApplicableRegions;
            if (regions is null)
                regions = new List<string>();
            regions.Remove("SS");
            regions.Remove("OE");
            regions.Remove("LC");
            regions.Remove("DM");
            regions.Remove("MS");
            regions.Remove("RM");
            regions.Remove("HR");
            if (ExpeditionData.challengeDifficulty < 0.8f)
            {
                regions.Remove("SB");
                regions.Remove("UW");
            }

            return ECEUtilities.FilterAndSelectRegion("Lap", regions, "RegionLap");
        }

        public override Challenge Generate()
        { 
            bool dCycle = ExpeditionData.challengeDifficulty < 0.8f;
            string regionAcronym = SelectRegion();
            if (regionAcronym == "")
                return null;
            else
                return new LapChallenge
                {
                    targetRegion = regionAcronym,
                    doubleCycle = dCycle
                };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Region Lap");
        }

        public override string ToString()
        {
            bool enteredLastC = (timesEntered == 1);
            return string.Concat(new string[]
            {
                "LapChallenge",
                "~",
                ValueConverter.ConvertToString<string>(this.targetRegion),
                "><",
                this.doubleCycle ? "1" : "0",
                "><",
                enteredLastC ? "1" : "0",
                "><",
                ValueConverter.ConvertToString<string>(this.lastExitedGate),
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
                this.targetRegion = array[0];
                this.doubleCycle = (array[1] == "1");
                this.enteredLastCycle = (array[2] == "1");
                this.lastExitedGate = array[3];
                this.completed = (array[4] == "1");
                this.hidden = (array[5] == "1");
                this.revealed = (array[6] == "1");

                if (ECEUtilities.DiedLastSession())
                    enteredLastCycle = false;

                if (ECEUtilities.IsRegionForbidden("LapChallenge", targetRegion))
                    targetRegion = SelectRegion();

            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                targetRegion = "SU";
                doubleCycle = true;
                enteredLastCycle = false;
                lastExitedGate = "_";
                completed = hidden = revealed = false;
            }
                UpdateDescription();

        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Enter <region> twice in " + (doubleCycle ? "two cycles, no backtracks." : "one cycle.")).Replace
                ("<region>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(targetRegion, ExpeditionData.slugcatPlayer)));
            base.UpdateDescription();
        }

        public override int Points()
        {
            int points = 90;
            if (!doubleCycle)
                points += 70;
            if (targetRegion == "UW" || targetRegion == "SB")
                points += 50;
            else if(!SlugcatStats.SlugcatStoryRegions(ExpeditionData.slugcatPlayer).Contains(targetRegion))
                points += 20;
            return (hidden ? points * 2 : points);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is LapChallenge lap && lap.targetRegion == targetRegion);
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            List<string> regions = ApplicableRegions;
            if (ECEUtilities.FilterAndSelectRegion("Lap", regions, "RegionLap") == "")
                return false;
            return base.ValidForThisSlugcat(slugcat);
        }

        public override void Reset()
        {
            timesEntered = 0;
            enteredLastCycle = false;
            base.Reset();
        }
    }

}
