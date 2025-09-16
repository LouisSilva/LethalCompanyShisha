using LethalLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace LethalCompanyShisha.Core;

internal static class Assets
{
    internal static AssetBundle MainAssetBundle;

    internal static void LoadAssetBundle(string assetBundleName)
    {
        try
        {
            string s = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (s == null)
            {
                throw new InvalidOperationException($"Could not find assetbundle: {assetBundleName}");
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(s, assetBundleName));
            MainAssetBundle = bundle;
        }
        catch (Exception e)
        {
            Plugin.logger.LogWarning($"Could not load assetbundle: {e}");
            MainAssetBundle = null;
        }
    }
}