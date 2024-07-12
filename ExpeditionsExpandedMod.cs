using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using BepInEx;
using Menu.Remix;
using System.Text.RegularExpressions;
using System.Globalization;

using Challenge = Expedition.Challenge;
using ExpeditionData = Expedition.ExpeditionData;
using ChallengeTools = Expedition.ChallengeTools;

using ExpLog = Expedition.ExpLog;
using ExpeditionGame = Expedition.ExpeditionGame;

using System.IO;
using IntVector2 = RWCustom.IntVector2;
using Custom = RWCustom.Custom;
using UnityEngine.Events;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System.Reflection;


#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]

namespace ExpeditionsExpanded
{
    [BepInPlugin("ShinyKelp.ExpeditionsExpanded", "ExpeditionsExpanded", "0.1.1")]

    public class ExpeditionsExpandedMod : BaseUnityPlugin
    {
        public static BepInEx.Logging.ManualLogSource ExpLogger { get; private set; }

        public static UnityAction OnAllPlayersDied;

        public static UnityAction OnHibernated;

        internal static bool HasShinyShieldMask = false;
        internal static bool HasCustomPlayer1 = false;
        internal static readonly HashSet<CreatureTemplate.Type> Critters = new HashSet<CreatureTemplate.Type>();


        private void OnEnable()
        {
            On.RainWorld.OnModsInit += RainWorldOnOnModsInit;
        }

        private bool IsInit;
        private void RainWorldOnOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            try
            {
                if (IsInit) return;

                On.Expedition.ChallengeOrganizer.RandomChallenge += ChallengeOrganizer_RandomChallenge;
                On.Menu.ExpeditionMenu.Singal += ExpeditionMenu_Singal;
                On.Menu.FilterDialog.ctor += FilterDialog_ctor;
                On.Menu.FilterDialog.Singal += FilterDialog_Singal;
                On.WinState.CycleCompleted += WinState_CycleCompleted;
                On.RainWorldGame.GoToDeathScreen += RainWorldGame_GoToDeathScreen;
                On.RainWorldGame.GoToStarveScreen += RainWorldGame_GoToStarveScreen;
                On.Menu.CharacterSelectPage.AbandonButton_OnPressDone += CharacterSelectPage_AbandonButton_OnPressDone;
                On.RainWorldGame.ShutDownProcess += RainWorldGame_ShutDownProcess;
                IL.Menu.FilterDialog.ctor += FilterDialog_ctorIL;
                //On.Expedition.ExpeditionCoreFile.FromString += ExpeditionCoreFile_FromString; //This is used for debugging ONLY

                foreach(ModManager.Mod mod in ModManager.ActiveMods)
                {
                    if(mod.id == "ShinyKelp.ShinyShieldMask")
                        HasShinyShieldMask = true;
                    else if(mod.id == "ShinyKelp.ExpPlayer1Change")
                        HasCustomPlayer1 = true;
                }
                Critters.Clear();
                Critters.Add(CreatureTemplate.Type.CicadaA);
                Critters.Add(CreatureTemplate.Type.CicadaB);
                Critters.Add(CreatureTemplate.Type.JetFish);
                Critters.Add(CreatureTemplate.Type.EggBug);
                Critters.Add(CreatureTemplate.Type.LanternMouse);
                Critters.Add(CreatureTemplate.Type.Snail);
                Critters.Add(MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.Yeek);
                if (!Directory.Exists(Custom.RootFolderDirectory() + "/ExpeditionsExpanded"))
                    Directory.CreateDirectory(Custom.RootFolderDirectory() + "/ExpeditionsExpanded");
                if (!Directory.Exists(Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal"))
                    Directory.CreateDirectory(Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal");

                ReadUserRegionFilters();

                ExpLogger = Logger;

                IsInit = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                throw;
            }
        }

        internal static Dictionary<string, HashSet<string>> UserDefinedRegionFilters;


        private void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
        {
            foreach (HashSet<string> hash in UserDefinedRegionFilters.Values)
                hash.Clear();
            UserDefinedRegionFilters.Clear();
            orig(self);
        }

        #region Challenge Filters

        private Vector2[] challengeLabelPositions, challengeCheckboxPositions;
        private Menu.BigArrowButton buttonLeft, buttonRight;
        private int currentPageIndex;
        private int totalPages;

        private bool ignoreDialogHooks = false;
        private void ExpeditionMenu_Singal(On.Menu.ExpeditionMenu.orig_Singal orig, Menu.ExpeditionMenu self, Menu.MenuObject sender, string message)
        {
            ignoreDialogHooks = message == "SETTINGS"; //Checks if dialog is from ExpeditionRegionSupport, instead of base game's filter dialog.
            orig(self, sender, message);
        }

        private void FilterDialog_Singal(On.Menu.FilterDialog.orig_Singal orig, Menu.FilterDialog self, Menu.MenuObject sender, string message)
        {
            if (!ignoreDialogHooks)
            {
                if (message.StartsWith("EXP_CH_FILTER_"))
                {

                    int previousStartingIndex = currentPageIndex * 10;
                    for (int i = previousStartingIndex; i < previousStartingIndex + 10; ++i)
                    {
                        if (i >= self.checkBoxes.Count)
                            break;
                        self.checkBoxes[i].pos.x = -200;
                        self.checkBoxes[i].lastPos.x = -200;
                        self.checkBoxes[i].inactive = true;
                        self.challengeTypes[i].pos.x = -200;
                        self.challengeTypes[i].lastPos.x = -200;
                    }
                    if (message == "EXP_CH_FILTER_LEFT")
                    {
                        currentPageIndex--;
                        if (currentPageIndex < 0)
                            currentPageIndex = totalPages - 1;
                    }
                    else if (message == "EXP_CH_FILTER_RIGHT")
                    {
                        currentPageIndex++;
                        if (currentPageIndex == totalPages)
                            currentPageIndex = 0;
                    }
                    int nextStartingIndex = currentPageIndex * 10;
                    for (int i = 0; i < 10; ++i)
                    {
                        if (i + nextStartingIndex >= self.checkBoxes.Count)
                            break;
                        self.checkBoxes[i + nextStartingIndex].pos = challengeCheckboxPositions[i];
                        self.checkBoxes[i + nextStartingIndex].lastPos = challengeCheckboxPositions[i];
                        self.checkBoxes[i + nextStartingIndex].inactive = false;
                        self.challengeTypes[i + nextStartingIndex].pos = challengeLabelPositions[i];
                        self.challengeTypes[i + nextStartingIndex].lastPos = challengeLabelPositions[i];
                    }

                }
                else if (message == "CLOSE")
                {
                    SaveFilterSelection(self);
                }
            }
            orig(self, sender, message);
        }

        //Only keep up to 10 dividers
        private void FilterDialog_ctorIL(MonoMod.Cil.ILContext il)
        {
            ILCursor c = new ILCursor(il);

            //if (j == list.Count - 1)
            c.GotoNext(MoveType.After,
                  x => x.MatchLdloc(6),
                x => x.MatchLdloc(3),
                x => x.Match(OpCodes.Callvirt),
                x => x.MatchLdcI4(1),
                x => x.MatchSub());

            //if (j > list.Count - 1)
            c.GotoNext(MoveType.After,
                x => x.MatchLdloc(6),
                x => x.MatchLdloc(3),
                x => x.Match(OpCodes.Callvirt),
                x => x.MatchLdcI4(1),
                x => x.MatchSub()
                );
            c.EmitDelegate<Func<int, int>>((count) =>
            {
                return Math.Min(count, 10);
            });
        }

        private void FilterDialog_ctor(On.Menu.FilterDialog.orig_ctor orig, Menu.FilterDialog self, ProcessManager manager, Menu.ChallengeSelectPage owner)
        {
            orig(self, manager, owner);
            if (ignoreDialogHooks)
                return;
            LoadFilterSelection(self);

            buttonLeft = new Menu.BigArrowButton(self, self.pages[0], "EXP_CH_FILTER_LEFT", new Vector2(453f, 360f), -1);
            buttonRight = new Menu.BigArrowButton(self, self.pages[0], "EXP_CH_FILTER_RIGHT", new Vector2(853f, 360f), 1);
            self.pages[0].subObjects.Add(buttonLeft);
            self.pages[0].subObjects.Add(buttonRight);

            challengeCheckboxPositions = new Vector2[10];
            challengeLabelPositions = new Vector2[10];
            for (int i = 0; i < 10; ++i)
            {
                challengeCheckboxPositions[i] = self.checkBoxes[i].pos;
                challengeLabelPositions[i] = self.challengeTypes[i].pos;
            }
            for (int i = 10; i < self.checkBoxes.Count; ++i)
            {
                self.checkBoxes[i].pos.x = -150;
                self.checkBoxes[i].inactive = true;
                self.challengeTypes[i].pos.x = -150;
            }
            /*
            while (self.dividers.Count > 10)
            {
                self.container.RemoveChild(self.dividers[10]);
                self.dividers.RemoveAt(10);
            }*/
            totalPages = Mathf.CeilToInt(((float)self.challengeTypes.Count) / 10f);
            currentPageIndex = 0;
        }

        private void SaveFilterSelection(Menu.FilterDialog menuDialog)
        {
            string filePath = Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal/DisabledChallenges.txt";

            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                string finalMsg = "";
                for (int i = 0; i < menuDialog.checkBoxes.Count; ++i)
                {
                    if (!menuDialog.checkBoxes[i].Checked)
                        finalMsg += menuDialog.challengeTypes[i].text + "><";
                }
                if (finalMsg.Length > 1)
                    finalMsg = finalMsg.Remove(finalMsg.Length - 2);
                writer.Write(finalMsg);
            }
        }

        private void LoadFilterSelection(Menu.FilterDialog menuDialog)
        {
            if (File.Exists(Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal/DisabledChallenges.txt"))
            {
                StreamReader sr = new StreamReader(Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal/DisabledChallenges.txt");
                HashSet<string> disabledChallenges = Regex.Split(sr.ReadToEnd(), "><").ToHashSet<string>();
                sr.Close();
                for (int i = 0; i < menuDialog.checkBoxes.Count; ++i)
                {
                    if (disabledChallenges.Contains(menuDialog.challengeTypes[i].text))
                        menuDialog.checkBoxes[i].Checked = false;
                }
            }
        }

        private Challenge ChallengeOrganizer_RandomChallenge(On.Expedition.ChallengeOrganizer.orig_RandomChallenge orig, bool hidden)
        {
            CheckFilterInit();
            return orig(false);
        }

        private void CheckFilterInit()
        {
            if (Expedition.ChallengeOrganizer.availableChallengeTypes is null)
                Expedition.ChallengeOrganizer.SetupChallengeTypes();
            if (Expedition.ChallengeOrganizer.filterChallengeTypes is null)
                Expedition.ChallengeOrganizer.filterChallengeTypes = new List<string>();
            if (Expedition.ChallengeOrganizer.filterChallengeTypes.Count == 0)
            {
                if (File.Exists(Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal/DisabledChallenges.txt"))
                {
                    StreamReader sr = new StreamReader(Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal/DisabledChallenges.txt");
                    HashSet<string> disabledChallenges = Regex.Split(sr.ReadToEnd(), "><").ToHashSet<string>();
                    sr.Close();

                    List<Challenge> challengeTypes = Expedition.ChallengeOrganizer.availableChallengeTypes;
                    foreach(Challenge challenge in challengeTypes)
                    {
                        if (disabledChallenges.Contains(challenge.ChallengeName()))
                            Expedition.ChallengeOrganizer.filterChallengeTypes.Add(challenge.GetType().Name);
                    }
                }
            }
        }

        #endregion

        #region Debugging
        private void ExpeditionCoreFile_FromString(On.Expedition.ExpeditionCoreFile.orig_FromString orig, Expedition.ExpeditionCoreFile self, string saveString)
        {
            UnityEngine.Debug.Log("CHECKPOINT: START");
            ExpeditionData.unlockables = new List<string>();
            ExpeditionData.completedQuests = new List<string>();
            ExpeditionData.allChallengeLists = new Dictionary<SlugcatStats.Name, List<Challenge>>();
            ExpeditionGame.allUnlocks = new Dictionary<SlugcatStats.Name, List<string>>();
            ExpeditionData.challengeTypes = new Dictionary<string, int>();
            ExpeditionData.level = 1;
            ExpeditionData.currentPoints = 0;
            ExpeditionData.totalPoints = 0;
            ExpeditionData.perkLimit = 1;
            ExpeditionData.totalChallengesCompleted = 0;
            ExpeditionData.totalHiddenChallengesCompleted = 0;
            ExpeditionData.totalWins = 0;
            ExpeditionData.slugcatPlayer = SlugcatStats.Name.White;
            ExpeditionData.completedMissions = new List<string>();
            ExpeditionData.slugcatWins = new Dictionary<string, int>();
            ExpeditionData.newSongs = new List<string>();
            ExpeditionData.allActiveMissions = new Dictionary<string, string>();
            ExpeditionData.missionBestTimes = new Dictionary<string, int>();
            ExpeditionData.ints = new int[8];
            ExpeditionData.requiredExpeditionContent = new Dictionary<string, List<string>>();
            ExpeditionData.ClearActiveChallengeList();
            string[] array = Regex.Split(saveString, "<expC>");
            bool flag = false;
            bool flag2 = false;
            bool flag3 = false;
            bool flag4 = false;
            bool flag5 = false;
            bool flag6 = false;
            UnityEngine.Debug.Log("CHECKPOINT: BEGIN LOOP");

            for (int i = 0; i < array.Length; i++)
            {
                UnityEngine.Debug.Log("CHECKPOINT #" + i + " " + array[i]);

                if (array[i].StartsWith("SLOT:"))
                {
                    int num = int.Parse(Regex.Split(array[i], ":")[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                    if (num < 0)
                    {
                        num = 0;
                    }
                    ExpeditionData.saveSlot = num;
                }
                if (array[i].StartsWith("LEVEL:"))
                {
                    ExpeditionData.level = int.Parse(Regex.Split(array[i], ":")[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                }
                if (array[i].StartsWith("PERKLIMIT:"))
                {
                    ExpeditionData.perkLimit = int.Parse(Regex.Split(array[i], ":")[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                }
                if (array[i].StartsWith("POINTS:"))
                {
                    ExpeditionData.currentPoints = int.Parse(Regex.Split(array[i], ":")[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                }
                if (array[i].StartsWith("TOTALPOINTS:"))
                {
                    ExpeditionData.totalPoints = int.Parse(Regex.Split(array[i], ":")[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                }
                if (array[i].StartsWith("TOTALCHALLENGES:"))
                {
                    ExpeditionData.totalChallengesCompleted = int.Parse(Regex.Split(array[i], ":")[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                }
                if (array[i].StartsWith("TOTALHIDDENCHALLENGES:"))
                {
                    ExpeditionData.totalHiddenChallengesCompleted = int.Parse(Regex.Split(array[i], ":")[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                }
                if (array[i].StartsWith("WINS:"))
                {
                    ExpeditionData.totalWins = int.Parse(Regex.Split(array[i], ":")[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                }
                if (array[i].StartsWith("SLUG:"))
                {
                    string text = Regex.Split(array[i], ":")[1];
                    if ((text == "Rivulet" || text == "Spear" || text == "Gourmand" || text == "Artificer" || text == "Saint") && !ModManager.MSC)
                    {
                        text = "White";
                    }
                    ExpeditionData.slugcatPlayer = new SlugcatStats.Name(text, false);
                }
                if (array[i].StartsWith("MANUAL:"))
                {
                    ExpeditionData.hasViewedManual = (Regex.Split(array[i], ":")[1] == "1");
                }
                if (array[i].StartsWith("MENUSONG:"))
                {
                    ExpeditionData.menuSong = Regex.Split(array[i], ":")[1];
                }
                if (array[i].StartsWith("CHALLENGETYPES:") && Regex.Split(array[i], ":")[1] != "")
                {
                    string[] array2 = Regex.Split(Regex.Split(array[i], ":")[1], "<>");
                    for (int j = 0; j < array2.Length; j++)
                    {
                        string[] array3 = Regex.Split(array2[j], "#");
                        if (ExpeditionData.challengeTypes.ContainsKey(array3[0]))
                        {
                            ExpeditionData.challengeTypes[array3[0]] = int.Parse(array3[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            ExpeditionData.challengeTypes.Add(array3[0], int.Parse(array3[1], NumberStyles.Any, CultureInfo.InvariantCulture));
                        }
                    }
                }
                if (array[i].StartsWith("SLUGWINS:") && Regex.Split(array[i], ":")[1] != "")
                {
                    string[] array4 = Regex.Split(Regex.Split(array[i], ":")[1], "<>");
                    for (int k = 0; k < array4.Length; k++)
                    {
                        string[] array5 = Regex.Split(array4[k], "#");
                        if (ExpeditionData.slugcatWins.ContainsKey(array5[0]))
                        {
                            ExpeditionData.slugcatWins[array5[0]] = int.Parse(array5[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            ExpeditionData.slugcatWins.Add(array5[0], int.Parse(array5[1], NumberStyles.Any, CultureInfo.InvariantCulture));
                        }
                    }
                }
                if (array[i].StartsWith("UNLOCKS:") && Regex.Split(array[i], ":")[1] != "")
                {
                    string[] array6 = Regex.Split(Regex.Split(array[i], ":")[1], "<>");
                    int num2 = 1;
                    for (int l = 0; l < array6.Length; l++)
                    {
                        if (array6[l].StartsWith("per-"))
                        {
                            int num3 = int.Parse(Regex.Split(array6[l], "-")[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                            num2 += num3;
                        }
                        ExpeditionData.unlockables.Add(array6[l]);
                    }
                    if (num2 > ExpeditionData.perkLimit)
                    {
                        ExpeditionData.perkLimit = num2;
                    }
                }
                if (array[i].StartsWith("NEWSONGS:") && Regex.Split(array[i], ":")[1] != "")
                {
                    string[] array7 = Regex.Split(Regex.Split(array[i], ":")[1], "<>");
                    for (int m = 0; m < array7.Length; m++)
                    {
                        if (!ExpeditionData.newSongs.Contains(array7[m]))
                        {
                            ExpeditionData.newSongs.Add(array7[m]);
                        }
                    }
                }
                if (array[i].StartsWith("QUESTS:") && Regex.Split(array[i], ":")[1] != "")
                {
                    string[] array8 = Regex.Split(Regex.Split(array[i], ":")[1], "<>");
                    for (int n = 0; n < array8.Length; n++)
                    {
                        if (array8[n] != "")
                        {
                            ExpeditionData.completedQuests.Add(array8[n]);
                        }
                    }
                }
                if (array[i].StartsWith("MISSIONS:") && Regex.Split(array[i], ":")[1] != "")
                {
                    string[] array9 = Regex.Split(Regex.Split(array[i], ":")[1], "<>");
                    for (int num4 = 0; num4 < array9.Length; num4++)
                    {
                        if (array9[num4] != "")
                        {
                            ExpeditionData.completedMissions.Add(array9[num4]);
                        }
                    }
                }
                if (array[i].StartsWith("INTS:") && Regex.Split(array[i], ":")[1] != "")
                {
                    ExpeditionData.ints = Array.ConvertAll<string, int>(Regex.Split(array[i], ":")[1].Split(new char[]
                    {
                ','
                    }), new Converter<string, int>(ValueConverter.ConvertToValue<int>));
                }
                if (array[i] == "[END PASSAGES]")
                {
                    flag3 = false;
                }
                if (array[i] == "[END UNLOCKS]")
                {
                    flag2 = false;
                }
                if (array[i] == "[END CHALLENGES]")
                {
                    flag = false;
                }
                if (array[i] == "[END MISSION]")
                {
                    flag4 = false;
                }
                if (array[i] == "[END TIMES]")
                {
                    flag5 = false;
                }
                if (array[i] == "[END CONTENT]")
                {
                    flag6 = false;
                }
                if (flag)
                {

                    try
                    {
                        string[] array10 = Regex.Split(array[i], "#");
                        SlugcatStats.Name name = new SlugcatStats.Name(array10[0], false);
                        string[] array11 = Regex.Split(array10[1], "~");
                        string type = array11[0];
                        string text2 = array11[1];//This part might break with faulty challenge string formats.
                        Challenge challenge = (Challenge)Activator.CreateInstance(Expedition.ChallengeOrganizer.availableChallengeTypes.Find((Challenge c) => c.GetType().Name == type).GetType());
                        challenge.FromString(text2);
                        ExpLog.Log(challenge.description);
                        if (!ExpeditionData.allChallengeLists.ContainsKey(name))
                        {
                            ExpeditionData.allChallengeLists.Add(name, new List<Challenge>());
                        }
                        ExpeditionData.allChallengeLists[name].Add(challenge);
                        ExpLog.Log(string.Concat(new string[]
                        {
                    "[",
                    name.value,
                    "] Recreated ",
                    type,
                    " : ",
                    text2
                        }));
                    }
                    catch (Exception ex)
                    {
                        ExpLog.Log("ERROR: Problem recreating challenge type with reflection: " + ex.Message);
                    }
                }
                if (flag2)
                {
                    string[] array12 = Regex.Split(array[i], "#");
                    SlugcatStats.Name key = new SlugcatStats.Name(array12[0], false);
                    string[] array13 = Regex.Split(array12[1], "><");
                    for (int num5 = 0; num5 < array13.Length; num5++)
                    {
                        if (!ExpeditionGame.allUnlocks.ContainsKey(key))
                        {
                            ExpeditionGame.allUnlocks[key] = new List<string>();
                        }
                        ExpeditionGame.allUnlocks[key].Add(array13[num5]);
                    }
                }
                if (flag3)
                {
                    string key2 = Regex.Split(array[i], "#")[0];
                    int value = int.Parse(Regex.Split(array[i], "#")[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                    if (!ExpeditionData.allEarnedPassages.ContainsKey(key2))
                    {
                        ExpeditionData.allEarnedPassages.Add(key2, value);
                    }
                    ExpeditionData.allEarnedPassages[key2] = value;
                }
                if (flag4)
                {
                    ExpLog.Log("LOADING ACTIVE MISSIONS:");
                    string text3 = Regex.Split(array[i], "#")[0];
                    string text4 = Regex.Split(array[i], "#")[1];
                    ExpLog.Log("SLUG: " + text3 + " | " + text4);
                    if (!ExpeditionData.allActiveMissions.ContainsKey(text3))
                    {
                        ExpeditionData.allActiveMissions.Add(text3, text4);
                    }
                }
                if (flag5)
                {
                    string[] array14 = Regex.Split(array[i], "#");
                    ExpLog.Log("TIME COUNT: " + array14.Length.ToString());
                    string key3 = array14[0];
                    ExpLog.Log("TIME MIS: " + array14[0]);
                    int value2 = int.Parse(array14[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                    ExpLog.Log("TIME TIME: " + array14[1]);
                    if (!ExpeditionData.missionBestTimes.ContainsKey(key3))
                    {
                        ExpeditionData.missionBestTimes.Add(key3, value2);
                    }
                    else
                    {
                        ExpeditionData.missionBestTimes[key3] = value2;
                    }
                }
                if (flag6)
                {
                    string[] array15 = Regex.Split(array[i], "#");
                    string text5 = array15[0];
                    List<string> value3 = Regex.Split(array15[1], "<mod>").ToList<string>();
                    ExpeditionData.requiredExpeditionContent.Add(text5, value3);
                    ExpLog.Log(text5 + " content: " + array15[1]);
                }
                if (array[i] == "[CHALLENGES]")
                {
                    flag = true;
                }
                if (array[i] == "[UNLOCKS]")
                {
                    flag2 = true;
                }
                if (array[i] == "[PASSAGES]")
                {
                    flag3 = true;
                }
                if (array[i] == "[MISSION]")
                {
                    flag4 = true;
                }
                if (array[i] == "[TIMES]")
                {
                    flag5 = true;
                }
                if (array[i] == "[CONTENT]")
                {
                    flag6 = true;
                }
            }


        }

        #endregion

        #region Death Records

        private void RainWorldGame_GoToDeathScreen(On.RainWorldGame.orig_GoToDeathScreen orig, RainWorldGame self)
        {
            if (self.rainWorld.ExpeditionMode)
            {
                RecordDeath();
                try
                {
                    OnAllPlayersDied.Invoke();
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
                finally
                {
                    orig(self);
                }
            }
            else
                orig(self);
        }

        private void RainWorldGame_GoToStarveScreen(On.RainWorldGame.orig_GoToStarveScreen orig, RainWorldGame self)
        {
            if (self.rainWorld.ExpeditionMode)
            {
                RecordDeath();
                try
                {
                    OnAllPlayersDied.Invoke();
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
                finally
                {
                    orig(self);
                }
            }
            else
                orig(self);
        }
        
        private void WinState_CycleCompleted(On.WinState.orig_CycleCompleted orig, WinState self, RainWorldGame game)
        {
            try
            {
                if (game.rainWorld.ExpeditionMode)
                {
                    ResetDeathRecords();
                    OnHibernated.Invoke();
                }
            }
            catch(Exception e)
            {
                Logger.LogError(e);
            }
            finally
            {
                orig(self, game);
            }
        }

        private void CharacterSelectPage_AbandonButton_OnPressDone(On.Menu.CharacterSelectPage.orig_AbandonButton_OnPressDone orig, Menu.CharacterSelectPage self, Menu.Remix.MixedUI.UIfocusable trigger)
        {
            try
            {
                ResetDeathRecords();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
            finally
            {
                orig(self, trigger);
            }
        }

        private void RecordDeath()
        {
            string slugcatPlayer = ExpeditionData.slugcatPlayer.ToString();
            string filePath = Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal/" + slugcatPlayer + ".txt";
            if (!File.Exists(filePath))
                File.Create(filePath);
        }

        public static bool DiedLastSession()
        {
            string slugcatPlayer = ExpeditionData.slugcatPlayer.ToString();
            string filePath = Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal/" + slugcatPlayer + ".txt";
            return File.Exists(filePath);
        }

        private void ResetDeathRecords()
        {
            string slugcatPlayer = ExpeditionData.slugcatPlayer.ToString();
            string filePath = Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal/" + slugcatPlayer + ".txt";
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        #endregion

        #region User-defined region filters
        private void ReadUserRegionFilters()
        {
            UserDefinedRegionFilters = new Dictionary<string, HashSet<string>>();
            foreach (string filePath in Directory.GetFiles(Custom.RootFolderDirectory() + "/ExpeditionsExpanded"))
            {
                if (!filePath.EndsWith(".txt"))
                    continue;
                string[] splitName = filePath.Split('/');
                string[] splitName2 = splitName[splitName.Length - 1].Split('\\');
                string className = splitName2[splitName2.Length - 1];
                className = className.Substring(0, className.Length - 4);
                UserDefinedRegionFilters.Add(className, new HashSet<string>());

                StreamReader r = new StreamReader(filePath);
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    UserDefinedRegionFilters[className].Add(line);
                }
                r.Close();
            }
        }

        public static bool SelectRegionAfterUserFilters(string challengeName, out string regionAcronym, List<string> applicableRegions)
        {
            if (UserDefinedRegionFilters.ContainsKey("~" + challengeName))
                foreach (string s in UserDefinedRegionFilters["~" + challengeName])
                    applicableRegions.Remove(s);

            if (UserDefinedRegionFilters.ContainsKey(challengeName))
                foreach (string s in UserDefinedRegionFilters[challengeName])
                    if (!applicableRegions.Contains(s))
                        applicableRegions.Add(s);

            if (applicableRegions.Count == 0)
            {
                regionAcronym = "";
                return false;
            }

            int selected = UnityEngine.Random.Range(0, applicableRegions.Count);
            regionAcronym = applicableRegions[selected];
            return true;
        }

        public static bool ApplyRegionUserFilters(string challengeName, ref List<string> applicableRegions)
        {
            if (applicableRegions is null)
                applicableRegions = SlugcatStats.SlugcatStoryRegions(ExpeditionData.slugcatPlayer);
            if (UserDefinedRegionFilters.ContainsKey("~" + challengeName))
                foreach (string s in UserDefinedRegionFilters["~" + challengeName])
                    applicableRegions.Remove(s);

            if (UserDefinedRegionFilters.ContainsKey(challengeName))
                foreach (string s in UserDefinedRegionFilters[challengeName])
                    if (!applicableRegions.Contains(s))
                        applicableRegions.Add(s);

            if (applicableRegions.Count == 0)
            {
                return false;
            }
            return true;
        }

        #endregion
    }

    public interface IRegionSpecificChallenge
    {
        public List<string> ApplicableRegions { get; set; }
    }



}
