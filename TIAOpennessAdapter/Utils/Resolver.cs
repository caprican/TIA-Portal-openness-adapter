using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;

using Microsoft.Win32;

namespace TIAOpennessAdapter.Utils
{
    /// <summary>
    /// Definition of helper functionality to resolve api dll, modules and options
    /// </summary>
    public static class Resolver
    {
        /// <summary>
        /// Required min version of engineering dll
        /// </summary>
        public const string StrRequiredVersion = "V15.0";
        private const string BasePath = @"SOFTWARE\Siemens\Automation\Openness";
        private const string ReferencedAssembly = "Siemens.Engineering";
        private const string ReferencedHmiAssembly = "Siemens.Engineering.Hmi";
        private const string ModuleBaseInterface = "IOpennessBaseModule";
        private const string ModuleInterface = "IOpennessModule";
        private static string? _assemblyPath = null;
        private static string? _assemblyPathHmi = null;


        //public static Assembly? OnResolve(object sender, ResolveEventArgs args)
        //{
        //    var assemblyName = new AssemblyName(args.Name);
        //    if (!assemblyName.Name.StartsWith(ReferencedAssembly))
        //    {
        //        return null;
        //    }

        //    using var regBaseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        //    using var opennessBaseKey = regBaseKey.OpenSubKey(BasePath);

        //    var opennessVersionBaseKey = opennessBaseKey.GetSubKeyNames().ToList();
        //    opennessVersionBaseKey.RemoveAll((key) =>
        //    {
        //        var subKey = opennessBaseKey.OpenSubKey(key);
        //        var t = subKey.GetSubKeyNames();
        //        var rst = subKey.GetSubKeyNames()?.Contains("PublicAPI");
        //        subKey.Close();
        //        return rst != true;
        //    });

        //    using var registryKeyLatestTiaVersion = opennessBaseKey?.OpenSubKey(opennessVersionBaseKey.Last());

        //    var requestedVersionOfAssembly = assemblyName.Version.ToString();

        //    using var assemblyVersionSubKey = registryKeyLatestTiaVersion?.OpenSubKey("PublicAPI")?.OpenSubKey(requestedVersionOfAssembly);
        //    var siemensEngineeringAssemblyPath = assemblyVersionSubKey?.GetValue(ReferencedAssembly).ToString();

        //    if (siemensEngineeringAssemblyPath == null || !File.Exists(siemensEngineeringAssemblyPath))
        //    {
        //        return null;
        //    }

        //    var assembly = Assembly.LoadFrom(siemensEngineeringAssemblyPath);
        //    return assembly;
        //}

        /// <summary>
        /// Resolve assembly by name
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static Assembly? OnResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            var path = "";

            if (assemblyName.Name.Equals(ReferencedAssembly))
            {
                path = _assemblyPath;
            }

            if (assemblyName.Name.Equals(ReferencedHmiAssembly))
            {
                path = _assemblyPathHmi;
            }

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    //var opennessName = AssemblyName.GetAssemblyName(path);
                    //var asm = Assembly.Load(opennessName);
                    var asm = Assembly.LoadFrom(path);

                    return asm;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            return null;
        }


        /// <summary>
        /// Retrieve assembly by path and version
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public static List<string>? GetAssemblies(string version)
        {
            var key = GetRegistryKey($@"{BasePath}\{version}");

            if (key != null)
            {
                try
                {
                    var subKey = key.OpenSubKey("PublicAPI");

                    if (subKey != null)
                    {
                        var subKeys = subKey.GetSubKeyNames().OrderBy(x => x).ToList();

                        var result = (from item in subKeys
                                      where Convert.ToDecimal(item.Substring(0, 4), CultureInfo.InvariantCulture) >= Convert.ToDecimal(StrRequiredVersion.Substring(1, 4), CultureInfo.InvariantCulture)
                                      select item.Substring(0, 4)).ToList();

                        subKey.Dispose();

                        return result;
                    }
                }
                finally
                {
                    key.Dispose();
                }
            }

            return null;
        }

        /// <summary>
        /// Get version info from registry key
        /// </summary>
        /// <returns></returns>
        public static List<string> GetEngineeringVersions()
        {
            var key = GetRegistryKey(BasePath);

            if (key != null)
            {
                try
                {
                    var names = key.GetSubKeyNames().OrderBy(x => x).ToList();

                    var result = (from item in names
                                  where Convert.ToDecimal(item.Substring(0, 4), CultureInfo.InvariantCulture) >= Convert.ToDecimal(StrRequiredVersion.Substring(1, 4), CultureInfo.InvariantCulture)
                                  select item.Substring(0, 4)).ToList();

                    key.Dispose();

                    return result;
                }
                finally
                {
                    key.Dispose();
                }
            }

            return new List<string>();
        }

        private static RegistryKey GetRegistryKey(string keyName)
        {
            var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            var key = baseKey.OpenSubKey(keyName);
            if (key is null)
            {
                baseKey.Dispose();
                baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
                key = baseKey.OpenSubKey(keyName);
            }
            if (key is null)
            {
                baseKey.Dispose();
                baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                key = baseKey.OpenSubKey(keyName);
            }
            baseKey.Dispose();

            return key;
        }

        /// <summary>
        /// Retrieve the path from assembly by version
        /// </summary>
        /// <param name="version"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static string? GetAssemblyPath(string version, string assembly)
        {
            var libraries = GetOpennessLibraries();
            var portalVersion = new Version(version);
            var apiVersion = new Version(assembly);
            var libraryVersion = libraries.Where(e => e.TiaPortalVersion.Major == portalVersion.Major &&
                                                     e.TiaPortalVersion.Minor == portalVersion.Minor).SingleOrDefault(e => e.PublicApiVersion.Major == apiVersion.Major &&
                                                                                                                           e.PublicApiVersion.Minor == apiVersion.Minor);
            //_assemblyPath = libraryVersion is null ? string.Empty : libraryVersion.LibraryFilePath;
            _assemblyPath = libraryVersion.LibraryFilePath;

            return _assemblyPath;
        }

        /// <summary>
        /// Check if openness api is installed
        /// </summary>
        /// <returns></returns>
        public static bool IsOpennessInstalled()
        {
            var engineeringVersion = GetEngineeringVersions();

            var requiredVersion = (from version in engineeringVersion
                                   where Convert.ToDecimal(version, CultureInfo.InvariantCulture) >= Convert.ToDecimal(StrRequiredVersion.Substring(1, 4), CultureInfo.InvariantCulture)
                                   select version).FirstOrDefault();


            return !string.IsNullOrEmpty(requiredVersion);
        }

        /// <summary>
        /// Get installed openness libraries
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyList<OpennessVersion> GetOpennessLibraries()
        {
            var opennessVersions = new List<OpennessVersion>();

            if (Environment.Is64BitOperatingSystem)
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var registryKey = baseKey.OpenSubKey(BasePath, RegistryKeyPermissionCheck.ReadSubTree, RegistryRights.ReadKey);
                var tiaPortalVersions = registryKey?.GetSubKeyNames();
                if (tiaPortalVersions != null)
                {
                    foreach (var tiaPortalVersion in tiaPortalVersions)
                    {
                        using var publicApi = registryKey?.OpenSubKey(Path.Combine(tiaPortalVersion, "PublicAPI"), RegistryKeyPermissionCheck.ReadSubTree, RegistryRights.ReadKey);
                        var publicApis = publicApi?.GetSubKeyNames();
                        if (publicApis != null)
                        {
                            foreach (var publicApiVersion in publicApis)
                            {
                                using var openness = publicApi?.OpenSubKey(publicApiVersion, RegistryKeyPermissionCheck.ReadSubTree, RegistryRights.ReadKey);
                                var library = openness?.GetValue(ReferencedAssembly) as string;

                                if (!string.IsNullOrWhiteSpace(library) && File.Exists(library))
                                {
                                    var portalVersion = new Version(tiaPortalVersion);
                                    var apiVersion = new Version(publicApiVersion);
                                    var opennessVersion = new OpennessVersion(portalVersion, library, apiVersion);
                                    opennessVersions.Add(opennessVersion);
                                }
                            }
                        }
                    }
                }
            }

            return opennessVersions.AsReadOnly();
        }

        public static string GetOpennessNamespace(Version tiaVersion)
        {
            switch (tiaVersion.Major)
            {
                case 13:
                case 14:
                    return "http://www.siemens.com/automation/Openness/SW/Interface/v1";
                case 15:
                    return tiaVersion.Minor == 1 ? "http://www.siemens.com/automation/Openness/SW/Interface/v2" : "http://www.siemens.com/automation/Openness/SW/Interface/v3";
                case 16:
                    return "http://www.siemens.com/automation/Openness/SW/Interface/v4";
                case 17:
                case 18:
                case 19:
                default:
                    return "http://www.siemens.com/automation/Openness/SW/Interface/v5";
            }
        }
    }
}