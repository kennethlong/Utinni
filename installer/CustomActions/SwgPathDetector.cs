/**
 * MIT License
 *
 * Copyright (c) 2020 Philip Klatt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
**/

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using WixToolset.Dtf.WindowsInstaller;

namespace Utinni.Installer.CustomActions
{
    // Opt-in SWG client path detector (D-20 / D-21 / T-06-06-02 / T-06-06-03).
    //
    // CON-D-01 ships-blank policy: Utinni installs ut.ini with a BLANK
    // [Launcher]swgClientPath. This custom action ONLY runs when the user
    // explicitly ticks the default-OFF "Detect SWG client path" checkbox
    // (property DETECTSWGPATH=1). Even when it runs, it writes a path ONLY when
    // EXACTLY ONE candidate is found; zero or multiple candidates leave ut.ini
    // untouched (blank). The action returns Success on every internal failure
    // (Return="ignore" in the .wxs) so a detection miss never fails the install.
    public class SwgPathDetector
    {
        // The SWG client executable the SWGEmu client ships as.
        private const string SwgClientExe = "SwgClient_r.exe";

        // Deferred, elevated (Impersonate="no") custom action. Reads INSTALLFOLDER
        // from CustomActionData (marshaled by the immediate SetSwgDetectData action,
        // because deferred actions cannot read installer properties directly).
        [CustomAction]
        public static ActionResult DetectSwgPath(Session session)
        {
            session.Log("Utinni: DetectSwgPath begin (opt-in SWG client detection)");

            try
            {
                CustomActionData data = session.CustomActionData;
                string installFolder = data.ContainsKey("INSTALLFOLDER") ? data["INSTALLFOLDER"] : null;

                if (string.IsNullOrEmpty(installFolder))
                {
                    session.Log("Utinni: INSTALLFOLDER not present in CustomActionData; leaving ut.ini blank (CON-D-01).");
                    return ActionResult.Success;
                }

                string utIniPath = Path.Combine(installFolder, "ut.ini");
                if (!File.Exists(utIniPath))
                {
                    session.Log("Utinni: ut.ini not found at " + utIniPath + "; nothing to seed.");
                    return ActionResult.Success;
                }

                List<string> candidates = ProbeCandidates(session);

                if (candidates.Count == 0)
                {
                    session.Log("Utinni: no SWG client path detected; ut.ini stays blank (CON-D-01).");
                    return ActionResult.Success;
                }

                if (candidates.Count > 1)
                {
                    session.Log("Utinni: multiple SWG client paths detected (" + string.Join(", ", candidates) +
                                "); ambiguous, so ut.ini stays blank (CON-D-01). User can set it from the Launcher.");
                    return ActionResult.Success;
                }

                string detected = candidates[0];
                session.Log("Utinni: exactly one SWG client detected at " + detected + "; seeding [Launcher]swgClientPath in ut.ini.");
                SetIniValue(utIniPath, "Launcher", "swgClientPath", detected);
                SetIniValue(utIniPath, "Launcher", "swgClientName", SwgClientExe);
            }
            catch (Exception ex)
            {
                // Never fail the install for a detection problem (T-06-06-03).
                session.Log("Utinni: DetectSwgPath swallowed exception (ut.ini left blank): " + ex.Message);
            }

            return ActionResult.Success;
        }

        // Returns the set of directories that contain a SWGEmu client executable.
        private static List<string> ProbeCandidates(Session session)
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Filesystem probes (the common SWGEmu install roots).
            string[] fsRoots =
            {
                @"C:\SWGEmu",
                @"C:\Program Files\SWGEmu",
                @"C:\Program Files (x86)\SWGEmu",
            };
            foreach (string root in fsRoots)
            {
                if (DirHasClient(root)) { found.Add(root); }
            }

            // Registry probes. Check both 32- and 64-bit views explicitly so WOW64
            // redirection of the deferred CA host bitness does not hide a key.
            ProbeRegistry(session, RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\SWGEmu", found);
            ProbeRegistry(session, RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\SWGEmu", found);
            ProbeRegistry(session, RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\SWGEmu", found);
            ProbeRegistry(session, RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\StarWarsGalaxies", found);
            ProbeRegistry(session, RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\StarWarsGalaxies", found);

            return new List<string>(found);
        }

        private static void ProbeRegistry(Session session, RegistryHive hive, RegistryView view, string subKey, HashSet<string> found)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(subKey))
                {
                    if (key == null) { return; }

                    // SWGEmu installers commonly store the install dir under one of these value names.
                    foreach (string valueName in new[] { "InstallDir", "Path", "Folder", "" })
                    {
                        var value = key.GetValue(valueName) as string;
                        if (!string.IsNullOrEmpty(value) && DirHasClient(value))
                        {
                            found.Add(value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                session.Log("Utinni: registry probe of " + hive + "\\" + subKey + " (" + view + ") failed: " + ex.Message);
            }
        }

        private static bool DirHasClient(string dir)
        {
            try
            {
                return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, SwgClientExe));
            }
            catch
            {
                return false;
            }
        }

        // Minimal in-place INI value setter. Preserves the rest of the file. If the
        // [section] exists, updates (or appends) the key under it; otherwise appends
        // a new section. Matches the simple flat [section]/key=value shape of ut.ini.
        private static void SetIniValue(string iniPath, string section, string key, string value)
        {
            string[] lines = File.ReadAllLines(iniPath);
            var output = new List<string>(lines.Length + 2);

            bool inTarget = false;
            bool wroteKey = false;
            bool sectionSeen = false;
            string sectionHeader = "[" + section + "]";

            foreach (string raw in lines)
            {
                string trimmed = raw.Trim();

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    // Leaving a section: if it was the target and we never wrote the key, add it now.
                    if (inTarget && !wroteKey)
                    {
                        output.Add(key + " = " + value);
                        wroteKey = true;
                    }
                    inTarget = string.Equals(trimmed, sectionHeader, StringComparison.OrdinalIgnoreCase);
                    if (inTarget) { sectionSeen = true; }
                    output.Add(raw);
                    continue;
                }

                if (inTarget && !wroteKey)
                {
                    int eq = trimmed.IndexOf('=');
                    if (eq > 0)
                    {
                        string existingKey = trimmed.Substring(0, eq).Trim();
                        if (string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
                        {
                            output.Add(key + " = " + value);
                            wroteKey = true;
                            continue;
                        }
                    }
                }

                output.Add(raw);
            }

            if (inTarget && !wroteKey)
            {
                output.Add(key + " = " + value);
                wroteKey = true;
            }

            if (!sectionSeen)
            {
                output.Add(sectionHeader);
                output.Add(key + " = " + value);
            }

            File.WriteAllLines(iniPath, output);
        }
    }
}
