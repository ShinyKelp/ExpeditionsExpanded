using Expedition;
using IL.MoreSlugcats;
using Menu.Remix;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ExpeditionsExpanded
{
    public class GatekeeperChallenge : Challenge, IChallengeHooks
    {
        int crossedGates = 0;
        public int minKarmaRequirement, totalGates;

        public void ApplyHooks()
        {
            On.OverWorld.GateRequestsSwitchInitiation += OverWorld_GateRequestsSwitchInitiation;
            ECEUtilities.OnHibernated += GatekeeperChallenge_OnHibernated;
        }
        public void RemoveHooks()
        {
            On.OverWorld.GateRequestsSwitchInitiation -= OverWorld_GateRequestsSwitchInitiation;
            ECEUtilities.OnHibernated -= GatekeeperChallenge_OnHibernated;
        }

        private void GatekeeperChallenge_OnHibernated()
        {
            if (!completed)
            {
                crossedGates = 0;
                UpdateDescription();
            }
        }

        private void OverWorld_GateRequestsSwitchInitiation(On.OverWorld.orig_GateRequestsSwitchInitiation orig, OverWorld self, RegionGate reportBackToGate)
        {
            try
            {
                if (!completed)
                {
                    int i = (!reportBackToGate.letThroughDir ? 1 : 0);
                    RegionGate.GateRequirement req = reportBackToGate.karmaRequirements[(!reportBackToGate.letThroughDir ? 1 : 0)];
                    if (int.TryParse(req.value, out int reqInt))
                    {
                        if(reqInt >= minKarmaRequirement)
                        {
                            crossedGates++;
                            UpdateDescription();
                            if(crossedGates == totalGates)
                                CompleteChallenge();
                        }
                    }
                }
            }catch(Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, reportBackToGate);
            }
        }

        public override Challenge Generate()
        {
            int ceil = 6 + (int)(ExpeditionData.challengeDifficulty * 6.6f);

            int karmaReq = 0, gatesAmount = 0;
            int total = 0;
            int res = ceil - total;
            while(res > 2 || res < 0)
            {
                karmaReq = UnityEngine.Random.Range(3, 6);
                gatesAmount = UnityEngine.Random.Range(2, 5);
                total = karmaReq * gatesAmount;
                if (total == 10)
                    total++;
                res = ceil - total;
            }

            return new GatekeeperChallenge
            {
                minKarmaRequirement = karmaReq,
                totalGates = gatesAmount
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Gate Crossing");
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "GatekeeperChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.minKarmaRequirement),
                "><",
                ValueConverter.ConvertToString<int>(this.totalGates),
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
                this.minKarmaRequirement = int.Parse(array[0]);
                this.totalGates = int.Parse(array[1]);
                this.completed = (array[2] == "1");
                this.hidden = (array[3] == "1");
                this.revealed = (array[4] == "1");
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                minKarmaRequirement = 3;
                totalGates = 2;
                completed = hidden = revealed = false;
            }
            this.UpdateDescription();
        }
        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Cross " + totalGates + " gates of level " + minKarmaRequirement + " or higher in one cycle [" + crossedGates + "/" + totalGates + "]");
            base.UpdateDescription();
        }

        public override int Points()
        {
            int points = 30 * (minKarmaRequirement + totalGates - 2) - 20;
            return hidden ? points * 2 : points;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is GatekeeperChallenge);
        }

        public override void Reset()
        {
            crossedGates = 0;
        }
    }
}
