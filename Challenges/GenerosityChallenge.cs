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
    public class GenerosityChallenge : Challenge, IChallengeHooks
    {
        public HashSet<EntityID> giftedMasks = new HashSet<EntityID>();   
        int targetAmount;

        ~GenerosityChallenge()
        {
            giftedMasks.Clear();
        }
        public void ApplyHooks()
        {
            On.ScavengerAI.RecognizePlayerOfferingGift += ScavengerAI_RecognizePlayerOfferingGift;
        }
        public void RemoveHooks()
        {
            On.ScavengerAI.RecognizePlayerOfferingGift -= ScavengerAI_RecognizePlayerOfferingGift;
        }

        private void ScavengerAI_RecognizePlayerOfferingGift(On.ScavengerAI.orig_RecognizePlayerOfferingGift orig, ScavengerAI self, Tracker.CreatureRepresentation subRep, Tracker.CreatureRepresentation objRep, bool objIsMe, PhysicalObject item)
        {
            try
            {
                if (!completed)
                {
                    if(ExpeditionsExpandedMod.HasShinyShieldMask && IsMasklessElite(self.scavenger) && objIsMe && item is VultureMask mask && !giftedMasks.Contains(mask.abstractPhysicalObject.ID))
                    {
                        giftedMasks.Add(mask.abstractPhysicalObject.ID);
                        if (giftedMasks.Count >= targetAmount)
                            CompleteChallenge();
                        UpdateDescription();
                    }
                }
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, subRep, objRep, objIsMe, item);
            }
        }

        private bool IsMasklessElite(Scavenger scav)
        {
            Creature.Grasp[] grasps = scav.grasps;
            bool alreadyHasMask = false;
            if(grasps != null)
            {
                foreach(Creature.Grasp grasp in grasps)
                {
                    if(grasp != null && grasp.grabbed is VultureMask)
                    {
                        alreadyHasMask = true;
                        break;
                    }
                }
            }

            return (scav.Elite && !scav.dead && !alreadyHasMask && scav.readyToReleaseMask);
        }

        public override Challenge Generate()
        {
            return new GenerosityChallenge
            {
                targetAmount = (int)(ExpeditionData.challengeDifficulty * 7f) + 1
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Generosity");
        }

        public override string ToString()
        {
            string giftedMasksStr = "";
            foreach (EntityID id in giftedMasks)
            {
                giftedMasksStr += "><" + id.ToString();
            }
            return string.Concat(new string[]
           {
                "GenerosityChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.targetAmount),
                "><",
                this.completed ? "1" : "0",
                "><",
                this.hidden ? "1" : "0",
                "><",
                this.revealed ? "1" : "0",
                giftedMasksStr
           });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                this.targetAmount = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.completed = (array[1] == "1");
                this.hidden = (array[2] == "1");
                this.revealed = (array[3] == "1");
                if (array.Length > 4)
                {
                    for (int i = 4; i < array.Length; i++)
                    {
                        giftedMasks.Add(EntityID.FromString(array[i]));
                    }
                }
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                targetAmount = 2;
                completed = hidden = revealed = false;
            }
            
            this.UpdateDescription();
        }

        public override void UpdateDescription()
        {

            this.description = ChallengeTools.IGT.Translate("Give <target_amount> masks to elite scavengers in need [<current_amount>/<target_amount>]"
                .Replace("<target_amount>", ValueConverter.ConvertToString<int>(targetAmount))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(giftedMasks.Count)));
            if (!ExpeditionsExpandedMod.HasShinyShieldMask)
                this.description = "[MISSING REQUIRED MOD: ShinyShieldMask] " + description;
            base.UpdateDescription();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is GenerosityChallenge);
        }

        public override int Points()
        {
            return targetAmount * 12 + 10;
        }

        public override void Reset()
        {
            giftedMasks.Clear();
            base.Reset();
        }
        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            if (!ExpeditionsExpandedMod.HasShinyShieldMask)
                return false;
            if (!ExpeditionsExpandedMod.HasCustomPlayer1 && ExpeditionData.slugcatPlayer ==
                MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Artificer)
                return false;
            return base.ValidForThisSlugcat(slugcat);
        }

    }
}
