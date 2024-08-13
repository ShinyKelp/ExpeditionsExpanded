using Expedition;
using Menu.Remix;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ExpeditionsExpanded
{
    public class GourmetChallenge : Challenge, IChallengeHooks
    {
        int targetAmount;
        HashSet<string> eatenEdibleTypes = new HashSet<string>();
        ~GourmetChallenge()
        {
            eatenEdibleTypes.Clear();
        }
        public void ApplyHooks()
        {
            On.Player.ObjectEaten += Player_ObjectEaten;
        }
        public void RemoveHooks()
        {
            On.Player.ObjectEaten -= Player_ObjectEaten;
        }

        private void Player_ObjectEaten(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            try
            {
                if (!completed)
                {
                    string eatenType = "";
                    if (edible is Creature creature)
                        eatenType = creature.abstractCreature.creatureTemplate.type.value;
                    else if (edible is PhysicalObject physObj)
                        eatenType = physObj.abstractPhysicalObject.type.value;
                    
                    if (!eatenEdibleTypes.Contains(eatenType))
                    {
                        eatenEdibleTypes.Add(eatenType);
                        if (eatenEdibleTypes.Count == targetAmount)
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
                orig(self, edible);
            }
        }

        public override Challenge Generate()
        {
            return new GourmetChallenge
            {
                targetAmount = (int)Mathf.Floor(ExpeditionData.challengeDifficulty * 12) + 3,
                eatenEdibleTypes = new HashSet<string>()
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Gourmet Diet");
        }

        public override string ToString()
        {
            string saveString = string.Concat(
                new string[]
                {
                    "GourmetChallenge",
                    "~",
                    ValueConverter.ConvertToString<int>(this.targetAmount),
                    "><",
                    this.completed ? "1" : "0",
                    "><",
                    this.hidden ? "1" : "0",
                    "><",
                    this.revealed ? "1" : "0",
                }
                );

            foreach (string type in eatenEdibleTypes.ToArray())
            {
                saveString = saveString + "><" + type;
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
                    for (int i = 4; i < array.Length; i++)
                        eatenEdibleTypes.Add(array[i]);
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                this.targetAmount = 5;
                this.completed = this.hidden = this.revealed = false;
            }
            this.UpdateDescription();
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Consume <target> different edibles [<current_amount>/<target>]")
                .Replace("<target>", ValueConverter.ConvertToString<int>(this.targetAmount))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.eatenEdibleTypes.Count));
            base.UpdateDescription();
        }

        public override int Points()
        {
            return (int)((targetAmount * 11f - 25) * (hidden ? 2 : 1));
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is GourmetChallenge);
        }

        public override void Reset()
        {
            this.eatenEdibleTypes.Clear();
            base.Reset();
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            if (ExpeditionsExpandedMod.HasCustomPlayer1)
                return true;
            return slugcat != MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Spear;
        }

    }

}
