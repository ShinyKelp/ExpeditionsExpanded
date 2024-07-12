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
   public class FriendsForAllChallenge : Challenge
    {
        public int amountToTame;
        HashSet<CreatureTemplate.Type> tamedCreatures = new HashSet<CreatureTemplate.Type>();
        public FriendsForAllChallenge() 
        {
            On.LizardAI.GiftRecieved += LizardAI_GiftRecieved;
        }

        ~FriendsForAllChallenge()
        {
            On.LizardAI.GiftRecieved -= LizardAI_GiftRecieved;
        }

        private void LizardAI_GiftRecieved(On.LizardAI.orig_GiftRecieved orig, LizardAI self, SocialEventRecognizer.OwnedItemOnGround giftOfferedToMe)
        {
            orig(self, giftOfferedToMe);
            try
            {
                if(giftOfferedToMe.owner is Player player)
                {
                    SocialMemory.Relationship rel = self.creature.realizedCreature.State.socialMemory.GetOrInitiateRelationship(giftOfferedToMe.owner.abstractCreature.ID);
                    if (rel.like >= 0.5f)
                        CheckNewFriend(self.creature.creatureTemplate.type);

                }
            }
            catch(Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
            }
        }

        private void CheckNewFriend(CreatureTemplate.Type friendType)
        {
            if (!tamedCreatures.Contains(friendType))
            {
                tamedCreatures.Add(friendType);
                UpdateDescription();
                if (tamedCreatures.Count >= amountToTame)
                    CompleteChallenge();
            }
        }

        public override Challenge Generate()
        {
            return new FriendsForAllChallenge
            {
                amountToTame = 2 + Mathf.FloorToInt(ExpeditionData.challengeDifficulty * 10)
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Friends For All");
        }

        public override string ToString()
        {
            string tamedFriendsStr = "";
            foreach(CreatureTemplate.Type t in tamedCreatures)
            {
                tamedFriendsStr += "><" + t.value;
            }
            return string.Concat(new string[]
            {
                "FriendsForAllChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.amountToTame),
                "><",
                this.completed ? "1" : "0",
                "><",
                this.hidden ? "1" : "0",
                "><",
                this.revealed ? "1" : "0",
                tamedFriendsStr
            });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                this.amountToTame = int.Parse(array[0]);
                this.completed = (array[1] == "1");
                this.hidden = (array[2] == "1");
                this.revealed = (array[3] == "1");
                if (array.Length > 4)
                {
                    for (int i = 4; i < array.Length; i++)
                    {
                        tamedCreatures.Add(new CreatureTemplate.Type(array[i]));
                    }
                }
            }
            catch (Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
                amountToTame = 2;
                completed = hidden = revealed = false;
            }
            this.UpdateDescription();
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Tame " + amountToTame + " different species of lizards [" + tamedCreatures.Count() + "/" + amountToTame + "]");
            base.UpdateDescription();
        }

        public override int Points()
        {
            return amountToTame * 15 + 10;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is FriendsForAllChallenge);
        }

        public override void Reset()
        {
            tamedCreatures.Clear();
        }
    }
}
