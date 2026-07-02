using System;
using System.Collections.Generic;
using System.Linq;
using ClientCore;
using DTAClient.Domain.Multiplayer;
using Rampastring.Tools;

namespace DTAClient.DXGUI.Multiplayer.GameLobby
{
    public static class MapCodeHelper
    {
        /// <summary>
        /// 将组件自定义INI文件中的代码应用到地图INI文件。
        /// </summary>
        /// <param name="mapIni">地图INI文件。</param>
        /// <param name="customIniPath">自定义INI文件路径。</param>
        /// <param name="gameMode">当前选中的游戏模式（如果已设置）。</param>
        public static void ApplyMapCode(IniFile mapIni, string customIniPath, GameMode gameMode)
        {
            IniFile associatedIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, customIniPath));
            string extraIniName = null;
            if (gameMode != null)
                extraIniName = associatedIni.GetStringValue("GameModeIncludes", gameMode.Name, null);
            associatedIni.EraseSectionKeys("GameModeIncludes");
            ApplyMapCode(mapIni, associatedIni);
            if (!String.IsNullOrEmpty(extraIniName))
                ApplyMapCode(mapIni, new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, extraIniName)));
        }

        /// <summary>
        /// 将任意INI文件中的地图代码应用到地图INI文件。
        /// </summary>
        /// <param name="mapIni">地图INI文件。</param>
        /// <param name="mapCodeIni">要应用到地图INI文件的INI文件。</param>
        public static void ApplyMapCode(IniFile mapIni, IniFile mapCodeIni)
        {
            ReplaceMapObjects(mapIni, mapCodeIni, "Aircraft");
            ReplaceMapObjects(mapIni, mapCodeIni, "Infantry");
            ReplaceMapObjects(mapIni, mapCodeIni, "Units");
            ReplaceMapObjects(mapIni, mapCodeIni, "Structures");
            ReplaceMapObjects(mapIni, mapCodeIni, "Terrain");
            IniFile.ConsolidateIniFiles(mapIni, mapCodeIni);
        }

        /// <summary>
        /// 替换特定地图节中与ID匹配的所有对象实例为新的对象ID。
        /// </summary>
        /// <param name="mapIni">地图INI文件。</param>
        /// <param name="mapCodeIni">要应用到地图INI文件的INI文件。</param>
        /// <param name="sectionName">对象节ID。</param>
        private static void ReplaceMapObjects(IniFile mapIni, IniFile mapCodeIni, string sectionName)
        {
            string replaceSectionName = "ReplaceMap" + sectionName;

            List<KeyValuePair<string, string>> objectRemapPairs = GetKeyValuePairs(mapCodeIni, replaceSectionName);
            if (objectRemapPairs.Count < 1) return;

            List<KeyValuePair<string, string>> sectionKeyValuePairs = GetKeyValuePairs(mapIni, sectionName);

            foreach (KeyValuePair<string, string> objectRemapPair in objectRemapPairs)
            {
                List<KeyValuePair<string, string>> matchingSectionKVPs =
                    sectionKeyValuePairs.Where(x => GetObjectID(x.Value, sectionName) == objectRemapPair.Key).ToList();

                foreach (KeyValuePair<string, string> matchingSectionKVP in matchingSectionKVPs)
                {
                    string id = GetObjectID(matchingSectionKVP.Value, sectionName);

                    if (!String.IsNullOrEmpty(objectRemapPair.Value))
                    {
                        mapIni.SetStringValue(sectionName, matchingSectionKVP.Key, matchingSectionKVP.Value.Replace(id, objectRemapPair.Value));
                        Logger.Log("MapCodeHelper: Changed an instance of '" + sectionName + "' object '" + id + "' into '" + objectRemapPair.Value + "'.");
                    }
                    else
                    {
                        mapIni.SetStringValue(sectionName, matchingSectionKVP.Key, "");
                        Logger.Log("MapCodeHelper: Removed an instance of '" + sectionName + "' object '" + id + "'.");
                    }
                }
            }

            mapCodeIni.EraseSectionKeys(replaceSectionName);
        }

        /// <summary>
        /// 从对象节值中获取对象ID。
        /// </summary>
        /// <param name="value">对象节值。</param>
        /// <param name="sectionName">节ID。</param>
        /// <returns></returns>
        private static string GetObjectID(string value, string sectionName)
        {
            if (sectionName != "Terrain")
            {
                string[] splitValue = value.Split(',');
                if (splitValue.Length < 2) return "N/A";
                else return splitValue[1];
            }
            else
                return value;
        }

        /// <summary>
        /// 从INI文件节中获取键值对。
        /// </summary>
        /// <param name="iniFile">INI文件。</param>
        /// <param name="sectionName">INI文件节。</param>
        /// <returns>所选INI文件节的键值对列表。如果节没有键，则返回空列表。</returns>
        private static List<KeyValuePair<string, string>> GetKeyValuePairs(IniFile iniFile, string sectionName)
        {
            IniSection section = iniFile.GetSection(sectionName);
            if (section == null)
                return new List<KeyValuePair<string, string>>();
            return section.Keys;
        }
    }
}
