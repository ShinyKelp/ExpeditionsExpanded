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
    public class AngryNoodleChallenge : Challenge, IChallengeHooks
    {
        public int targetAmount;
        int currentAmount = 0;
        bool killedAMother = false;

        public void ApplyHooks()
        {
            ECEUtilities.OnHibernated += AngryNoodle_OnHibernated;
            On.SmallNeedleWorm.Scream += SmallNeedleWorm_Scream;
        }
        public void RemoveHooks()
        {
            ECEUtilities.OnHibernated -= AngryNoodle_OnHibernated;
            On.SmallNeedleWorm.Scream -= SmallNeedleWorm_Scream;
        }

        private void SmallNeedleWorm_Scream(On.SmallNeedleWorm.orig_Scream orig, SmallNeedleWorm self)
        {
            try
            {
                if(!completed && !killedAMother)
                {
                    if(!self.hasScreamed && self.ClosestCreature() is Player)
                    {
                        currentAmount++;
                        if (currentAmount >= targetAmount)
                            CompleteChallenge();
                        UpdateDescription();
                    }
                }
            }
            catch(Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self);
            }
        }

        private void AngryNoodle_OnHibernated()
        {
            killedAMother = false;
        }

        public override bool RespondToCreatureKill()
        {
            return true;
        }

        public override void CreatureKilled(Creature crit, int playerNumber)
        {
            if (!completed)
            {
                if(crit.Template.TopAncestor().type == CreatureTemplate.Type.BigNeedleWorm &&
                    crit.Template.type != CreatureTemplate.Type.SmallNeedleWorm)
                {
                    currentAmount = 0;
                    killedAMother = true;
                    UpdateDescription();
                }
            }
        }

        public override Challenge Generate()
        {
            return new AngryNoodleChallenge
            {
                targetAmount = (int)(ExpeditionData.challengeDifficulty * 10f) + 2
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Angry Noodle");
        }

        public override string ToString()
        {
            
            return string.Concat(new string[]
            {
                "AngryNoodleChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.targetAmount),
                "><",
                ValueConverter.ConvertToString<int>(this.currentAmount),
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
                this.currentAmount = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.completed = (array[2] == "1");
                this.hidden = (array[3] == "1");
                this.revealed = (array[4] == "1");

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
            UnityEngine.Debug.Log("AngryNoodle description.");
            this.description = ChallengeTools.IGT.Translate("Eat/kill <target_amount> small noodleflies without killing a mother [<current_amount>/<target_amount>]")
                .Replace("<target_amount>", ValueConverter.ConvertToString<int>(this.targetAmount))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.currentAmount));
            base.UpdateDescription();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is AngryNoodleChallenge);
        }

        public override int Points()
        {
            return (8 * (targetAmount) - 4) * (this.hidden ? 2 : 1);
        }

        public override void Reset()
        {
            currentAmount = 0;
            base.Reset();
        }

    }

}
