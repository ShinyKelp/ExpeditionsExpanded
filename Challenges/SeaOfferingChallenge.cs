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
    public class SeaOfferingChallenge : Challenge, IChallengeHooks
    {
        public int totalOffers;
        int currentOffers = 0;
        EntityID offeringID = new EntityID();
        float lastThrown = 0F;

        public void ApplyHooks()
        {
            On.Player.ThrowObject += Player_ThrowObject;
            On.BigEel.Crush += BigEel_Crush;
        }
        public void RemoveHooks()
        {
            On.Player.ThrowObject -= Player_ThrowObject;
            On.BigEel.Crush -= BigEel_Crush;
        }

        private void Player_ThrowObject(On.Player.orig_ThrowObject orig, Player self, int grasp, bool eu)
        {
            try
            {
                if (!completed)
                {
                    if (self.grasps[grasp] != null && self.grasps[grasp].grabbed is Creature crit)
                    {
                        if (ECEUtilities.Critters.Contains(crit.abstractCreature.creatureTemplate.type) && !crit.dead)
                        {
                            offeringID = crit.abstractCreature.ID;
                            lastThrown = Time.time;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, grasp, eu);
            }
        }

        private void BigEel_Crush(On.BigEel.orig_Crush orig, BigEel self, PhysicalObject obj)
        {
            try
            {
                if (!completed)
                    if (obj != null && obj.abstractPhysicalObject.ID == offeringID)
                        if (Time.time - lastThrown < 8f)
                        {
                            currentOffers++;
                            if (currentOffers >= totalOffers)
                                CompleteChallenge();
                            UpdateDescription();
                        }
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, obj);
            }
        }



        public override Challenge Generate()
        {
            int totalOfferings;
            if (ExpeditionData.challengeDifficulty > 0.9f)
                totalOfferings = 3;
            else if (ExpeditionData.challengeDifficulty >= 0.5f)
                totalOfferings = 2;
            else
                totalOfferings = 1;

            return new SeaOfferingChallenge
            {
                totalOffers = totalOfferings
            };
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "SeaOfferingChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.totalOffers),
                "><",
                ValueConverter.ConvertToString<int>(this.currentOffers),
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
                this.totalOffers = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.currentOffers = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.completed = (array[2] == "1");
                this.hidden = (array[3] == "1");
                this.revealed = (array[4] == "1");
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                this.totalOffers = 1;
                this.currentOffers = 0;
                completed = hidden = revealed = false;
            }
            this.UpdateDescription();
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Sea Offering");
        }

        public override void UpdateDescription()
        {

            this.description = ChallengeTools.IGT.Translate("Offer <total_amount> critters alive to a Leviathan [<current_amount>/<total_amount>]"
                .Replace("<total_amount>", totalOffers.ToString())
                .Replace("<current_amount>", currentOffers.ToString()));

            base.UpdateDescription();
        }

        public override int Points()
        {
            
            return (totalOffers * 15 + 30) * (this.hidden ? 2 : 1);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is SeaOfferingChallenge);
        }

    }

}
