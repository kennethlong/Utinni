using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace UtinniCoreDotNet.Utility
{
    // Phase 24 embed render-sizing (crew-consult design A, docs commit d9bb55b).
    //
    // On the ADVERTISED client the launcher appends '@utinni_embed.cfg' to the game's
    // post-'--' config args (CWD-relative: the engine's '@' parser is whitespace-delimited
    // with no quote support and the client dir may contain spaces). The engine parses that
    // file AFTER client.cfg, so its values override the pinned screenWidth/screenHeight and
    // the D3D9 backbuffer is created at the embed panel's size: present becomes 1:1, the
    // view is undistorted, and the gizmo maps 1:1 (RT == window). The game cannot have read
    // anything yet when this runs — its main thread is spin-parked at the patched PE entry
    // until Native.SignalLauncherReady(), which the caller invokes strictly AFTER this.
    //
    // borderlessWindow=true rides along for two verified reasons:
    //  - the engine's checkDisplayMode fails windowed mode when the requested size merely
    //    EQUALS the desktop on either axis and silently creates a FULLSCREEN-EXCLUSIVE
    //    device (fatal to the embed); that equality check is gated on !borderlessWindow.
    //  - AdjustWindowRect on a borderless (WS_POPUP) window is a no-op, so the client area
    //    is created at exactly the requested size (the reparent strips the frame anyway).
    //
    // Failure policy: NEVER block startup. Any invalid measurement or I/O failure deletes
    // the cfg and returns — a missing file makes the engine warn and fall back to
    // client.cfg (the pre-fix stretched view), which is degraded but fully functional.
    internal static class EmbedResolution
    {
        internal const string FileName = "utinni_embed.cfg";

        // The engine UI needs roughly 1024x768 minimum; below that, don't pin.
        private const int MinWidth = 1024;
        private const int MinHeight = 768;

        // Measures nothing itself — the caller passes the INNER PanelGame client size (the
        // exact rect RepositionSwgWindow stretches the game window into; FormMain.ClientSize
        // would over-measure by the tool panels + titlebar). Writes atomically (tmp + move)
        // so a crash mid-write can never leave a torn file for the engine's parser.
        internal static void WriteOrClear(Form form, System.Drawing.Size panelClientSize)
        {
            string clientDir;
            try
            {
                clientDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            }
            catch (Exception ex)
            {
                Log.Info("EmbedResolution: could not resolve the client directory; skipping embed cfg: " + ex.Message);
                return;
            }

            if (string.IsNullOrEmpty(clientDir) || !Directory.Exists(clientDir))
            {
                Log.Info("EmbedResolution: client directory unavailable; skipping embed cfg.");
                return;
            }

            string target = Path.Combine(clientDir, FileName);
            try
            {
                int width = panelClientSize.Width;
                int height = panelClientSize.Height;

                // Strictly smaller than the panel's monitor in both axes: larger-than-desktop
                // sizes trip the engine's windowed-mode rejection into exclusive fullscreen.
                System.Drawing.Rectangle desktop = Screen.FromControl(form).Bounds;
                bool valid = width >= MinWidth && height >= MinHeight
                    && width < desktop.Width && height < desktop.Height;
                if (!valid)
                {
                    Log.Info($"EmbedResolution: measured embed {width}x{height} (desktop {desktop.Width}x{desktop.Height}) failed validation; clearing embed cfg.");
                    DeleteIfExists(target);
                    return;
                }

                string tmp = target + ".tmp";
                File.WriteAllText(tmp,
                    "[ClientGraphics]\r\n" +
                    "\tscreenWidth=" + width + "\r\n" +
                    "\tscreenHeight=" + height + "\r\n" +
                    "\tborderlessWindow=true\r\n");
                DeleteIfExists(target);
                File.Move(tmp, target);
                Log.Info($"EmbedResolution: wrote {target} ({width}x{height}, borderless).");
            }
            catch (Exception ex)
            {
                Log.Info("EmbedResolution: write failed; clearing embed cfg: " + ex.Message);
                try
                {
                    DeleteIfExists(target);
                }
                catch
                {
                    // Best-effort cleanup only — startup must proceed regardless.
                }
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
