using Expedition;
using ExpeditionsExpanded;
using Menu.Remix;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ExpeditionsExpanded;
public class SampleChallenge : Challenge, IRegionSpecificChallenge, IChallengeHooks
{
    #region Region-Specific

    //If your challenge uses specific regions, implement an interface like this one. It is important for compatibility with ExpeditionRegionSupport.
    //This is only necessary if you want to generate a challenge that needs region data, like Vista or Pearl challenges.
    interface IRegionSpecificChallenge
    {
        List<string> ApplicableRegions { get; set; }
    }
    List<string> baseApplicableRegions = SlugcatStats.SlugcatStoryRegions(ExpeditionData.slugcatPlayer);    //This is your base list of regions. It can have a hardcoded default value, but don't read/modify it directly in your code.
    public List<string> ApplicableRegions                                                                   //This is what you actually want to use in your code.
    { 
        get => return baseApplicableRegions;
        set 
        {
            baseApplicableRegions.Clear();
            baseApplicableRegions.AddRange(value);
        } 
    }                                                               
    #endregion

    #region Declaration
    //Variables
    int cycleGoalProgression;
    int deathlessGoalProgression;
    int goalToReach;
    string targetRegion;
    List<object> someList;

    public SampleChallenge()
    {
        //Recommended to initialize your variables here, most importantly those that are not saved in savefiles.
        /*
        * The state of a challenge during a playthrough and the state of a challenge in a savefile are independent from each other.
        * The state is only ever saved to a file after each successful cycle.
        * The state is only ever loaded from a file when entering the expedition, or when clicking "Continue" in the death screen.
        * This makes it tricky to alter progress after death, because any changes you make on death will immediately be overriden by the previous savestate.
        * Likewise, your variables are not going to be reset and re-read from savefile upon a successful cycle, but they WILL if the player exits back to the menu and re-enters.
        * So, to make it a lot easier to track progress, I have created two events that are called on player death and hibernation, and they ensure that variables will stay consistent    
        * between in-game states and savefile states.
        * Still, some manual tracking will be required (see FromString method)
        */
        cycleGoalProgression = 0;
        deathlessGoalProgression = 0;
        someList = new List<object>();
    }

    ~SampleChallenge()
    {
        //If you have any lists, arrays, etc; clear them here.
        someList.Clear();
    }

    //IMPORTANT: If you need hooks, implement the IChallengeHooks class. ExpeditionsExpanded will make sure to hook them when necessary.
    //Don't apply the hooks anywhere else unless you know what you're doing.
    public void ApplyHooks()
    {
        On.Player.ObjectEaten += Player_ObjectEaten;
        ExpeditionsExpanded.ExpeditionsExpandedMod.OnAllPlayersDied += AllPlayersDied;  //Called when all players die in expedition.
                                                                                        //Recommended to use for progress reset on death.
        ExpeditionsExpanded.ExpeditionsExpandedMod.OnHibernated += Hibernated;  //Called when player hibernates successfully.
                                                                                //Recommended to use for progress or variable reset on cycle end.
    }
    //Remove all your hook listeners here
    public void RemoveHooks()
    {
        On.Player.ObjectEaten -= Player_ObjectEaten;
        ExpeditionsExpanded.ExpeditionsExpandedMod.OnHibernated -= Hibernated;
        ExpeditionsExpanded.ExpeditionsExpandedMod.OnAllPlayersDied -= AllPlayersDied;
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
        
        List<string> availableRegions = ApplicableRegions;      //This list will undergo the filters that ExpeditionRegionSupport wants to use, if the mod is enabled.
        availableRegions.Remove("SU");                          //Afterwards, you can do some modifications hard-coded. It is recommended to only *remove* regions from the base list,
                                                                //but never add them.


        //ECEUtilities class has some functions for manual user input.
        //FilterAndSelectRegion will choose a region from the given list, after applying all user filters if possible.
        //You must provide the name of the challenge class without "Challenge". You may also provide an alternative name,
        //in case you also want to include the actual challenge name as an option.
        string regionAcronym = ECEUtilities.FilterAndSelectRegion("Sample", availableRegions, "Example");
        //If the function returns empty, that means that there are no available regions, and Generate should return a null value.
        if (regionAcronym == "")
            return null;

        return new SampleChallenge
        {
            goalToReach = thisToReach
            targetRegion = regionAcronym
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
                targetRegion,
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
        this.targetRegion = array[2];
        this.completed = (array[3] == "1");
        this.hidden = (array[4] == "1");
        this.revealed = (array[5] == "1");

        //Remember: challenge states are ONLY saved when the player hibernates, not when they die. We must check if they died last time and update our state accordingly.
        //TO-DO: Method to check how many deaths in a row there were without a successful hibernation.
        if (ECEUtilities.DiedLastSession())    //DiedLastSession() will return true if the last cycle of the expedition was a death.
            deathlessGoalProgression = 0;

        //ECE.IsRegionForbidden will check wether the current region is no longer applicable. This allows users to change their filters mid-expedition.
        //In such a case, it would be best to simply select another region at random.
        if (ECEUtilities.IsRegionForbidden("Sample", targetRegion, "Example"))
            targetRegion = "SU";

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
        //It is recommended to check here if there are any available regions with the current settings, to avoid potential menu softlocks.
        List<string> regions = ApplicableRegions;
        if (ECEUtilities.FilterAndSelectRegion("Sample", regions, "Example") == "")
            return false;
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
