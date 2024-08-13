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
    public class TruePinChallenge : Challenge, IChallengeHooks
    {
        string lastPinnedCreature = "";
        public int targetAmount;
        int currentAmount = 0;

        public void ApplyHooks()
        {
            On.Spear.LodgeInCreature_CollisionResult_bool_bool += Spear_LodgeInCreature_CollisionResult_bool_bool;
        }

        public void RemoveHooks()
        {
            On.Spear.LodgeInCreature_CollisionResult_bool_bool -= Spear_LodgeInCreature_CollisionResult_bool_bool;
        }

        private void Spear_LodgeInCreature_CollisionResult_bool_bool(On.Spear.orig_LodgeInCreature_CollisionResult_bool_bool orig, Spear self, SharedPhysics.CollisionResult result, bool eu, bool isJellyFish)
        {
            orig(self, result, eu, isJellyFish);
            try
            {
                if (!completed)
                {
                    if (self.pinToWallCounter > 299 && result.chunk.owner is Creature crit && !crit.dead && crit.abstractCreature.ID.ToString() != lastPinnedCreature)
                    {
                        lastPinnedCreature = crit.abstractCreature.ID.ToString();
                        currentAmount++;
                        if (currentAmount >= targetAmount)
                            CompleteChallenge();
                        UpdateDescription();
                    }

                }
            }
            catch(Exception ex)
            {
                ECEUtilities.ExpLogger.LogError(ex);
            }
        }


        public override Challenge Generate()
        {
            
            return new TruePinChallenge
            {
                targetAmount = (int)(ExpeditionData.challengeDifficulty * 16f) + 4
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("True Spear Pinning");
        }

        public override string ToString()
        {
            
            return string.Concat(new string[]
            {
                "TruePinChallenge",
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
                this.targetAmount = 2;
                revealed = hidden = completed = false;
            }
            this.UpdateDescription();
        }
        public override void UpdateDescription()
        {

            this.description = ChallengeTools.IGT.Translate("Pin <target_amount> ALIVE creatures to walls or floors [<current_amount>/<target_amount>]")
                .Replace("<target_amount>", ValueConverter.ConvertToString<int>(this.targetAmount))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.currentAmount));
            base.UpdateDescription();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is TruePinChallenge || challenge is PinChallenge);
        }

        public override int Points()
        {
            return (9 * (targetAmount)) * (this.hidden ? 2 : 1);
        }

        public override void Reset()
        {
            currentAmount = 0;
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
