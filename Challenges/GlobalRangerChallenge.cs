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
    public class GlobalRangerChallenge : Challenge
    {
        HashSet<CreatureTemplate.Type> killedCreatures = new HashSet<CreatureTemplate.Type>();
        public int targetAmount;
        public GlobalRangerChallenge()
        {
        }

        ~GlobalRangerChallenge()
        {
            killedCreatures.Clear();
        }

        public override bool RespondToCreatureKill()
        {
            return true;
        }

        public override void CreatureKilled(Creature crit, int playerNumber)
        {
            if (!completed && !killedCreatures.Contains(crit.abstractCreature.creatureTemplate.type))
            {
                killedCreatures.Add(crit.abstractCreature.creatureTemplate.type);
                if (killedCreatures.Count >= targetAmount)
                    CompleteChallenge();
                UpdateDescription();
            }
        }

        public override Challenge Generate()
        {
            
            return new GlobalRangerChallenge
            {
                targetAmount = (int)(ExpeditionData.challengeDifficulty * 30f) + 6
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Global Ranger");
        }

        public override string ToString()
        {
            string killedCreaturesStr = "";
            foreach (CreatureTemplate.Type t in killedCreatures)
            {
                killedCreaturesStr += "><" + t.value;
            }
            return string.Concat(new string[]
            {
                "GlobalRangerChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.targetAmount),
                "><",
                this.completed ? "1" : "0",
                "><",
                this.hidden ? "1" : "0",
                "><",
                this.revealed ? "1" : "0",
                killedCreaturesStr

            });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                this.targetAmount = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.completed = (array[1] == "1");
                this.hidden = (array[2] == "1");
                this.revealed = (array[3] == "1");

                if (array.Length > 4)
                {
                    for (int i = 4; i < array.Length; i++)
                    {
                        killedCreatures.Add(new CreatureTemplate.Type(array[i]));
                    }
                }
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                this.targetAmount = 6;
                revealed = hidden = completed = false;
            }
            this.UpdateDescription();
        }
        public override void UpdateDescription()
        {

            this.description = ChallengeTools.IGT.Translate("Kill <target_amount> different creature species [<current_amount>/<target_amount>]")
                .Replace("<target_amount>", ValueConverter.ConvertToString<int>(this.targetAmount))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.killedCreatures.Count));
            base.UpdateDescription();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is GlobalRangerChallenge);
        }

        public override int Points()
        {
            return (3 * (targetAmount) + 5) * (this.hidden ? 2 : 1);
        }

        public override void Reset()
        {
            killedCreatures.Clear();
            base.Reset();
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            if (ExpeditionsExpandedMod.HasCustomPlayer1)
                return true;
            return slugcat != MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Saint;
        }

    }

}
