// Copyright 2020 Aaron R Robinson
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is furnished
// to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
// PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.Setup.Configuration;
using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CompilerPaths.BuildTasks
{
    /// <summary>
    /// MSBuild task used to find the MSVC compiler and platform SDK.
    /// </summary>
    public class FindMSVCCompiler : Task
    {
        private static readonly Lazy<IEnumerable<VSInstall>> g_VsInstalls = new Lazy<IEnumerable<VSInstall>>(GetAllVSWithVCInstallPath, true);
        private static readonly Lazy<IEnumerable<WinSDK>> g_WinSdks = new Lazy<IEnumerable<WinSDK>>(GetAllWinSDK, true);

        /// <summary>
        /// Platform of desired compiler.
        /// </summary>
        /// <remarks>
        /// This is typically the value defined in the MSBuild 'Platform' property.
        /// </remarks>
        [Required]
        public string Platform { get; set; }

        /// <summary>
        /// [Optional] The desired version of Visual Studio to target.
        /// </summary>
        /// <remarks>
        /// If not set the latest will be returned.
        /// </remarks>
        public string VSVersion { get; set; }

        /// <summary>
        /// [Optional] The desired version of Windows SDK to target.
        /// </summary>
        /// <remarks>
        /// If not set the latest will be returned.
        /// </remarks>
        public string WinSDKVersion { get; set; }

        /// <summary>
        /// Absolutely path to compiler.
        /// </summary>
        [Output]
        public string CompilerPath { get; set; }

        /// <summary>
        /// Collection of typical include paths to use.
        /// </summary>
        [Output]
        public string[] IncludePaths { get; set; }

        /// <summary>
        /// Collection of typical lib paths to use.
        /// </summary>
        [Output]
        public string[] LibPaths { get; set; }

        /// <inheritdoc />
        public override bool Execute()
        {
            PlatformType platform = ConvertPlatform();

            var vsInstalls = g_VsInstalls.Value;
            VSInstall vsInstall = null;
            if (string.IsNullOrEmpty(this.VSVersion))
            {
                vsInstall = vsInstalls.First();
            }
            else
            {
                if (!Version.TryParse(this.VSVersion, out Version vs))
                {
                    throw new Exception($"Invalid version format for Visual Studio: '{this.VSVersion}'");
                }

                // Enumerate VS versions.
                foreach (var versionMaybe in vsInstalls)
                {
                    this.Log.LogMessage(MessageImportance.Normal, $"Consider Visual Studio version: {versionMaybe.Version}");
                    if (versionMaybe.Version == vs)
                    {
                        vsInstall = versionMaybe;
                        break;
                    }
                }

                if (vsInstall == null)
                {
                    throw new Exception($"Visual Studio version not found: {this.VSVersion}");
                }
            }

            var winSdks = g_WinSdks.Value;
            WinSDK winSdk = null;
            if (string.IsNullOrEmpty(this.WinSDKVersion))
            {
                winSdk = winSdks.First();
            }
            else
            {
                if (!Version.TryParse(this.WinSDKVersion, out Version vs))
                {
                    throw new Exception($"Invalid version format for Windows SDK: '{this.WinSDKVersion}'");
                }

                // Enumerate VS versions.
                foreach (var versionMaybe in winSdks)
                {
                    this.Log.LogMessage(MessageImportance.Normal, $"Consider Windows SDK version: {versionMaybe.Version}");
                    if (versionMaybe.Version == vs)
                    {
                        winSdk = versionMaybe;
                        break;
                    }
                }

                if (winSdk == null)
                {
                    throw new Exception($"Windows SDK version not found: {this.WinSDKVersion}");
                }
            }

            string vcToolDir = GetVCToolsRootDir(vsInstall.Install);

            // Compute bin directory for compiler
            var hostPlatform = Environment.Is64BitOperatingSystem ? PlatformType.x64 : PlatformType.x86;
            this.CompilerPath = Path.Combine(vcToolDir, "bin", $"Host{hostPlatform}", platform.ToString(), "cl.exe");

            // Collect the include paths
            var incPaths = new List<string>();

            var vcIncDir = Path.Combine(vcToolDir, "include");
            incPaths.Add(vcIncDir);

            // Add WinSDK inc paths
            incPaths.AddRange(winSdk.IncPaths);

            this.IncludePaths = incPaths.ToArray();

            // Collect the lib paths
            var libPaths = new List<string>();

            var vcLibDir = Path.Combine(vcToolDir, "lib", platform.ToString());
            libPaths.Add(vcLibDir);

            // Add WinSDK lib paths
            foreach (var libPath in winSdk.LibPaths)
            {
                libPaths.Add(Path.Combine(libPath, platform.ToString()));
            }

            this.LibPaths = libPaths.ToArray();

            return true;
        }

        private enum PlatformType
        {
            x86,
            x64,
        }

        private PlatformType ConvertPlatform()
        {
            if (this.Platform is null)
            {
                throw new NullReferenceException(nameof(this.Platform));
            }

            return this.Platform switch
            {
                "x86" => PlatformType.x86,
                "Win32" => PlatformType.x86,
                "x64" => PlatformType.x64,
                "AnyCPU" => Environment.Is64BitOperatingSystem ? PlatformType.x64 : PlatformType.x86,
                _ => throw new NotSupportedException($"Unknown platform supplied: {this.Platform}")
            };
        }

        private static string GetVCToolsRootDir(string vsInstallDir)
        {
            var vcToolsRoot = Path.Combine(vsInstallDir, "VC\\Tools\\MSVC\\");

            var latestToolVersion = new Version();
            string latestPath = null;
            foreach (var dirMaybe in Directory.EnumerateDirectories(vcToolsRoot))
            {
                var verDir = Path.GetFileName(dirMaybe);

                if (!Version.TryParse(verDir, out Version latestMaybe)
                    || latestMaybe < latestToolVersion)
                {
                    continue;
                }

                latestToolVersion = latestMaybe;
                latestPath = dirMaybe;
            }

            return latestPath ?? throw new Exception("Unknown VC Tools version found.");
        }

        // Used to sort version entries in descending order as
        // to defer to the latest version.
        private class VersionDescendingOrder : IComparer<Version>
        {
            public int Compare(Version x, Version y) => y.CompareTo(x);
        }

        private class VSInstall
        {
            public Version Version;
            public string Install;
        }

        private static IEnumerable<VSInstall> GetAllVSWithVCInstallPath()
        {
            var vsInstalls = new SortedList<Version, VSInstall>(new VersionDescendingOrder());

            var setupConfig = new SetupConfiguration();
            IEnumSetupInstances enumInst = setupConfig.EnumInstances();
            while (true)
            {
                var el = new ISetupInstance[1];
                enumInst.Next(1, el, out int ret);
                if (ret != 1)
                {
                    break;
                }

                var vsInst = (ISetupInstance2)el[0];
                ISetupPackageReference[] pkgs = vsInst.GetPackages();
                foreach (var n in pkgs)
                {
                    var ver = new Version(vsInst.GetInstallationVersion());
                    if (n.GetId().Equals("Microsoft.VisualStudio.Component.VC.Tools.x86.x64"))
                    {
                        vsInstalls.Add(ver, new VSInstall()
                        {
                            Version = ver,
                            Install = vsInst.GetInstallationPath()
                        });
                        break;
                    }
                }
            }

            if (!vsInstalls.Any())
            {
                throw new Exception("Visual Studio with VC Tools package must be installed.");
            }

            return vsInstalls.Values.ToArray();
        }

        private class WinSDK
        {
            public Version Version;
            public IEnumerable<string> IncPaths;
            public IEnumerable<string> LibPaths;
        }

        private static IEnumerable<WinSDK> GetAllWinSDK()
        {
            var sdks = new SortedList<Version, WinSDK>(new VersionDescendingOrder());

            using (var kits = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Kits\Installed Roots"))
            {
                string win10sdkRoot = (string)kits.GetValue("KitsRoot10");

                // Collect the possible SDK versions.
                foreach (var verMaybe in kits.GetSubKeyNames())
                {
                    if (!Version.TryParse(verMaybe, out Version versionMaybe))
                    {
                        continue;
                    }

                    // WinSDK inc and lib paths
                    var incDir = Path.Combine(win10sdkRoot, "Include", verMaybe);
                    var libDir = Path.Combine(win10sdkRoot, "Lib", verMaybe);
                    if (!Directory.Exists(incDir) || !Directory.Exists(libDir))
                    {
                        continue;
                    }

                    var sharedIncDir = Path.Combine(incDir, "shared");
                    var umIncDir = Path.Combine(incDir, "um");
                    var ucrtIncDir = Path.Combine(incDir, "ucrt");
                    var umLibDir = Path.Combine(libDir, "um");
                    var ucrtLibDir = Path.Combine(libDir, "ucrt");

                    sdks.Add(versionMaybe, new WinSDK()
                    {
                        Version = versionMaybe,
                        IncPaths = new[] { sharedIncDir, umIncDir, ucrtIncDir },
                        LibPaths = new[] { umLibDir, ucrtLibDir },
                    });
                }
            }

            if (!sdks.Any())
            {
                throw new Exception("No Win10 SDK version found.");
            }

            return sdks.Values.ToArray();
        }
    }
}
