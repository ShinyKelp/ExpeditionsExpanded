using Expedition;
using Menu.Remix;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ExpeditionsExpanded
{
    public class HeistChallenge : Challenge, IChallengeHooks
    {
        bool killedAScav = false, payedToll = false;
        bool doubleHeist;
        string heistedRegion = "_";
        HashSet<EntityID> grabbedPearls = new HashSet<EntityID>();
        HashSet<EntityID> stolenPearls = new HashSet<EntityID>();
        Dictionary<EntityID, string> stolenPearlsDict = new Dictionary<EntityID, string>();

        public void ApplyHooks()
        {
            On.Player.SlugcatGrab += Player_SlugcatGrab;
            On.Player.Regurgitate += Player_Regurgitate;
            On.Player.SpitOutOfShortCut += Player_SpitOutOfShortCut;
            On.ScavengerOutpost.FeeRecieved += ScavengerOutpost_FeeRecieved;
            ECEUtilities.OnHibernated += OnHibernated;
        }
        public void RemoveHooks()
        {
            On.Player.SlugcatGrab -= Player_SlugcatGrab;
            On.Player.Regurgitate -= Player_Regurgitate;
            On.Player.SpitOutOfShortCut -= Player_SpitOutOfShortCut;
            On.ScavengerOutpost.FeeRecieved -= ScavengerOutpost_FeeRecieved;
            ECEUtilities.OnHibernated -= OnHibernated;
        }

        ~HeistChallenge()
        {
            grabbedPearls.Clear();
            stolenPearls.Clear();
            stolenPearlsDict.Clear();
        }

        private void OnHibernated()
        {
            if (!completed)
            {
                killedAScav = false;
                payedToll = false;
                grabbedPearls.Clear();
                stolenPearls.Clear();
            }
        }

        private void ScavengerOutpost_FeeRecieved(On.ScavengerOutpost.orig_FeeRecieved orig, ScavengerOutpost self, Player player, AbstractPhysicalObject item, int value)
        {
            try
            {
                if (!completed)
                {
                    if (item.type == AbstractPhysicalObject.AbstractObjectType.DataPearl)
                        payedToll = true;
                }
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, player, item, value);
            }
        }

        private void Player_SpitOutOfShortCut(On.Player.orig_SpitOutOfShortCut orig, Player self, IntVector2 pos, Room newRoom, bool spitOutAllSticks)
        {
            try
            {
                if (!completed)
                {
                    if (!killedAScav && !payedToll && newRoom.abstractRoom.shelter)
                    {
                        if (self.objectInStomach != null && self.objectInStomach.type == AbstractPhysicalObject.AbstractObjectType.DataPearl && stolenPearlsDict.ContainsKey(self.objectInStomach.ID))
                            CheckCompletion(stolenPearlsDict[self.objectInStomach.ID]);
                        else
                        {
                            foreach (Creature.Grasp grasp in self.grasps)
                            {
                                if (grasp != null && grasp.grabbed is DataPearl p && stolenPearlsDict.ContainsKey(p.abstractPhysicalObject.ID))
                                {
                                    CheckCompletion(stolenPearlsDict[p.abstractPhysicalObject.ID]);
                                    break;
                                }
                            }
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
                orig(self, pos, newRoom, spitOutAllSticks);
            }
        }

        private void CheckCompletion(string region)
        {
            if (!doubleHeist)
                CompleteChallenge();
            else
            {
                if (heistedRegion == "_")
                    heistedRegion = region;
                else if (heistedRegion != region)
                    CompleteChallenge();
                UpdateDescription();
            }
        }

        private void Player_Regurgitate(On.Player.orig_Regurgitate orig, Player self)
        {
            try
            {
                if (!completed)
                {
                    if (self.objectInStomach != null && self.objectInStomach.type == AbstractPhysicalObject.AbstractObjectType.DataPearl)
                        if (!grabbedPearls.Contains(self.objectInStomach.ID))
                            grabbedPearls.Add(self.objectInStomach.ID);
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

        private void Player_SlugcatGrab(On.Player.orig_SlugcatGrab orig, Player self, PhysicalObject obj, int graspUsed)
        {
            try
            {
                if (!completed)
                {

                    if (obj != null && obj.abstractPhysicalObject.type == AbstractPhysicalObject.AbstractObjectType.DataPearl)
                    {
                        if (!grabbedPearls.Contains(obj.abstractPhysicalObject.ID))
                        {
                            grabbedPearls.Add(obj.abstractPhysicalObject.ID);
                            if (self.room.abstractRoom.scavengerOutpost)
                            {
                                if (self.abstractCreature.world.game.session.creatureCommunities.LikeOfPlayer(CreatureCommunities.CommunityID.Scavengers, -1, self.playerState.playerNumber) < 0.25f)
                                {
                                    stolenPearls.Add(obj.abstractPhysicalObject.ID);
                                    stolenPearlsDict.Add(obj.abstractPhysicalObject.ID, self.abstractCreature.world.region.name);
                                }
                            }
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
                orig(self, obj, graspUsed);
            }
        }

        public override bool RespondToCreatureKill()
        {
            return true;
        }

        public override void CreatureKilled(Creature crit, int playerNumber)
        {
            if (crit.abstractCreature.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Scavenger &&
            crit.room.abstractRoom.scavengerOutpost)
                killedAScav = true;

            base.CreatureKilled(crit, playerNumber);
        }

        public override Challenge Generate()
        {
            return new HeistChallenge
            {
                doubleHeist = (ExpeditionData.challengeDifficulty > 0.9f)
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Heist"); ;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "HeistChallenge",
                "~",
                this.doubleHeist ? "1" : "0",
                "><",
                this.heistedRegion,
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
                this.doubleHeist = (array[0] == "1");
                this.heistedRegion = array[1];
                this.completed = (array[2] == "1");
                this.hidden = (array[3] == "1");
                this.revealed = (array[4] == "1");
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                doubleHeist = false;
                heistedRegion = "";
                completed = hidden = revealed = false;
            }
            UpdateDescription();

        }

        public override void UpdateDescription()
        {
            if (this.doubleHeist)
            {
                this.description = ChallengeTools.IGT.Translate("Steal from two tolls without killing, paying or chieftain [<score>/2]").Replace("<score>", (heistedRegion == "_" ? "0" : (completed ? "2" : "1")));
            }
            else
                this.description = ChallengeTools.IGT.Translate("Steal from AngryNoodle_OnHibernated scav toll without killing, paying or chieftain");
            base.UpdateDescription();
        }
        public override int Points()
        {
            int points = (doubleHeist ? 180 : 75);
            return (int)(hidden ? points * 2 : points);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is HeistChallenge);
        }

        public override void Reset()
        {
            killedAScav = false;
            payedToll = false;
            grabbedPearls.Clear();
            stolenPearls.Clear();
            heistedRegion = "_";
            base.Reset();
        }


    }

}
