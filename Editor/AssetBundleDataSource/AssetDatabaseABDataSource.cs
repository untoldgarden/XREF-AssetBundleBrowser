using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor.IMGUI.Controls;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections;
using UnityEngine.Networking;

namespace AssetBundleBrowser.AssetBundleDataSource
{
    internal class AssetDatabaseABDataSource : ABDataSource
    {
        public static List<ABDataSource> CreateDataSources()
        {
            var op = new AssetDatabaseABDataSource();
            var retList = new List<ABDataSource>();
            retList.Add(op);
            return retList;
        }

        public string Name
        {
            get
            {
                return "Default";
            }
        }

        public string ProviderName
        {
            get
            {
                return "Built-in";
            }
        }

        public string[] GetAssetPathsFromAssetBundle(string assetBundleName)
        {
            return AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
        }

        public string GetAssetBundleName(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                return string.Empty;
            }
            var bundleName = importer.assetBundleName;
            if (importer.assetBundleVariant.Length > 0)
            {
                bundleName = bundleName + "." + importer.assetBundleVariant;
            }
            return bundleName;
        }

        public string GetImplicitAssetBundleName(string assetPath)
        {
            return AssetDatabase.GetImplicitAssetBundleName(assetPath);
        }

        public string[] GetAllAssetBundleNames()
        {
            return AssetDatabase.GetAllAssetBundleNames();
        }

        public bool IsReadOnly()
        {
            return false;
        }

        public void SetAssetBundleNameAndVariant(string assetPath, string bundleName, string variantName)
        {
            AssetImporter.GetAtPath(assetPath).SetAssetBundleNameAndVariant(bundleName, variantName);
        }

        public void RemoveUnusedAssetBundleNames()
        {
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }

        public bool CanSpecifyBuildTarget
        {
            get { return true; }
        }
        public bool CanSpecifyBuildOutputDirectory
        {
            get { return true; }
        }

        public bool CanSpecifyBuildOptions
        {
            get { return true; }
        }

        public bool BuildAssetBundles(ABBuildInfo info)
        {
            if (info == null)
            {
                Debug.Log("Error in build");
                return false;
            }

            string guidelines = GetMetaGuidelines();

            if (guidelines == "")
            {
                Debug.LogError("GetMetaGuidelines failed");
                return false;
            }
            string unityVersion = Application.unityVersion;
            Debug.Log("unityVersion: " + unityVersion);
            //parse the res json into dictionary
            Dictionary<string, object> dict = Google.MiniJSON.Json.Deserialize(guidelines) as Dictionary<string, object>;
            if (dict == null)
            {
                Debug.LogError("parse json failed");
                return false;
            }
            //get the unity version
            string uv = dict["unityVersion"] as string;
            bool majorMinorIsEqual = false;
            bool patchIsEqual = false;
            string minCompatibleVersion = "";

            CheckUnityVersionCompatibility(uv, out majorMinorIsEqual, out patchIsEqual, out minCompatibleVersion);

            if (!majorMinorIsEqual)
            {
                Debug.LogError("You are not using the correct version of Unity. Minimum compatible version: " + minCompatibleVersion + "; Recommended version: " + uv);
                //show error popup
                EditorUtility.DisplayDialog("Error", "You are not using the correct version of Unity. \n Minimum compatible version: " + minCompatibleVersion + " \n Recommended version: " + uv, "OK");
                return false;
            }
            // show working alert popup if the patch version is not equal
            if (!patchIsEqual)
            {
                bool result = EditorUtility.DisplayDialog("Warning", "You are not using the recommended version of Unity. \n Unknown issues might occur. \n Recommended version: " + uv + ". \n Do you want to continue?", "Yes", "No");
                if (!result)
                {
                    return false;
                }
                return Build(info);
            
            }
            else
            {
                return Build(info);
            }



        }

        public bool Build(ABBuildInfo info)
        {
                //create folder callded Temp if it doesn't exist
                if (!Directory.Exists("Assets/Temp"))
                {
                    Directory.CreateDirectory("Assets/Temp");
                }

                // Path for the meta file - adjust as needed
                string xrefVersion, xrefEBVersion, xrefLatestVersion, xrefEBLatestVersion;
                CheckPackageVersion("com.untoldgarden.xref", out xrefVersion, out xrefLatestVersion);
                CheckPackageVersion("com.untoldgarden.xref-experience-builder", out xrefEBVersion, out xrefEBLatestVersion);

                //cancel if the latest  version is not installed
                if (xrefVersion != xrefLatestVersion || xrefEBVersion != xrefEBLatestVersion)
                {
                    Debug.LogError("XREF and XREF Experience Builder must be updated to the latest  version.");
                    return false;
                }

                string metaFilePath = "Assets/Temp/XREFBundleMeta.txt";
                if (xrefVersion == "ERROR" || xrefEBVersion == "ERROR")
                {
                    Debug.LogError("Error while checking package versions");
                    return false;
                }
                if (xrefVersion == "NOT_FOUND" || xrefEBVersion == "NOT_FOUND")
                {
                    Debug.LogError("Package not found. XREF and XREF Experience Builder must be installed.");
                    return false;
                }
                string meta = "com.untoldgarden.xref: " + xrefVersion + "\n" + "com.untoldgarden.xref-experience-builder: " + xrefEBVersion;
                File.WriteAllText(metaFilePath, meta);

                // Get all asset bundle names
                string[] allBundleNames = AssetDatabase.GetAllAssetBundleNames();
                List<AssetBundleBuild> buildMap = new List<AssetBundleBuild>();
                List<string> tempMetafiles = new List<string>();
                foreach (string bundleName in allBundleNames)
                {
                    //create folder callded Temp if it doesn't exist
                    if (!Directory.Exists($"Assets/Temp/{bundleName}"))
                    {
                        Directory.CreateDirectory($"Assets/Temp/{bundleName}");
                    }
                    string newMetaFilePath = $"Assets/Temp/{bundleName}/XREFBundleMeta.txt";
                    File.Copy(metaFilePath, newMetaFilePath, overwrite: true);
                    tempMetafiles.Add(newMetaFilePath);

                    // Get all asset paths for each bundle
                    string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);

                    // Create a new AssetBundleBuild and set its properties
                    AssetBundleBuild buildEntry = new AssetBundleBuild();
                    buildEntry.assetBundleName = bundleName;

                    // Add the meta file to the list of assets
                    var assetList = new List<string>
                {
                    newMetaFilePath // Add meta file to each bundle
                };
                    Debug.Log("bundleName: " + bundleName + "added meta file: " + newMetaFilePath);

                    foreach (string assetPath in assetPaths)
                    {
                        // Check if the asset is a script
                        if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(MonoScript))
                        {
                            // Skip adding this script to the asset bundle
                            continue;
                        }
                        assetList.Add(assetPath);
                    }


                    buildEntry.assetNames = assetList.ToArray();
                    buildMap.Add(buildEntry);
                }


                // Build the asset bundles
                var buildManifest = BuildPipeline.BuildAssetBundles(info.outputDirectory, buildMap.ToArray(), info.options, info.buildTarget);

                // Delete the meta files
                foreach (string metaFile in tempMetafiles)
                {
                    File.Delete(metaFile);
                    //delete the Assets/Temp/{bundleName} folder if it exists
                    string folderPath = "Assets/Temp/" + Path.GetFileNameWithoutExtension(metaFile);
                    if (Directory.Exists(folderPath))
                        Directory.Delete(folderPath, true);
                }

                // var buildManifest = BuildPipeline.BuildAssetBundles(info.outputDirectory, info.options, info.buildTarget);
                if (buildManifest == null)
                {
                    Debug.Log("Error in build");
                    return false;
                }

                foreach (var assetBundleName in buildManifest.GetAllAssetBundles())
                {
                    if (info.onBuild != null)
                    {
                        info.onBuild(assetBundleName);
                    }
                }
                return true;
        }


        public void CheckPackageVersion(string packageName, out string version, out string latestVersion)
        {
            version = "";
            latestVersion = "";
            ListRequest request = Client.List(); // Request the list of packages
            while (!request.IsCompleted)
                System.Threading.Thread.Sleep(10); // Wait until the request is completed

            if (request.Status == StatusCode.Success)
            {
                foreach (var package in request.Result)
                {
                    if (package.name == packageName)
                    {
                        Debug.Log("Package version: " + package.version);
                        version = package.version;
                        latestVersion = package.versions.latest;
                        return;
                    }
                }

                Debug.LogWarning("Package not found: " + packageName);
                version = "NOT_FOUND";
            }
            else if (request.Status >= StatusCode.Failure)
            {
                Debug.LogError("Failed to get packages list.");
                version = "ERROR";
            }
            else
            {
                Debug.LogError("Unknown error while getting packages list.");
                version = "ERROR";
            }
        }
        public string CheckLatestPackageVersion(string packageName)
        {
            ListRequest request = Client.List(); // Request the list of packages
            while (!request.IsCompleted)
                System.Threading.Thread.Sleep(10); // Wait until the request is completed

            if (request.Status == StatusCode.Success)
            {
                foreach (var package in request.Result)
                {
                    if (package.name == packageName)
                    {
                        Debug.Log("Package version latest: " + package.versions.latest);
                        return package.versions.latest;
                    }
                }

                Debug.LogWarning("Package not found: " + packageName);
                return "NOT_FOUND";

            }
            else if (request.Status >= StatusCode.Failure)
            {
                Debug.LogError("Failed to get packages list.");
                return "ERROR";

            }
            else
            {
                Debug.LogError("Unknown error while getting packages list.");
                return "ERROR";

            }
        }
        public string GetMetaGuidelines()
        {
            string storageURL = "https://firebasestorage.googleapis.com/v0/b/xref-client.appspot.com/o/appconfig%2Fxref-bundle-meta.json?alt=media";
            using (UnityWebRequest request = UnityWebRequest.Get(storageURL))
            {
                //send request and pause thread until received
                request.SendWebRequest();
                while (!request.isDone)
                {
                    System.Threading.Thread.Sleep(10);
                }

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"Error downloading file: {request.error}");
                    return "";
                }
                else
                {
                    // Process the response here
                    Debug.Log($"Downloaded file content: {request.downloadHandler.text}");
                    return request.downloadHandler.text;
                }
            }
        }

        public void CheckUnityVersionCompatibility(string unityVersion, out bool majorMinorIsEqual, out bool patchIsEqual, out string minCompatibleVersion)
        {
            majorMinorIsEqual = false;
            patchIsEqual = false;
            minCompatibleVersion = "";
            //unity version format is 2023.1.0f1
            // major and minor version need to be equal between current unity version and provided unityVersion
            string[] unityVersionParts = unityVersion.Split('.')
                .Select(x => x.Trim())
                .ToArray();
            string[] currentUnityVersionParts = Application.unityVersion.Split('.');
            if (currentUnityVersionParts.Length < 2)
            {
                Debug.LogError("Error parsing unity version");
                return;
            }
            if (unityVersionParts.Length < 2)
            {
                Debug.LogError("Error parsing unity version");
                return;
            }
            int currentUnityVersionMinor = int.Parse(currentUnityVersionParts[1]);
            int unityVersionMinor = int.Parse(unityVersionParts[1]);
            int currentUnityVersionMajor = int.Parse(currentUnityVersionParts[0]);
            int unityVersionMajor = int.Parse(unityVersionParts[0]);
            //remove everything after the leter f inclusive (e.g. 2023.1.0f1 -> 2023.1.0)
            currentUnityVersionParts[2] = currentUnityVersionParts[2].Split('f')[0];
            unityVersionParts[2] = unityVersionParts[2].Split('f')[0];
            int currentUnityVersionPatch = int.Parse(currentUnityVersionParts[2]);
            int unityVersionPatch = int.Parse(unityVersionParts[2]);

            majorMinorIsEqual = currentUnityVersionMajor == unityVersionMajor && currentUnityVersionMinor == unityVersionMinor;
            patchIsEqual = currentUnityVersionPatch == unityVersionPatch;
            minCompatibleVersion = unityVersionMajor + "." + unityVersionMinor;


        }
    }
}
