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
using System.IO;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;

namespace UtinniCoreDotNetGen
{
    public class Program
    {
        class Gen : ILibrary
        {
            private readonly string[] _args;

            public Gen(string[] args)
            {
                _args = args;
            }

            public void Setup(Driver driver)
            {
                string workingDir = AppDomain.CurrentDomain.BaseDirectory;
                string buildMode = new DirectoryInfo(workingDir).Name;
                // C-15: replaced brittle Substring(0, workingDir.LastIndexOf("\\bin\\"))
                // which threw InvalidOperationException when \bin\ was not in the path
                // (e.g. CI runner output paths, env-var fallback). ResolveSlnDir supports
                // three resolution modes: args[0], walk-up to Utinni.sln, UTINNI_SLN_DIR env var.
                string slnDir = ResolveSlnDir(workingDir, _args);

                driver.ParserOptions.TargetTriple = "i686-pc-win32-msvc"; // Generates x86 EntryPoints

                // Needed for spdlog to work
                driver.ParserOptions.UnityBuild = true;
                driver.ParserOptions.EnableRTTI = true;
                driver.ParserOptions.AddDefines("SPDLOG_NO_EXCEPTIONS");
                driver.ParserOptions.AddDefines("FMT_EXCEPTIONS=0");

                var options = driver.Options;
                options.GeneratorKind = GeneratorKind.CSharp;
                options.OutputDir = slnDir + "UtinniCoreDotNet\\Generated\\";
                options.GenerateDebugOutput = false;

                options.SystemModule.SymbolsLibraryName = "UtinniCore-Symbols";

                string targetProjName = "UtinniCore";
                var module = options.AddModule(targetProjName);

                // Includes
                module.IncludeDirs.Add(slnDir);
                module.IncludeDirs.Add(slnDir + "external");
                module.IncludeDirs.Add(slnDir + targetProjName); // ToDo make this a loop to grab all the subfolders

                // Headers
                module.Headers.Add("utinni.h");
                module.Headers.Add("utility\\log.h");
                module.Headers.Add("plugin_framework\\plugin_manager.h");

                module.Headers.Add("swg\\appearance\\skeleton.h");
                module.Headers.Add("swg\\client\\client.h");
                module.Headers.Add("swg\\camera\\debug_camera.h");
                module.Headers.Add("swg\\game\\game.h");
                module.Headers.Add("swg\\graphics\\directx9.h");
                module.Headers.Add("swg\\graphics\\graphics.h");
                module.Headers.Add("swg\\misc\\config.h");
                module.Headers.Add("swg\\misc\\network.h");
                module.Headers.Add("swg\\misc\\swg_math.h");
                module.Headers.Add("swg\\object\\client_object.h");
                module.Headers.Add("swg\\object\\creature_object.h");
                module.Headers.Add("swg\\object\\player_object.h");
                module.Headers.Add("swg\\scene\\ground_scene.h");
                module.Headers.Add("swg\\scene\\terrain.h");
                module.Headers.Add("swg\\scene\\render_world.h");
                module.Headers.Add("swg\\scene\\world_snapshot.h");
                module.Headers.Add("swg\\ui\\cui_chat_window.h");
                module.Headers.Add("swg\\ui\\cui_hud.h");
                module.Headers.Add("swg\\ui\\cui_io.h");
                module.Headers.Add("swg\\ui\\cui_manager.h");
                module.Headers.Add("swg\\ui\\cui_misc.h");
                module.Headers.Add("swg\\ui\\imgui_impl.h");

                // Library
                module.LibraryDirs.Add(slnDir + "bin\\" + buildMode + "\\");
                module.Libraries.Add(targetProjName + ".dll");
            }

            public void SetupPasses(Driver driver)
            {
            }

            public void Preprocess(Driver driver, ASTContext ctx)
            {
                ctx.IgnoreHeadersWithName("spdlog");
                ctx.IgnoreHeadersWithName("detourxs");
                ctx.IgnoreHeadersWithName("ADE32");
            }

            public void Postprocess(Driver driver, ASTContext ctx)
            {
            }
        }

        /// <summary>
        /// C-15: Resolves the Utinni solution root directory using three resolution modes:
        /// 1. args[0] / $(SolutionDir) — explicit arg passed by the post-build event.
        /// 2. Walk-up from workingDir until a directory containing Utinni.sln is found.
        /// 3. UTINNI_SLN_DIR environment variable fallback.
        /// Throws InvalidOperationException if none of the modes succeed.
        /// </summary>
        public static string ResolveSlnDir(string workingDir, string[] args)
        {
            // Preference 1: explicit arg (e.g. $(SolutionDir) from post-build event)
            if (args != null && args.Length > 0 && Directory.Exists(args[0]))
            {
                return args[0].TrimEnd('\\', '/') + "\\";
            }

            // Preference 2: walk up from workingDir looking for Utinni.sln
            var dir = new DirectoryInfo(workingDir);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Utinni.sln")))
                {
                    return dir.FullName.TrimEnd('\\', '/') + "\\";
                }
                dir = dir.Parent;
            }

            // Preference 3: UTINNI_SLN_DIR environment variable
            string envDir = Environment.GetEnvironmentVariable("UTINNI_SLN_DIR");
            if (!string.IsNullOrEmpty(envDir) && Directory.Exists(envDir))
            {
                return envDir.TrimEnd('\\', '/') + "\\";
            }

            throw new InvalidOperationException(
                "Could not resolve Utinni solution directory. " +
                "Pass $(SolutionDir) as args[0], run from a build output directory inside " +
                "the solution tree, or set UTINNI_SLN_DIR.");
        }

        static void Main(string[] args)
        {
            ConsoleDriver.Run(new Gen(args));
            //Console.Read();
        }
    }
}
