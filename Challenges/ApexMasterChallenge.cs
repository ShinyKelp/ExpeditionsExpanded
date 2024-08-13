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

        public CreatureTemplate.Type targetCreature;

        public override bool RespondToCreatureKill()
        {
            return true;
        }

        public override void CreatureKilled(Creature crit, int playerNumber)
        {
            if (!completed)
            {
                if (crit.abstractCreature.creatureTemplate.type == targetCreature)
                {
                    if (targetCreature == CreatureTemplate.Type.KingVulture &&
                        !crit.abstractCreature.superSizeMe)
                        return;
                    CompleteChallenge();
                    UpdateDescription();
                }
            }
        }

        public override Challenge Generate()
        {
            CreatureTemplate.Type chosenType;
            System.Random r = new System.Random();

            List<CreatureTemplate.Type> candidateTypes = new List<CreatureTemplate.Type>()
            {
                CreatureTemplate.Type.KingVulture,
                MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.TrainLizard,
                MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.ScavengerKing,
                MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.HunterDaddy
            };
            if (ExpeditionsExpandedMod.HasRedHorror)
                candidateTypes.Add(new CreatureTemplate.Type("RedHorrorCenti"));
            return new ApexMasterChallenge
            {
                targetCreature = candidateTypes[UnityEngine.Random.Range(0, candidateTypes.Count)]
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
                ECEUtilities.ExpLogger.LogError(e);
                targetCreature = MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.TrainLizard;
            }
            
            this.UpdateDescription();
        }
        public override void UpdateDescription()
        {
            string critName = "Unknown";
            if (targetCreature == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.HunterDaddy)
                critName = ChallengeTools.IGT.Translate("Hunter Long Legs");
            else if (targetCreature == CreatureTemplate.Type.KingVulture)
                critName = ChallengeTools.IGT.Translate("Albino King Vulture");
            else if (ExpeditionsExpandedMod.HasRedHorror && targetCreature == new CreatureTemplate.Type("RedHorrorCenti"))
                critName = "Red Horror";
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
            if (targetCreature == CreatureTemplate.Type.KingVulture)
                points = 160;
            if (targetCreature == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.TrainLizard)
                points = 170;
            else if (targetCreature == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.HunterDaddy)
                points = 150;
            else if (targetCreature == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.ScavengerKing)
                points = 180;
            else if (targetCreature.value == "RedHorrorCenti")
                points = 175;
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
