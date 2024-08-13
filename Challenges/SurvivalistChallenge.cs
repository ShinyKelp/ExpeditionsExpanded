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
    public class SurvivalistChallenge : Challenge, IChallengeHooks
    {
        int survivedCycles = 0;
        int targetCycles;

        public void ApplyHooks()
        {
            ECEUtilities.OnAllPlayersDied += Survivalist_OnDeath;
            ECEUtilities.OnHibernated += Survivalist_OnHibernated;
        }
        public void RemoveHooks()
        {
            ECEUtilities.OnAllPlayersDied -= Survivalist_OnDeath;
            ECEUtilities.OnHibernated -= Survivalist_OnHibernated;
        }

        void Survivalist_OnHibernated()
        {
            if (!completed)
            {
                survivedCycles++;
                if (survivedCycles >= targetCycles)
                    CompleteChallenge();
                UpdateDescription();
            }
        }
        void Survivalist_OnDeath()
        {
            if (!completed)
            {
                survivedCycles = 0;
                UpdateDescription();
            }
        }
      
        public override Challenge Generate()
        {
            return new SurvivalistChallenge
            {
                targetCycles = (int)(ExpeditionData.challengeDifficulty * 12f) + 3
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Survivalist");
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "SurvivalistChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.targetCycles),
                "><",
                ValueConverter.ConvertToString<int>(this.survivedCycles),
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
                this.targetCycles = int.Parse(array[0]);
                this.survivedCycles = int.Parse(array[1]);
                this.completed = (array[2] == "1");
                this.hidden = (array[3] == "1");
                this.revealed = (array[4] == "1");
                if (ECEUtilities.DiedLastSession())
                    this.survivedCycles = 0;
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                targetCycles = 3;
                completed = hidden = revealed = false;
            }
            this.UpdateDescription();
        }
        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Survive " + targetCycles + " cycles without dying [" + survivedCycles + "/" + targetCycles + "]");
            base.UpdateDescription();
        }

        public override int Points()
        {
            int points = (targetCycles-2) * 8;
            return hidden ? points * 2 : points;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is SurvivalistChallenge);
        }

        public override void Reset()
        {
            survivedCycles = 0;
        }
    }
}
