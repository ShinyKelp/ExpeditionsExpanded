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
    public class StudentChallenge : Challenge, IChallengeHooks
    {
        int targetAmount;
        HashSet<AbstractPhysicalObject.AbstractObjectType> givenObjects = new HashSet<AbstractPhysicalObject.AbstractObjectType>();

        public void ApplyHooks()
        {
            On.SLOracleBehaviorHasMark.GrabObject += SLOracleBehaviorHasMark_GrabObject;
        }
        public void RemoveHooks()
        {
            On.SLOracleBehaviorHasMark.GrabObject -= SLOracleBehaviorHasMark_GrabObject;
        }

        private void SLOracleBehaviorHasMark_GrabObject(On.SLOracleBehaviorHasMark.orig_GrabObject orig, SLOracleBehaviorHasMark self, PhysicalObject item)
        {
            orig(self, item);
            try
            {
                if (!completed)
                {
                    if (!(item is SSOracleSwarmer) && !givenObjects.Contains(item.abstractPhysicalObject.type))
                    {
                        givenObjects.Add(item.abstractPhysicalObject.type);
                        if (givenObjects.Count >= targetAmount)
                            CompleteChallenge();
                        UpdateDescription();
                    }
                }
            }
            catch(Exception ex)
            {
                ECEUtilities.ExpLogger.LogError(ex);
            }
            finally
            {

            }
        }

        ~StudentChallenge()
        {
            givenObjects.Clear();
        }

        public override Challenge Generate()
        {
            return new StudentChallenge
            {
                targetAmount = (int)Mathf.Floor(ExpeditionData.challengeDifficulty * 22) + 2,
                givenObjects = new HashSet<AbstractPhysicalObject.AbstractObjectType>()
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Student");
        }

        public override string ToString()
        {
            string[] eatenTypesArray = new string[givenObjects.Count];
            int i = 0;
            foreach (AbstractPhysicalObject.AbstractObjectType type in givenObjects.ToArray())
            {
                eatenTypesArray[i] = "><" + type.value;
                i++;
            }
            string saveString = string.Concat(
                new string[]
                {
                    "StudentChallenge",
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

            foreach (AbstractPhysicalObject.AbstractObjectType type in givenObjects.ToArray())
            {
                saveString = saveString + "><" + type.value;
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
                        givenObjects.Add(new AbstractPhysicalObject.AbstractObjectType(array[i]));
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
            string iteratorName = ExpeditionData.slugcatPlayer == MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Artificer ? "Pebbles" : "Moon";
            this.description = ChallengeTools.IGT.Translate("Bring <target> different items to <iterator> [<current_amount>/<target>]")
                .Replace("<iterator>", iteratorName)
                .Replace("<target>", ValueConverter.ConvertToString<int>(this.targetAmount))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.givenObjects.Count));
            base.UpdateDescription();
        }

        public override int Points()
        {
            return (int)((targetAmount * 6f + 5) * (hidden ? 2 : 1));
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is StudentChallenge);
        }

        public override void Reset()
        {
            this.givenObjects.Clear();
            base.Reset();
        }

    }

}
