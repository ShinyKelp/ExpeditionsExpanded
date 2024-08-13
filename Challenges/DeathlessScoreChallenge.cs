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
    public class DeathlessScoreChallenge : Challenge, IChallengeHooks
    {
        int scoreGoal;
        int currentScore;

        public void ApplyHooks()
        {
            ECEUtilities.OnAllPlayersDied += OnAllPlayersDied;
        }
        public void RemoveHooks()
        {
            ECEUtilities.OnAllPlayersDied -= OnAllPlayersDied;
        }

        private void OnAllPlayersDied()
        {
            if (!completed)
            {
                currentScore = 0;
                UpdateDescription();
            }
        }

        public override bool RespondToCreatureKill()
        {
            return true;
        }

        public override void CreatureKilled(Creature crit, int playerNumber)
        {
            if (!completed)
            {
                int score = ChallengeTools.creatureScores.TryGetValue(crit.abstractCreature.creatureTemplate.type.value, out score) ? score : 1;
                currentScore += score;
                if(currentScore >=  scoreGoal)
                {
                    currentScore = scoreGoal;
                    CompleteChallenge();
                }
                UpdateDescription();
            }
        }

        public override Challenge Generate()
        {
            int goal = Mathf.RoundToInt(Mathf.Lerp(40f, 250f, ExpeditionData.challengeDifficulty) / 10f) * 10;
            return new DeathlessScoreChallenge
            {
                scoreGoal = goal
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Deathless Score");
        }

        public override string ToString()
        {
            return string.Concat(new string[]
           {
                "DeathlessScoreChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.scoreGoal),
                "><",
                ValueConverter.ConvertToString<int>(this.currentScore),
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
                this.scoreGoal = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.currentScore = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.completed = (array[2] == "1");
                this.hidden = (array[3] == "1");
                this.revealed = (array[4] == "1");
                if (ECEUtilities.DiedLastSession())
                    currentScore = 0;
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                scoreGoal = 40;
                currentScore = 0;
                completed = hidden = revealed = false;
            }
            
            this.UpdateDescription();
        }
        public override void UpdateDescription()
        {

            this.description = ChallengeTools.IGT.Translate("Earn <score_target> points from creature kills without dying [<current_score>/<score_target>]")
                .Replace("<score_target>", ValueConverter.ConvertToString<int>(this.scoreGoal))
                .Replace("<current_score>", ValueConverter.ConvertToString<int>(currentScore));
            base.UpdateDescription();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is DeathlessScoreChallenge);
        }

        public override int Points()
        {
           

            return (int) (scoreGoal * 0.3f * (this.hidden ? 2 : 1));
        }

        public override void Reset()
        {
            scoreGoal = 0;
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
