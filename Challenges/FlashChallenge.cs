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

    public class FlashChallenge : Challenge
    {
        int targetAmount;
        int blindedKills;
        public CreatureTemplate.Type targetCreature;
        private enum FlashChallengeTargets
        {
            Lizard,
            Scavenger,
            Dropwig,
            Vulture
        }
        public FlashChallenge()
        {
            blindedKills = 0;
        }

        ~FlashChallenge()
        {
        }

        public override bool RespondToCreatureKill()
        {
            return true;
        }

        public override void CreatureKilled(Creature crit, int playerNumber)
        {
            if (crit.abstractCreature.creatureTemplate.TopAncestor().type == targetCreature && crit.Blinded)
            {
                blindedKills++;
                if (blindedKills == targetAmount)
                    CompleteChallenge();
                UpdateDescription();
            }
            base.CreatureKilled(crit, playerNumber);
        }
        public override Challenge Generate()
        {
            CreatureTemplate.Type chosenType;
            float multiplier = 1f;
            System.Random r = new System.Random();
            int select = r.Next(0, 4);
            switch ((FlashChallengeTargets)select)
            {
                case FlashChallengeTargets.Dropwig:
                    chosenType = CreatureTemplate.Type.DropBug;
                    multiplier = 1.3f;
                    break;
                case FlashChallengeTargets.Lizard:
                    chosenType = CreatureTemplate.Type.LizardTemplate;
                    break;
                case FlashChallengeTargets.Scavenger:
                    chosenType = CreatureTemplate.Type.Scavenger;
                    multiplier = 1.5f;
                    break;
                case FlashChallengeTargets.Vulture:
                    chosenType = CreatureTemplate.Type.Vulture;
                    multiplier = 0.6f;
                    break;
                default:
                    chosenType = CreatureTemplate.Type.LizardTemplate;
                    break;

            }
            return new FlashChallenge
            {
                targetCreature = chosenType,
                targetAmount = (int)(ExpeditionData.challengeDifficulty * 5f * multiplier + 1),
                blindedKills = 0
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Flashing");
        }

        public override string ToString()
        {
            return string.Concat(new string[]
           {
                "FlashChallenge",
                "~",
                targetCreature.value,
                "><",
                ValueConverter.ConvertToString<int>(this.targetAmount),
                "><",
                ValueConverter.ConvertToString<int>(this.blindedKills),
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
                this.blindedKills = int.Parse(array[2], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.completed = (array[3] == "1");
                this.hidden = (array[4] == "1");
                this.revealed = (array[5] == "1");
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                targetCreature = CreatureTemplate.Type.Scavenger;
                targetAmount = 2;
                blindedKills = 0;
                completed = revealed = hidden = false;
            }
            this.UpdateDescription();
        }

        public override void UpdateDescription()
        {
            string critName = "Unknown";
            if (this.targetCreature == CreatureTemplate.Type.LizardTemplate)
                critName = "Lizards";
            else if (this.targetCreature.Index >= 0)
                critName = ChallengeTools.IGT.Translate(ChallengeTools.creatureNames[this.targetCreature.Index]);

            this.description = ChallengeTools.IGT.Translate("Kill <target_amount> <target_creature> while they are blinded [<current_amount>/<target_amount>]")
                .Replace("<target_amount>", ValueConverter.ConvertToString<int>(this.targetAmount))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.blindedKills))
                .Replace("<target_creature>", critName);
            base.UpdateDescription();
        }

        public override int Points()
        {
            return (int)(15 + (targetAmount * 11f)) * (hidden ? 2 : 1);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is FlashChallenge flash && flash.targetCreature == this.targetCreature);
        }

        public override void Reset()
        {
            blindedKills = 0;
            base.Reset();
        }
    }


}
