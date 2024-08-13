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
    public class CycleRangerChallenge : Challenge, IChallengeHooks
    {
        HashSet<CreatureTemplate.Type> killedCreatures = new HashSet<CreatureTemplate.Type>();
        public int targetAmount;

        ~CycleRangerChallenge()
        {
            killedCreatures.Clear();
        }
        public void ApplyHooks()
        {
            ECEUtilities.OnHibernated += RangerChallenge_OnHibernated;
        }
        public void RemoveHooks()
        {
            ECEUtilities.OnHibernated -= RangerChallenge_OnHibernated;
        }

        private void RangerChallenge_OnHibernated()
        {
            if (!completed)
            {
                killedCreatures.Clear();
                UpdateDescription();
            }
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
            
            return new CycleRangerChallenge
            {
                targetAmount = (int)(ExpeditionData.challengeDifficulty * 13f) + 3
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Cycle Ranger");
        }

        public override string ToString()
        {
            return string.Concat(new string[]
           {
                "CycleRangerChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.targetAmount),
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
                this.completed = (array[1] == "1");
                this.hidden = (array[2] == "1");
                this.revealed = (array[3] == "1");
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                targetAmount = 3;
                completed = hidden = revealed = false;
            }
            
            this.UpdateDescription();
        }
        public override void UpdateDescription()
        {

            this.description = ChallengeTools.IGT.Translate("Kill <target_amount> different creature species this cycle [<current_amount>/<target_amount>]")
                .Replace("<target_amount>", ValueConverter.ConvertToString<int>(this.targetAmount))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.killedCreatures.Count));
            base.UpdateDescription();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is CycleRangerChallenge);
        }

        public override int Points()
        {
            return (int)(7.5f * (targetAmount-2) + 5) * (this.hidden ? 2 : 1);
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
