﻿using Expedition;
using Menu.Remix;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ExpeditionsExpanded
{
    public class ApexExpertChallenge : Challenge
    {
        enum ApexExpertTargets
        {
            RedLizard,
            RedCentipede,
            DaddyLongLegs,
            MirosVulture

        }
        public CreatureTemplate.Type targetCreature;
        int targetAmount;
        int targetsKilled;
        public ApexExpertChallenge()
        {
            ExpeditionsExpandedMod.OnAllPlayersDied += OnAllPlayersDied;
        }

        ~ApexExpertChallenge()
        {
            ExpeditionsExpandedMod.OnAllPlayersDied -= OnAllPlayersDied;
        }

        private void OnAllPlayersDied()
        {
            if (!completed)
            {
                targetsKilled = 0;
                UpdateDescription();
            }
        }

        public override bool RespondToCreatureKill()
        {
            return true;
        }

        public override void CreatureKilled(Creature crit, int playerNumber)
        {
            if (!completed && crit.abstractCreature.creatureTemplate.type == targetCreature)
            {
                targetsKilled++;
                if (targetsKilled >= targetAmount)
                    CompleteChallenge();
                UpdateDescription();
            }
        }

        public override Challenge Generate()
        {
            CreatureTemplate.Type chosenType;
            float multiplier = 1f;
            System.Random r = new System.Random();
            int select = 1, extra = 0;
            if (ExpeditionData.challengeDifficulty > 0.5f)
                select = r.Next(0, 4);
            else
                select = r.Next(0, 3);
            switch ((ApexExpertTargets)select)
            {
                case ApexExpertTargets.RedLizard:
                    chosenType = CreatureTemplate.Type.RedLizard;
                    multiplier = 6f;
                    break;
                case ApexExpertTargets.RedCentipede:
                    chosenType = CreatureTemplate.Type.RedCentipede;
                    multiplier = 5f;
                    break;
                case ApexExpertTargets.DaddyLongLegs:
                    chosenType = CreatureTemplate.Type.DaddyLongLegs;
                    multiplier = 4f;
                    break;
                case ApexExpertTargets.MirosVulture:
                    chosenType = MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.MirosVulture;
                    extra = -1;
                    multiplier = 4f;
                    break;
                default:
                    chosenType = CreatureTemplate.Type.RedLizard;
                    break;
            }
            return new ApexExpertChallenge
            {
                targetCreature = chosenType,
                targetAmount = Math.Max(Mathf.CeilToInt(ExpeditionData.challengeDifficulty * multiplier), 2) + extra
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Apex Expert");
        }

        public override string ToString()
        {
            return string.Concat(new string[]
           {
                "ApexExpertChallenge",
                "~",
                targetCreature.value,
                "><",
                ValueConverter.ConvertToString<int>(this.targetAmount),
                "><",
                ValueConverter.ConvertToString<int>(this.targetsKilled),
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
                this.targetCreature = new CreatureTemplate.Type(array[0]);
                this.targetAmount = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.targetsKilled = int.Parse(array[2], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.completed = (array[3] == "1");
                this.hidden = (array[4] == "1");
                this.revealed = (array[5] == "1");
                if (ExpeditionsExpandedMod.DiedLastSession())
                    targetsKilled = 0;
            }
            catch(Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
                targetCreature = CreatureTemplate.Type.RedLizard;
                targetAmount = 2;
                targetsKilled = 0;
                completed = hidden = revealed = false;
            }
            this.UpdateDescription();
        }
        public override void UpdateDescription()
        {
            string critName = "Unknown";
            if (this.targetCreature.Index >= 0)
                critName = ChallengeTools.IGT.Translate(ChallengeTools.creatureNames[this.targetCreature.Index]);

            this.description = ChallengeTools.IGT.Translate("Kill <target_amount> <target_creature> without dying [<current_amount>/<target_amount>]")
                .Replace("<target_amount>", ValueConverter.ConvertToString<int>(this.targetAmount))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.targetsKilled))
                .Replace("<target_creature>", critName);
            base.UpdateDescription();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is ApexExpertChallenge apexChallenge && apexChallenge.targetCreature == targetCreature);
        }

        public override int Points()
        {
            int points = 0;
            if (targetCreature == CreatureTemplate.Type.RedLizard)
                points = 34;
            else if (targetCreature == CreatureTemplate.Type.RedCentipede)
                points = 38;
            else if (targetCreature == CreatureTemplate.Type.DaddyLongLegs)
                points = 46;
            else if (targetCreature == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.MirosVulture)
                points = 66;

            return points * targetAmount * (this.hidden ? 2 : 1);
        }

        public override void Reset()
        {
            targetsKilled = 0;
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
