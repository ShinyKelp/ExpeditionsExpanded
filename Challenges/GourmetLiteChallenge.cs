using Expedition;
using Menu.Remix;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using UnityEngine;
using MoreSlugcats;
using System.Diagnostics;

namespace ExpeditionsExpanded
{
    public class GourmetLiteChallenge : Challenge, IChallengeHooks
    {
        int targetAmount;
        Dictionary<EntityID, List<AbstractPhysicalObject.AbstractObjectType>> likedEdibles = new Dictionary<EntityID, List<AbstractPhysicalObject.AbstractObjectType>>();

        public void ApplyHooks()
        {
            On.MoreSlugcats.SlugNPCAI.AteFood += SlugNPCAI_AteFood;
            On.Player.Die += Player_Die;
            On.ShelterDoor.Close += ShelterDoor_Close;
        }
        public void RemoveHooks()
        {
            On.MoreSlugcats.SlugNPCAI.AteFood -= SlugNPCAI_AteFood;
            On.Player.Die -= Player_Die;
            On.ShelterDoor.Close -= ShelterDoor_Close;
        }

        ~GourmetLiteChallenge()
        {
            foreach (List<AbstractPhysicalObject.AbstractObjectType> list in likedEdibles.Values)
                list.Clear();
            likedEdibles.Clear();
        }

        private int foodCount { 
            get {
                int count = 0;
                foreach (List<AbstractPhysicalObject.AbstractObjectType> list in likedEdibles.Values)
                    count += list.Count();
                return count;
            } 
        }

        private void SlugNPCAI_AteFood(On.MoreSlugcats.SlugNPCAI.orig_AteFood orig, SlugNPCAI self, PhysicalObject food)
        {

            orig(self, food);
            try
            {
                if (ExpeditionsExpandedMod.lastPupFoodLiked && !completed)
                {
                    if (likedEdibles.ContainsKey(self.cat.abstractCreature.ID))
                    {
                        if (!likedEdibles[self.cat.abstractCreature.ID].Contains(food.abstractPhysicalObject.type))
                        {
                            likedEdibles[self.cat.abstractCreature.ID].Add(food.abstractPhysicalObject.type);
                            if(foodCount >= targetAmount)
                                CompleteChallenge();
                            UpdateDescription();
                        }
                        else
                        {
                            UpdateDescription();
                        }
                    }
                    else
                    {
                        likedEdibles.Add(self.cat.abstractCreature.ID, new List<AbstractPhysicalObject.AbstractObjectType>());
                        likedEdibles[self.cat.abstractCreature.ID].Add(food.abstractPhysicalObject.type);
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
                ExpeditionsExpandedMod.lastPupFoodLiked = false;
            }
        }

        private void ShelterDoor_Close(On.ShelterDoor.orig_Close orig, ShelterDoor self)
        {
            try
            {
                if (!completed && likedEdibles.Count > 0)
                {
                    List<EntityID> pups = new List<EntityID>();
                    foreach (AbstractCreature crit in self.room.abstractRoom.creatures)
                    {
                        if (crit.realizedCreature != null && crit.realizedCreature is Player player && player.isNPC)
                            pups.Add(crit.ID);
                    }
                    List<EntityID> abandonedPups = new List<EntityID>();
                    foreach (EntityID id in likedEdibles.Keys)
                        if (!pups.Contains(id))
                            abandonedPups.Add(id);
                    foreach (EntityID id in abandonedPups)
                        likedEdibles.Remove(id);
                    if (abandonedPups.Count > 0)
                        UpdateDescription();
                }
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self);
            }
        }

        private void Player_Die(On.Player.orig_Die orig, Player self)
        {
            try
            {
                if (!completed)
                {
                    if (self.isNPC)
                    {
                        if (likedEdibles.ContainsKey(self.abstractCreature.ID))
                        {
                            likedEdibles[self.abstractCreature.ID].Clear();
                            likedEdibles.Remove(self.abstractCreature.ID);
                            UpdateDescription();
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
                orig(self);
            }
        }

        public override Challenge Generate()
        {
            return new GourmetLiteChallenge
            {
                targetAmount = (int)Mathf.Clamp(Mathf.Ceil(ExpeditionData.challengeDifficulty * 3f), 1, 3),
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Gourmet Lite");
        }

        public override string ToString()
        {
            int totalCount = 0;
            foreach(List<AbstractPhysicalObject.AbstractObjectType> list in likedEdibles.Values)
            {
                if(list.Count > 0)
                    totalCount += 1 + list.Count;
            }
            List<string> allLikedEdibles = new List<string>();

            foreach (EntityID id in likedEdibles.Keys)
            {
                if (likedEdibles[id].Count > 0)
                {
                    allLikedEdibles.Add("PUP_" + id.ToString());
                    foreach (AbstractPhysicalObject.AbstractObjectType type in likedEdibles[id])
                        allLikedEdibles.Add(type.ToString());
                }
                
            }
            string saveString = string.Concat(
                new string[]
                {
                    "GourmetLiteChallenge",
                    "~",
                    ValueConverter.ConvertToString<int>(this.targetAmount),
                    "><",
                    this.completed ? "1" : "0",
                    "><",
                    this.hidden ? "1" : "0",
                    "><",
                    this.revealed ? "1" : "0"
                }
                );
            if (allLikedEdibles.Count > 0)
                saveString += "><";

            foreach (string foodType in allLikedEdibles)
            {
                saveString = saveString + "><" + foodType;
            }
            return saveString;
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
                    EntityID currentPupID = new EntityID();
                    for (int i = 4; i < array.Length; i++)
                    {
                        if (array[i].StartsWith("PUP_"))
                        {
                            currentPupID = EntityID.FromString(array[i].Substring(4));
                            likedEdibles.Add(currentPupID, new List<AbstractPhysicalObject.AbstractObjectType>());
                        }
                        else
                        {
                            if(likedEdibles.ContainsKey(currentPupID))
                            {
                                likedEdibles[currentPupID].Add(new AbstractPhysicalObject.AbstractObjectType(array[i]));
                            }
                        }
                    }
                }
                    
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                this.targetAmount = 1;
                this.completed = this.hidden = this.revealed = false;
            }
            this.UpdateDescription();
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Give slugpups " + targetAmount + 
                " different favored foods [" + foodCount + "/"+ targetAmount + "]");
            base.UpdateDescription();
        }

        public override int Points()
        {
            return (int)((targetAmount * 23f + 30) * (hidden ? 2 : 1));
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is GourmetLiteChallenge);
        }

        public override void Reset()
        {
            this.likedEdibles.Clear();
            base.Reset();
        }
    }

}
