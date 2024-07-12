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
   public class ApexMasterChallenge : Challenge
    {
        enum ApexMasterTargets
        {
            TrainLizard,
            ScavengerKing,
            HunterLongLegs
        }
        public CreatureTemplate.Type targetCreature;
        public ApexMasterChallenge()
        {
        }

        ~ApexMasterChallenge()
        {
        }

        public override bool RespondToCreatureKill()
        {
            return true;
        }

        public override void CreatureKilled(Creature crit, int playerNumber)
        {
            if (crit.abstractCreature.creatureTemplate.type == targetCreature)
            {
                CompleteChallenge();
                UpdateDescription();
            }
        }

        public override Challenge Generate()
        {
            CreatureTemplate.Type chosenType;
            System.Random r = new System.Random();
            int select = 1;
            select = r.Next(0, 3);
            switch ((ApexMasterTargets)select)
            {
                case ApexMasterTargets.TrainLizard:
                    chosenType = MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.TrainLizard;
                    break;
                case ApexMasterTargets.ScavengerKing:
                    chosenType = MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.ScavengerKing;
                    break;
                case ApexMasterTargets.HunterLongLegs:
                    chosenType = MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.HunterDaddy;
                    break;
                default:
                    chosenType = MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.TrainLizard;
                    break;
            }
            return new ApexMasterChallenge
            {
                targetCreature = chosenType
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Apex Master");
        }

        public override string ToString()
        {
            return string.Concat(new string[]
           {
                "ApexMasterChallenge",
                "~",
                targetCreature.value,
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
                this.completed = (array[1] == "1");
                this.hidden = (array[2] == "1");
                this.revealed = (array[3] == "1");
            }
            catch (Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
                targetCreature = MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.TrainLizard;
            }
            
            this.UpdateDescription();
        }
        public override void UpdateDescription()
        {
            string critName = "Unknown";
            if(targetCreature == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.HunterDaddy)
                critName = ChallengeTools.IGT.Translate("Hunter Long Legs");
            else if (this.targetCreature.Index >= 0)
                critName = ChallengeTools.IGT.Translate(StaticWorld.GetCreatureTemplate(targetCreature).name);

            this.description = ChallengeTools.IGT.Translate("Kill a <target_creature>")
                .Replace("<target_creature>", critName);
            base.UpdateDescription();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is ApexMasterChallenge apexChallenge && apexChallenge.targetCreature == targetCreature);
        }

        public override int Points()
        {
            int points = 0;
            if (targetCreature == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.TrainLizard)
                points = 150;
            else if (targetCreature == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.HunterDaddy)
                points = 175;
            else if (targetCreature == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.ScavengerKing)
                points = 200;

            return points * (this.hidden ? 2 : 1);
        }

        public override void Reset()
        {
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
