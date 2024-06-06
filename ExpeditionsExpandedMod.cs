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

        internal static Dictionary<string, string> LapChallengeRegions;
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

                if (!Directory.Exists(Custom.RootFolderDirectory() + "/ExpeditionsExpanded"))
                    Directory.CreateDirectory(Custom.RootFolderDirectory() + "/ExpeditionsExpanded");
                if (!Directory.Exists(Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal"))
                    Directory.CreateDirectory(Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal");

                LapChallengeRegions = new Dictionary<string, string>();
                string filePath = Custom.RootFolderDirectory() + "/ExpeditionsExpanded/LapChallenge.txt";
                if (File.Exists(filePath))
                {
                    StreamReader r = new StreamReader(filePath);
                    string line;
                    while((line = r.ReadLine()) != null)
                    {
                        string[] array = Regex.Split(line,",,");
                        if(array.Length == 2)
                        {
                            LapChallengeRegions.Add(array[0], array[1]);
                            UnityEngine.Debug.Log("Adding: " + array[0] + " " + array[1]);
                        }
                    }
                    r.Close();
                }

                ExpLogger = Logger;

                IsInit = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                throw;
            }
        }

        private void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
        {
            LapChallengeRegions.Clear();
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
    }

    public class GourmetChallenge : Challenge
    {
        int targetAmount;
        HashSet<AbstractPhysicalObject.AbstractObjectType> eatenEdibleTypes;

        public GourmetChallenge()
        {
            eatenEdibleTypes = new HashSet<AbstractPhysicalObject.AbstractObjectType>();
            On.Player.ObjectEaten += Player_ObjectEaten;
        }

        ~GourmetChallenge()
        {
            On.Player.ObjectEaten -= Player_ObjectEaten;
        }

        private void Player_ObjectEaten(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            try
            {
                if (!completed)
                {
                    if( edible is PhysicalObject physObj)
                    {
                        AbstractPhysicalObject.AbstractObjectType eatenType = physObj.abstractPhysicalObject.type;
                        if (!eatenEdibleTypes.Contains(eatenType))
                        {
                            eatenEdibleTypes.Add(eatenType);
                            if (eatenEdibleTypes.Count == targetAmount)
                                CompleteChallenge();
                            UpdateDescription();
                        }
                    }

                }
            }
            catch(Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
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
                eatenEdibleTypes = new HashSet<AbstractPhysicalObject.AbstractObjectType>()
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Gourmet Diet");
        }

        public override string ToString()
        {
            string[] eatenTypesArray = new string[eatenEdibleTypes.Count];
            int i = 0;
            foreach (AbstractPhysicalObject.AbstractObjectType type in eatenEdibleTypes.ToArray())
            {
                eatenTypesArray[i] = "><" + type.value;
                i++;
            }
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
                    this.revealed ? "1" : "0"
                }
                );

            foreach (AbstractPhysicalObject.AbstractObjectType type in eatenEdibleTypes.ToArray())
            {
                saveString = saveString + "><" + type.value;
            }
            return saveString;
            
        }

        public override void FromString(string args)
        {
            string[] array = Regex.Split(args, "><");
            if (array.Length >= 4)
            {
                this.targetAmount = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.completed = (array[1] == "1");
                this.hidden = (array[2] == "1");
                this.revealed = (array[3] == "1");

                eatenEdibleTypes = new HashSet<AbstractPhysicalObject.AbstractObjectType>();

                if(array.Length > 4)
                    for (int i = 4; i < array.Length; i++)
                        eatenEdibleTypes.Add(new AbstractPhysicalObject.AbstractObjectType(array[i]));
            }
            else
            {
                this.targetAmount = 5;
                this.completed = this.hidden = this.revealed = false;
                eatenEdibleTypes = new HashSet<AbstractPhysicalObject.AbstractObjectType>();
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
            return (int)((targetAmount*11f - 25) * (hidden? 2 : 1));
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

    }

    public class FlashChallenge : Challenge
    {
        int targetAmount;
        int blindedKills;
        public CreatureTemplate.Type targetCreature;
        private enum FlashChallengeTargets
        {
            Lizard,
            Scavenger,
            Dropwig,
            Vulture
        }
        public FlashChallenge()
        {
            blindedKills = 0;
        }

        ~FlashChallenge()
        {
        }

        public override bool RespondToCreatureKill()
        {
            return true;
        }

        public override void CreatureKilled(Creature crit, int playerNumber)
        {
            if (crit.abstractCreature.creatureTemplate.TopAncestor().type == targetCreature && crit.Blinded)
            {
                blindedKills++;
                if (blindedKills == targetAmount)
                    CompleteChallenge();
                UpdateDescription();
            }
            base.CreatureKilled(crit, playerNumber);
        }
        public override Challenge Generate()
        {
            CreatureTemplate.Type chosenType;
            float multiplier = 1f;
            System.Random r = new System.Random();
            int select = r.Next(0,4);
            switch ((FlashChallengeTargets)select)
            {
                case FlashChallengeTargets.Dropwig:
                    chosenType = CreatureTemplate.Type.DropBug;
                    multiplier = 1.3f;
                    break;
                case FlashChallengeTargets.Lizard:
                    chosenType = CreatureTemplate.Type.LizardTemplate;
                    break;
                case FlashChallengeTargets.Scavenger:
                    chosenType = CreatureTemplate.Type.Scavenger;
                    multiplier = 1.5f;
                    break;
                case FlashChallengeTargets.Vulture:
                    chosenType = CreatureTemplate.Type.Vulture;
                    multiplier = 0.6f;
                    break;
                default:
                    chosenType = CreatureTemplate.Type.LizardTemplate;
                    break;

            }
            return new FlashChallenge
            {
                targetCreature = chosenType,
                targetAmount = (int)(ExpeditionData.challengeDifficulty * 5f * multiplier + 1),
                blindedKills = 0
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Flashing");
        }

        public override string ToString()
        {
            return string.Concat(new string[]
           {
                "FlashChallenge",
                "~",
                targetCreature.value,
                "><",
                ValueConverter.ConvertToString<int>(this.targetAmount),
                "><",
                ValueConverter.ConvertToString<int>(this.blindedKills),
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
            string[] array = Regex.Split(args, "><");
            this.targetCreature = new CreatureTemplate.Type(array[0]);
            this.targetAmount = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
            this.blindedKills = int.Parse(array[2], NumberStyles.Any, CultureInfo.InvariantCulture);
            this.completed = (array[3] == "1");
            this.hidden = (array[4] == "1");
            this.revealed = (array[5] == "1");
            this.UpdateDescription();
        }

        public override void UpdateDescription()
        {
            string critName = "Unknown";
            if (this.targetCreature == CreatureTemplate.Type.LizardTemplate)
                critName = "Lizards";
            else if (this.targetCreature.Index >= 0)
                critName = ChallengeTools.IGT.Translate(ChallengeTools.creatureNames[this.targetCreature.Index]);
            
            this.description = ChallengeTools.IGT.Translate("Kill <target_amount> <target_creature> while they are blinded [<current_amount>/<target_amount>]")
                .Replace("<target_amount>", ValueConverter.ConvertToString<int>(this.targetAmount))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.blindedKills))
                .Replace("<target_creature>", critName);
            base.UpdateDescription();
        }

        public override int Points()
        {
            return (int)(25 + (targetAmount * 12f)) * (hidden ? 2 : 1);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is FlashChallenge flash && flash.targetCreature == this.targetCreature);
        }

        public override void Reset()
        {
            blindedKills = 0;
            base.Reset();
        }
    }

    public class SandwichChallenge : Challenge
    {
        int eatenMushrooms = 0;
        int mushroomsToEat = 0;
        public SandwichChallenge() 
        {
            On.Player.ObjectEaten += Player_ObjectEaten;
            ExpeditionsExpandedMod.OnHibernated += OnHibernated;

        }

        private void Player_ObjectEaten(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            try
            {
                if(!completed && (edible is PhysicalObject physObj && physObj.abstractPhysicalObject.type == AbstractPhysicalObject.AbstractObjectType.Mushroom))
                {
                    eatenMushrooms++;
                    if (eatenMushrooms == mushroomsToEat)
                        CompleteChallenge();
                    UpdateDescription();
                }
            }
            catch (Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, edible);
            }
        }

        ~SandwichChallenge()
        {
            On.Player.ObjectEaten -= Player_ObjectEaten;
            ExpeditionsExpandedMod.OnHibernated -= OnHibernated;
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
            string[] array = Regex.Split(args, "><");
            if (array.Length == 4)
            {
                this.mushroomsToEat = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.completed = (array[1] == "1");
                this.hidden = (array[2] == "1");
                this.revealed = (array[3] == "1");
            }
            else
            {
                this.mushroomsToEat = 5;
                this.completed = this.hidden = this.revealed = false;
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
    
    public class HeistChallenge : Challenge
    {
        bool killedAScav, payedToll;
        bool doubleHeist;
        string heistedRegion;
        HashSet<EntityID> grabbedPearls = new HashSet<EntityID>();
        HashSet<EntityID> stolenPearls = new HashSet<EntityID>();
        Dictionary<EntityID, string> stolenPearlsDict = new Dictionary<EntityID, string>();
        public HeistChallenge() 
        {
            On.Player.SlugcatGrab += Player_SlugcatGrab;
            On.Player.Regurgitate += Player_Regurgitate;
            On.Player.SpitOutOfShortCut += Player_SpitOutOfShortCut;
            On.ScavengerOutpost.FeeRecieved += ScavengerOutpost_FeeRecieved;
            ExpeditionsExpandedMod.OnHibernated += OnHibernated;
            killedAScav = payedToll = false;
            grabbedPearls = new HashSet<EntityID>();
            stolenPearlsDict = new Dictionary<EntityID, string>();
            heistedRegion = "_";
        }

        ~HeistChallenge()
        {
            On.Player.SlugcatGrab -= Player_SlugcatGrab;
            On.Player.Regurgitate -= Player_Regurgitate;
            On.Player.SpitOutOfShortCut -= Player_SpitOutOfShortCut;
            On.ScavengerOutpost.FeeRecieved -= ScavengerOutpost_FeeRecieved;
            ExpeditionsExpandedMod.OnHibernated -= OnHibernated;
            grabbedPearls.Clear();
            stolenPearls.Clear();
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
                    if(item.type == AbstractPhysicalObject.AbstractObjectType.DataPearl)
                        payedToll = true;
                }
            }
            catch (Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
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
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
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
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
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
                                if(self.abstractCreature.world.game.session.creatureCommunities.LikeOfPlayer(CreatureCommunities.CommunityID.Scavengers, -1, self.playerState.playerNumber) < 0.1f)
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
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
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
            string[] array = Regex.Split(args, "><");
            this.doubleHeist = (array[0] == "1");
            this.heistedRegion = array[1];
            this.completed = (array[2] == "1");
            this.hidden = (array[3] == "1");
            this.revealed = (array[4] == "1");
            this.UpdateDescription();
        }

        public override void UpdateDescription()
        {
            if (this.doubleHeist)
            {
                this.description = ChallengeTools.IGT.Translate("Steal from two tolls without killing, paying or chieftain [<score>/2]").Replace("<score>", (heistedRegion == "_" ? "0" : (completed? "2" : "1")));
            }
            else
                this.description = ChallengeTools.IGT.Translate("Steal from a scav toll without killing, paying or chieftain");
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

    public class LapChallenge : Challenge
    {
        public string targetRegion;
        int timesEntered = 0;

        public  LapChallenge()
        {
            On.OverWorld.GateRequestsSwitchInitiation += OverWorld_GateRequestsSwitchInitiation;
            timesEntered = 0;
        }

        ~LapChallenge()
        {
            On.OverWorld.GateRequestsSwitchInitiation -= OverWorld_GateRequestsSwitchInitiation;
        }

        private void OverWorld_GateRequestsSwitchInitiation(On.OverWorld.orig_GateRequestsSwitchInitiation orig, OverWorld self, RegionGate reportBackToGate)
        {
            try
            {
                if (!completed)
                {
                    string currentRegion = Region.GetVanillaEquivalentRegionAcronym(self.activeWorld.name);
                    string[] array = Regex.Split(reportBackToGate.room.abstractRoom.name, "_");
                    if (array.Length == 3)
                    {
                        string region;
                        if (currentRegion == array[1])
                        {
                            region = array[2];
                        }
                        else
                            region = array[1];
                        if (region == targetRegion)
                            timesEntered++;
                        if (timesEntered == 2)
                            CompleteChallenge();
                    }
                }
            }
            catch (Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, reportBackToGate);
            }
        }

        public override Challenge Generate()
        {
            List<string> list = SlugcatStats.SlugcatStoryRegions(ExpeditionData.slugcatPlayer);
            foreach(string s in ExpeditionsExpandedMod.LapChallengeRegions.Keys)
                list.Add(s);
            list.Remove("SS");
            list.Remove("OE");
            list.Remove("LC");
            list.Remove("DM");
            list.Remove("MS");
            list.Remove("RM");
            list.Remove("HR");
            if(ExpeditionData.challengeDifficulty < 0.9f)
            {
                list.Remove("SB");
                list.Remove("UW");
            }
            timesEntered = 0;

            int selected = UnityEngine.Random.Range(0, list.Count);

            return new LapChallenge
            {
                targetRegion = list[selected]
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Region Lap"); ;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "LapChallenge",
                "~",
                ValueConverter.ConvertToString<string>(this.targetRegion),
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
            string[] array = Regex.Split(args, "><");
            this.targetRegion = array[0];
            this.completed = (array[1] == "1");
            this.hidden = (array[2] == "1");
            this.revealed = (array[3] == "1");

            if (!SlugcatStats.SlugcatStoryRegions(ExpeditionData.slugcatPlayer).Contains(targetRegion) &&
                !ExpeditionsExpandedMod.LapChallengeRegions.ContainsKey(targetRegion))
                targetRegion = "SU";

            this.UpdateDescription();
        }

        public override void UpdateDescription()
        {
            string regionName = "";
            if(ExpeditionsExpandedMod.LapChallengeRegions.ContainsKey(targetRegion))
                regionName = ExpeditionsExpandedMod.LapChallengeRegions[targetRegion];
            else
                regionName = Region.GetRegionFullName(this.targetRegion, ExpeditionData.slugcatPlayer);
            this.description = ChallengeTools.IGT.Translate("Enter <region> twice in one cycle.").Replace
                ("<region>", ChallengeTools.IGT.Translate(regionName));
            base.UpdateDescription();
        }
        
        public override int Points()
        {
            int points = 160;
            if (targetRegion == "UW" || targetRegion == "SB")
                points = 220;
            return (hidden? points*2 : points);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is LapChallenge lap && lap.targetRegion == targetRegion);
        }

        public override void Reset()
        {
            timesEntered = 0;
            base.Reset();
        }
    }

    public class ApexExpertChallenge : Challenge
    {
        enum ApexExpertTargets
        {
            RedLizard,
            RedCentipede,
            DaddyLongLegs,
            MirosVulture
            
        }
        public CreatureTemplate.Type targetCreature;
        int targetAmount;
        int targetsKilled;
        public ApexExpertChallenge()
        {
            ExpeditionsExpandedMod.OnAllPlayersDied += OnAllPlayersDied;
        }

        ~ApexExpertChallenge()
        {
            ExpeditionsExpandedMod.OnAllPlayersDied -= OnAllPlayersDied;
        }

        private void OnAllPlayersDied()
        {
            if (!completed)
            {
                targetsKilled = 0;
                UpdateDescription();
            }
        }

        public override bool RespondToCreatureKill()
        {
            return true;
        }
        
        public override void CreatureKilled(Creature crit, int playerNumber)
        {
            if (crit.abstractCreature.creatureTemplate.type == targetCreature)
            {
                targetsKilled++;
                if (targetsKilled == targetAmount)
                    CompleteChallenge();
                UpdateDescription();
            }
        }

        public override Challenge Generate()
        {
            CreatureTemplate.Type chosenType;
            float multiplier = 1f;
            System.Random r = new System.Random();
            int select = 1, extra = 0;
            if (ExpeditionData.challengeDifficulty > 0.5f)
                select = r.Next(0, 4);
            else
                select = r.Next(0, 3);
            switch ((ApexExpertTargets)select)
            {
                case ApexExpertTargets.RedLizard:
                    chosenType = CreatureTemplate.Type.RedLizard;
                    multiplier = 6f;
                    break;
                case ApexExpertTargets.RedCentipede:
                    chosenType = CreatureTemplate.Type.RedCentipede;
                    multiplier = 5f;
                    break;
                case ApexExpertTargets.DaddyLongLegs:
                    chosenType = CreatureTemplate.Type.DaddyLongLegs;
                    multiplier = 4f;
                    break;
                case ApexExpertTargets.MirosVulture:
                    chosenType = MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.MirosVulture;
                    extra = -1;
                    multiplier = 4f;
                    break;
                default:
                    chosenType = CreatureTemplate.Type.RedLizard;
                    break;
            }
            return new ApexExpertChallenge
            {
                targetCreature = chosenType,
                targetAmount = Math.Max(Mathf.CeilToInt(ExpeditionData.challengeDifficulty * multiplier), 2) + extra
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Apex Expert");
        }

        public override string ToString()
        {
            return string.Concat(new string[]
           {
                "ApexExpertChallenge",
                "~",
                targetCreature.value,
                "><",
                ValueConverter.ConvertToString<int>(this.targetAmount),
                "><",
                ValueConverter.ConvertToString<int>(this.targetsKilled),
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
            string[] array = Regex.Split(args, "><");
            this.targetCreature = new CreatureTemplate.Type(array[0]);
            this.targetAmount = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
            this.targetsKilled = int.Parse(array[2], NumberStyles.Any, CultureInfo.InvariantCulture);
            this.completed = (array[3] == "1");
            this.hidden = (array[4] == "1");
            this.revealed = (array[5] == "1");
            if (ExpeditionsExpandedMod.DiedLastSession())
                targetsKilled = 0;
            this.UpdateDescription();
        }
        public override void UpdateDescription()
        {
            string critName = "Unknown";
            if (this.targetCreature.Index >= 0)
                critName = ChallengeTools.IGT.Translate(ChallengeTools.creatureNames[this.targetCreature.Index]);

            this.description = ChallengeTools.IGT.Translate("Kill <target_amount> <target_creature> without dying [<current_amount>/<target_amount>]")
                .Replace("<target_amount>", ValueConverter.ConvertToString<int>(this.targetAmount))
                .Replace("<current_amount>", ValueConverter.ConvertToString<int>(this.targetsKilled))
                .Replace("<target_creature>", critName);
            base.UpdateDescription();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is ApexExpertChallenge apexChallenge && apexChallenge.targetCreature == targetCreature);
        }

        public override int Points()
        {
            int points = 0;
            if (targetCreature == CreatureTemplate.Type.RedLizard)
                points = 34;
            else if (targetCreature == CreatureTemplate.Type.RedCentipede)
                points = 38;
            else if (targetCreature == CreatureTemplate.Type.DaddyLongLegs)
                points = 46;
            else if (targetCreature == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.MirosVulture)
                points = 66;

            return points * targetAmount * (this.hidden? 2 : 1);
        }

        public override void Reset()
        {
            targetsKilled = 0;
            base.Reset();
        }

    }

    public class SeaOfferingChallenge : Challenge
    {
        enum SeaOfferingTypes
        {
            EggBug,
            LanternMouse,
            Yeek
        }
        CreatureTemplate.Type offeringType;
        EntityID offeringID;
        float lastThrown;
        public SeaOfferingChallenge()
        {
            On.Player.ThrowObject += Player_ThrowObject;
            On.BigEel.Crush += BigEel_Crush;
            offeringID = new EntityID();
            lastThrown = 0f;
        }
        ~SeaOfferingChallenge()
        {
            On.Player.ThrowObject -= Player_ThrowObject;
            On.BigEel.Crush -= BigEel_Crush;
        }

        private void Player_ThrowObject(On.Player.orig_ThrowObject orig, Player self, int grasp, bool eu)
        {
            try
            {
                if (!completed)
                {
                    if (self.grasps[grasp] != null && self.grasps[grasp].grabbed is Creature crit)
                    {
                        if(crit.abstractCreature.creatureTemplate.type == offeringType && !crit.dead)
                        {
                            offeringID = crit.abstractCreature.ID;
                            lastThrown = Time.time;
                        }
                    }
                }
            }
            catch(Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, grasp, eu);
            }
        }

        private void BigEel_Crush(On.BigEel.orig_Crush orig, BigEel self, PhysicalObject obj)
        {
            try
            {
                if(!completed)
                    if(obj != null && obj.abstractPhysicalObject.ID == offeringID)
                        if(Time.time - lastThrown < 8f)
                            CompleteChallenge();
            }catch(Exception e)
            {
                ExpeditionsExpandedMod.ExpLogger.LogError(e);
            }
            finally
            {
                orig(self, obj);
            }
        }



        public override Challenge Generate()
        {
            bool canHaveYeeks = false;

            if(ExpeditionData.challengeDifficulty > 0.8f)
            {
                List<string> regions = SlugcatStats.SlugcatStoryRegions(ExpeditionData.slugcatPlayer);
                regions.AddRange(SlugcatStats.SlugcatOptionalRegions(ExpeditionData.slugcatPlayer));
                if (regions.Contains("OE") || regions.Contains("CL") || regions.Contains("RM"))
                    canHaveYeeks = true;
            }

            CreatureTemplate.Type chosenType;
            System.Random r = new System.Random();
            int select = 1;
            if (canHaveYeeks)
                select = r.Next(0, 3);
            else
                select = r.Next(0, 2);
            switch ((SeaOfferingTypes)select)
            {
                case SeaOfferingTypes.LanternMouse:
                    chosenType = CreatureTemplate.Type.LanternMouse;
                    break;
                case SeaOfferingTypes.EggBug:
                    chosenType = CreatureTemplate.Type.EggBug;
                    break;
                case SeaOfferingTypes.Yeek:
                    chosenType = MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.Yeek;
                    break;
                default:
                    chosenType = CreatureTemplate.Type.EggBug; 
                    break;
            }

            return new SeaOfferingChallenge
            {
                offeringType = chosenType
            };
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "SeaOfferingChallenge",
                "~",
                offeringType.value,
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
            string[] array = Regex.Split(args, "><");
            this.offeringType = new CreatureTemplate.Type(array[0]);
            this.completed = (array[1] == "1");
            this.hidden = (array[2] == "1");
            this.revealed = (array[3] == "1");
            this.UpdateDescription();
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Sea Offering");
        }

        public override void UpdateDescription()
        {
            string critName = "Unknown";
            if (this.offeringType.Index >= 0)
                critName = ChallengeTools.IGT.Translate(offeringType.ToString());

            this.description = ChallengeTools.IGT.Translate("Offer one <creature> alive to a Leviathan.")
                .Replace("<creature>", critName);
                
            base.UpdateDescription();
        }

        public override int Points()
        {
            int points = 65;
            if (offeringType == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.Yeek)
                points = 90;
            return points * (this.hidden? 2 : 1);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is SeaOfferingChallenge);
        }

    }

}
