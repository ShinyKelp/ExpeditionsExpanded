using Expedition;
using Menu.Remix;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using RWCustom;
using UnityEngine.Events;

namespace ExpeditionsExpanded
{
    public interface IRegionSpecificChallenge
    {
        public List<string> ApplicableRegions { get; set; }
    }

    public interface IChallengeHooks
    {
        public void ApplyHooks();
        public void RemoveHooks();
    }

    public static class ECEUtilities
    {
        public static readonly HashSet<CreatureTemplate.Type> Critters = new HashSet<CreatureTemplate.Type>();

        public static BepInEx.Logging.ManualLogSource ExpLogger { get; internal set; }

        public static UnityAction OnAllPlayersDied;

        public static UnityAction OnHibernated;

        internal static Dictionary<string, HashSet<string>> UserDefinedRegionFilters;
        internal static void ReadUserRegionFilters()
        {
            UserDefinedRegionFilters = new Dictionary<string, HashSet<string>>();
            foreach (string filePath in Directory.GetFiles(Custom.RootFolderDirectory() + "/ExpeditionsExpanded/RegionFilters"))
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

        private static bool PassFilters(string lineFilters)
        {
            string[] splitDef = lineFilters.Split(',');
            lineFilters = splitDef[0];
            for (int i = 1; i < splitDef.Length; i++)
            {
                string iSplit = splitDef[i];
                if (iSplit.Contains(':'))
                {
                    string[] splitFilter = iSplit.Split(':');
                    switch (splitFilter[0])
                    {
                        case "Diff":
                            float difficultyFilter = float.Parse(splitFilter[1]);
                            if (ExpeditionData.challengeDifficulty < difficultyFilter)
                                return false;
                            break;
                        case "Player":
                            if (splitFilter[1] == "Survivor")
                                splitFilter[1] = "White";
                            else if (splitFilter[1] == "Monk")
                                splitFilter[1] = "Yellow";
                            else if (splitFilter[1] == "Hunter")
                                splitFilter[1] = "Red";
                            else if (splitFilter[1] == "Spearmaster")
                                splitFilter[1] = "Spear";
                            if (splitFilter[1] != ExpeditionData.slugcatPlayer.ToString())
                                return false;
                            break;
                    }
                }
            }
            return true;
        }

        public static string FilterAndSelectRegion(string challengeName, List<string> applicableRegions, string altChallengeName = "")
        {
            if (applicableRegions is null)
                applicableRegions = new List<string>();
            string selectedRegionAcronym = "";
            HashSet<string> filters;
            if (UserDefinedRegionFilters.ContainsKey(challengeName))
                filters = UserDefinedRegionFilters[challengeName];
            else if (UserDefinedRegionFilters.ContainsKey(challengeName + "Challenge"))
                filters = UserDefinedRegionFilters[challengeName + "Challenge"];
            else if (UserDefinedRegionFilters.ContainsKey(altChallengeName))
                filters = UserDefinedRegionFilters[altChallengeName];
            else if (UserDefinedRegionFilters.ContainsKey(altChallengeName + "Challenge"))
                filters = UserDefinedRegionFilters[challengeName + "Challenge"];
            else filters = new HashSet<string>();
            foreach (string line in filters)
            {
                string regionDef = line;
                string regionName;
                int operation = 0;  //0: Do nothing. 1: Add. 2: Remove.
                if (regionDef.StartsWith("!"))
                {
                    operation = 2;
                    regionName = regionDef = regionDef.Substring(1);
                }
                else
                {
                    operation = 1;
                    regionName = regionDef;
                }

                if (regionDef.Contains(','))
                {
                    regionName = regionDef.Split(',')[0];
                    if (!PassFilters(regionDef))
                        operation = 0;
                }

                if (operation == 1 && !applicableRegions.Contains(regionName))
                    applicableRegions.Add(regionName);
                else if (operation == 2)
                    applicableRegions.Remove(regionName);
            }
            if (applicableRegions.Count > 0)
            {
                int selected = UnityEngine.Random.Range(0, applicableRegions.Count);
                selectedRegionAcronym = applicableRegions[selected];
            }
            return selectedRegionAcronym;
        }

        public static bool IsRegionForbidden(string challengeName, string regionAcronym, string altChallengeName = "")
        {
            HashSet<string> filters;
            if (UserDefinedRegionFilters.ContainsKey(challengeName))
                filters = UserDefinedRegionFilters[challengeName];
            else if (UserDefinedRegionFilters.ContainsKey(challengeName + "Challenge"))
                filters = UserDefinedRegionFilters[challengeName + "Challenge"];
            else if (UserDefinedRegionFilters.ContainsKey(altChallengeName))
                filters = UserDefinedRegionFilters[altChallengeName];
            else if (UserDefinedRegionFilters.ContainsKey(altChallengeName + "Challenge"))
                filters = UserDefinedRegionFilters[altChallengeName + "Challenge"];
            else
                return false;

            bool forbidden = false;

            foreach (string line in filters)
            {
                if (!line.StartsWith("!") && line.StartsWith(regionAcronym) && forbidden)
                {
                    if (!line.Contains(',') || PassFilters(line.Substring(1)))
                        forbidden = false;
                }
                else if (line.StartsWith("!" + regionAcronym))
                {
                    if (!line.Contains(',') || PassFilters(line.Substring(1)))
                        forbidden = true;
                }
            }
            return forbidden;
        }
        public static bool DiedLastSession()
        {
            string slugcatPlayer = ExpeditionData.slugcatPlayer.ToString();
            string filePath = Custom.RootFolderDirectory() + "/ExpeditionsExpanded/internal/" + slugcatPlayer + ".txt";
            return File.Exists(filePath);
        }
    }

}
