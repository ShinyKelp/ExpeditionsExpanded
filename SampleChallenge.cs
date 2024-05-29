using Expedition;
using ExpeditionsExpanded;
using Menu.Remix;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

public class SampleChallenge : Challenge
{
    #region Declaration
    //Variables
    int cycleGoalProgression;
    int deathlessGoalProgression;
    int goalToReach;
    List<object> someList;

    //In Constructor, hook to any methods you want
    public SampleChallenge()
    {
        On.Player.ObjectEaten += Player_ObjectEaten;

        /*
        * The state of a challenge during a playthrough and the state of a challenge in a savefile are independent from each other.
        * The state is only ever saved after each successful cycle.
        * The state is only ever loaded when entering the expedition, or when clicking "Continue" in the death screen.
        * This makes it tricky to alter progress after death, because any changes you make on death will immediately be overriden by the previous savestate.
        * Likewise, your variables are not going to be reset and re-read from savefile upon a successful cycle, but they WILL if the player exits back to the menu and re-enters.
        * So, to make it a lot easier to track progress, I have created two events that are called on player death and hibernation, and they ensure that variables will stay consistent    
        * between in-game states and savefile states.
        * Still, some manual tracking will be required (see FromString method)
        */
        ExpeditionsExpanded.ExpeditionsExpandedMod.OnAllPlayersDied += AllPlayersDied;  //Called when all players die in expedition.
                                                                                        //Recommended to use for progress reset on death.
        ExpeditionsExpanded.ExpeditionsExpandedMod.OnHibernated += Hibernated;  //Called when player hibernates successfully.
                                                                                //Recommended to use for progress or variable reset on cycle end.

        //Recommended to initialize your variables here, most importantly those that are not saved in savefiles.
        cycleGoalProgression = 0;
        deathlessGoalProgression = 0;
        someList = new List<object>();
    }

    //In Destructor, remember to remove hook listeners
    ~SampleChallenge()
    {
        On.Player.ObjectEaten -= Player_ObjectEaten;
        ExpeditionsExpanded.ExpeditionsExpandedMod.OnHibernated -= Hibernated;
        ExpeditionsExpanded.ExpeditionsExpandedMod.OnAllPlayersDied -= AllPlayersDied;
        //If you have any lists, arrays, etc; clear them here.
        someList.Clear();
    }

    #endregion

    #region Listeners
       
    private void Player_ObjectEaten(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
    {
        try
        {
            if (!completed)
            {
                cycleGoalProgression++;
                if (cycleGoalProgression == goalToReach)
                    CompleteChallenge();    //This function will complete the challenge successfully.
                                            //Completion will not persist if the player does not hibernate.
                UpdateDescription();        //This function should be called whenever the state of the challenge changes (progress is made or lost)
            }
        }
        catch (Exception e)
        {
            ExpeditionsExpanded.ExpeditionsExpandedMod.ExpLogger.LogError(e);   //ExpeditionsExpanded provides a Logger if you don't want to
        }                                                                       //have an actual plugin in your mod.
        finally
        {
            orig(self, edible);
        }
    }


    private void AllPlayersDied()
    {
        if (!completed)
            deathlessGoalProgression = 0;   //Reset progress on death.
        UpdateDescription();
    }
    
    
    private void Hibernated()
    {
        if(!completed)
            cycleGoalProgression = 0;    //Reset progress at cycle end.
        UpdateDescription();
    }

    //Update function will be called every frame. Not recommended; it's more likely to cause crashes or lag.
    public override void Update()
    {
        base.Update();
        if (!completed)
        {
            //Can read stuff from RainWorldGame game.
            Room currentRoom = game.cameras[0].room;
            Player player1 = game.Players[0].realizedCreature as Player;
            //...
        }
    }

    //Override and return true if your challenge wants to listen to creature kills by players.
    public override bool RespondToCreatureKill()
    {
        return true;
    }

    //Called whenever a player kills a creature. Easier approach than setting up a listener for creature deaths, provided by base Challenge.
    public override void CreatureKilled(Creature crit, int playerNumber)
    {
        base.CreatureKilled(crit, playerNumber);
        if (!completed)
        {
            deathlessGoalProgression++;
            if (deathlessGoalProgression == goalToReach)
                CompleteChallenge();
            UpdateDescription();
        }
    }

    #endregion

    #region Saving/Loading challenge

    //Called whenever a new challenge of this type has to be created.
    public override Challenge Generate()
    {
        float difficulty = ExpeditionData.challengeDifficulty;  //Difficulty slider of expedition menu. Goes from 0f to 1f inclusive.
        int thisToReach = 2 + (int)(difficulty * 10);
        return new SampleChallenge
        {
            goalToReach = thisToReach
        };
    }

    //Used to save the challenge in savefile, in string format.
    //IMPORTANT: Be EXTREMELY careful with the formatting. A faulty format can corrupt a save file!
    //Note: Variables that you intend to reset on hibernation do not need to be added here.
    public override string ToString()
    {
        return string.Concat(new string[]
           {
                "SampleChallenge",      //Always start with the name of the class followed by '~'.
                "~",
                ValueConverter.ConvertToString<int>(this.goalToReach),
                "><",                                                               //Use '><' as separator to differentiate variables.
                ValueConverter.ConvertToString<int>(this.deathlessGoalProgression), //Variables that are not 100% reset every cycle need to be included here.
                "><",
                this.completed ? "1" : "0",                                         //Always include these three. You can use a different format if you want,
                "><",                                                               //but keep it in mind in FromString.
                this.hidden ? "1" : "0",
                "><",
                this.revealed ? "1" : "0"
           });
    }

    //This will be called to load an existing challenge from a save file.
    //The received string will be exactly the same defined in ToString, except removing the challenge name and '~' at the beginning.
    public override void FromString(string args)
    {
        //We assume '><' is the separator
        string[] array = Regex.Split(args, "><");
        //Be mindful of the order of variables
        this.goalToReach = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
        this.deathlessGoalProgression = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
        this.completed = (array[2] == "1");
        this.hidden = (array[3] == "1");
        this.revealed = (array[4] == "1");

        //The changes we register on a player's death might not be saved directly into the string; we must manually check and set them here.
        if (ExpeditionsExpandedMod.DiedLastSession())    //DiedLastSession() will return true if the last cycle of the expedition was a death.
            deathlessGoalProgression = 0;

        this.UpdateDescription();
    }

    #endregion

    #region Misc

    //Name that will be displayed in challenge list.
    public override string ChallengeName()
    {
        return ChallengeTools.IGT.Translate("Example Challenge");       //Recommended to use the translator to support multi-language.
    }

    //Put the description of the challenge in its current state.
    public override void UpdateDescription() 
    {
        CreatureTemplate.Type someCreatureType = CreatureTemplate.Type.GreenLizard;
        string critName = ChallengeTools.IGT.Translate(ChallengeTools.creatureNames[someCreatureType.Index]);   //Gets display name of given creature type (in plural)
        //Once again, recommended to use the translator for multi-language support.
        this.description = ChallengeTools.IGT.Translate("Do something to <target_amount> <target_creature> without dying [<current_amount>/<target_amount>]")
            .Replace("<target_amount>", ValueConverter.ConvertToString<int>(this.deathlessGoalProgression))
            .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.goalToReach))
            .Replace("<target_creature>", critName);
        base.UpdateDescription();
    }

    //Function that checks wether otherChallenge and this challenge can both occur in the same expedition run.
    public override bool Duplicable(Challenge otherChallenge)
    {
        return !(otherChallenge is SampleChallenge);    //In this case, we're not allowing two SampleChallenges to happen at the same time.
    }

    //How many points the challenge will yield on completion.
    public override int Points()
    {
        int points = goalToReach * 2;
        //All vanilla challenges give double points if they are hidden. You wouldn't break the tradition, would you?
        if (this.hidden)
            points *= 2;
        return points;
    }

    //Function called when expedition is lost and player is sent to game over screen.
    public override void Reset()
    {
        deathlessGoalProgression = 0;
        cycleGoalProgression = 0;
        base.Reset();
    }

    //Function to check wether this challenge is valid for a given slugcat.
    public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
    {
        if (slugcat == SlugcatStats.Name.Yellow)
            return false;
        else if (slugcat == MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Saint)
            return false;
        else return true;
    }

    /*
     *  And that's more or less it! There are a couple more functions you can override from base Challenge,
     *  but those are optional (techically, all functions except ToString/FromString are optional).
     *  You can check out Expeditions Expanded's challenges or the base game challenges to get 
     *  more insight about coding new challenges!
     *  (Here's a tip: don't start from scratch. Copy-paste an existing challenge and modify from there.
     *  Trust me it'll save you a lot of effort)
     */

    #endregion
}
