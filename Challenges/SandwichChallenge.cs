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
    public class SandwichChallenge : Challenge, IChallengeHooks
    {
        int eatenMushrooms = 0;
        int mushroomsToEat;

        public void ApplyHooks()
        {
            On.Player.ObjectEaten += Player_ObjectEaten;
            On.Spear.HitSomethingWithoutStopping += Spear_HitSomethingWithoutStopping;
            ECEUtilities.OnHibernated += OnHibernated;
        }
        public void RemoveHooks()
        {
            On.Player.ObjectEaten -= Player_ObjectEaten;
            On.Spear.HitSomethingWithoutStopping -= Spear_HitSomethingWithoutStopping;
            ECEUtilities.OnHibernated -= OnHibernated;
        }

        private void Spear_HitSomethingWithoutStopping(On.Spear.orig_HitSomethingWithoutStopping orig, Spear self, PhysicalObject obj, BodyChunk chunk, PhysicalObject.Appendage appendage)
        {
            try
            {
                if (!completed && self.Spear_NeedleCanFeed() && obj is Mushroom)
                {
                    eatenMushrooms++;
                    if (eatenMushrooms == mushroomsToEat)
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
                orig(self, obj, chunk, appendage);
            }
        }

        private void Player_ObjectEaten(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            try
            {
                if (!completed && (edible is PhysicalObject physObj && physObj.abstractPhysicalObject.type == AbstractPhysicalObject.AbstractObjectType.Mushroom))
                {
                    eatenMushrooms++;
                    if (eatenMushrooms == mushroomsToEat)
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
                orig(self, edible);
            }
        }

        ~SandwichChallenge()
        {

        }

        private void OnHibernated()
        {
            if (!completed)
            {
                if (eatenMushrooms > 0)
                {
                    eatenMushrooms = 0;
                    UpdateDescription();
                }
            }
        }

        public override Challenge Generate()
        {
            return new SandwichChallenge
            {
                eatenMushrooms = 0,
                mushroomsToEat = (int)Mathf.Floor(ExpeditionData.challengeDifficulty * 13f) + 3
            };
        }

        public override string ChallengeName()
        {
            return "'" + ChallengeTools.IGT.Translate("Sandwich") + "'";
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "SandwichChallenge",
                "~",
                ValueConverter.ConvertToString<int>(this.mushroomsToEat),
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
                this.mushroomsToEat = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.completed = (array[1] == "1");
                this.hidden = (array[2] == "1");
                this.revealed = (array[3] == "1");
            }
            catch (Exception e)
            {
                ECEUtilities.ExpLogger.LogError(e);
                mushroomsToEat = 3;
                completed = hidden = revealed = false;
            }
            this.UpdateDescription();

        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Eat <target> mushrooms in one cycle [<current_amount>/<target>]")
                .Replace("<target>", ValueConverter.ConvertToString<int>(this.mushroomsToEat))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.eatenMushrooms));
            base.UpdateDescription();
        }

        public override int Points()
        {
            return (int)((mushroomsToEat * 9f) - 15) * (hidden ? 2 : 1);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is SandwichChallenge);
        }


    }

}
