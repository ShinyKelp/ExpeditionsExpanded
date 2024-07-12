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
    public class RottenOfferingChallenge : Challenge
    {
        public int totalOffers;
        int currentOffers;
        EntityID offeringID;
        float lastThrown;
        public RottenOfferingChallenge()
        {
            On.Player.ThrowObject += Player_ThrowObject;
            On.DaddyLongLegs.Collide += DaddyLongLegs_Collide;
            offeringID = new EntityID();
            lastThrown = 0f;
        }

        private void DaddyLongLegs_Collide(On.DaddyLongLegs.orig_Collide orig, DaddyLongLegs self, PhysicalObject otherObject, int myChunk, int otherChunk)
        {
            try
            {
                if (!completed && Time.time - lastThrown < 8f)
                {
                    Tracker.CreatureRepresentation creatureRepresentation = self.AI.tracker.RepresentationForObject(otherObject, false);
                    if (creatureRepresentation != null && self.AI.DynamicRelationship(creatureRepresentation).type == CreatureTemplate.Relationship.Type.Eats && self.CheckDaddyConsumption(otherObject))
                    {
                        bool flag = false;
                        if (!self.SizeClass && self.digestingCounter > 0)
                        {
                            return;
                        }
                        int num = 0;
                        while (num < self.tentacles.Length && !flag)
                        {
                            if (self.tentacles[num].grabChunk != null && self.tentacles[num].grabChunk.owner == otherObject)
                            {
                                flag = true;
                            }
                            num++;
                        }
                        if (flag)
                        {
                            if (otherObject.abstractPhysicalObject.ID == offeringID)
                            {
                                currentOffers++;
                                if (currentOffers >= totalOffers)
                                    CompleteChallenge();
                                UpdateDescription();
                                offeringID = new EntityID();
                            }
                        }
                    }
                }
            }catch(Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, otherObject, myChunk, otherChunk);
            }
            
        }

        ~RottenOfferingChallenge()
        {
            On.Player.ThrowObject -= Player_ThrowObject;
            On.DaddyLongLegs.Collide -= DaddyLongLegs_Collide;
        }

     

        private void Player_ThrowObject(On.Player.orig_ThrowObject orig, Player self, int grasp, bool eu)
        {
            try
            {
                if (!completed)
                {
                    if (self.grasps[grasp] != null && self.grasps[grasp].grabbed is Creature crit)
                    {
                        if (ExpeditionsExpandedMod.Critters.Contains(crit.abstractCreature.creatureTemplate.type) && !crit.dead)
                        {
                            offeringID = crit.abstractCreature.ID;
                            lastThrown = Time.time;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, grasp, eu);
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

            return new RottenOfferingChallenge
            {
                totalOffers = totalOfferings
            };
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "RottenOfferingChallenge",
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
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
                totalOffers = 1;
                currentOffers = 0;
                completed = hidden = revealed = false;
            }
            this.UpdateDescription();
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Rotten Offering");
        }

        public override void UpdateDescription()
        {

            this.description = ChallengeTools.IGT.Translate("Offer <total_amount> critters alive to a Long Legs [<current_amount>/<total_amount>]"
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
            return !(challenge is RottenOfferingChallenge);
        }

    }

}
