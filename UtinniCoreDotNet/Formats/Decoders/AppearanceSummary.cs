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
// Mesh / skeleton / animation STRUCTURAL-COUNT layout understood by reading the swg-client-v2
// tools/swg_blender Python readers (mesh_static.py, mesh_skeletal.py, skeleton.py, animation.py —
// SOE/Bootprint-derived community tooling). Only the on-disk chunk layout (the FORM nesting and the
// INFO/CNT/NAME count fields) was studied to extract counts — no code, comments, identifier names,
// or test fixtures copied from any reference source. Implementation original to Utinni under MIT.

using System.Collections.Generic;
using System.Text;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Decoders
{
    /// <summary>
    /// A bounded structural-count summary of a mesh / skeletal-mesh / skeleton / animation asset.
    /// Phase-7 read-only overview — counts and joint names only, NOT a geometry decode.
    /// Unused fields are zero/empty for the asset's <see cref="Kind"/>.
    /// </summary>
    public sealed class AppearanceInfo
    {
        public string Kind { get; }              // "mesh" | "skeletal-mesh" | "skeleton" | "animation"
        public int VertexCount { get; }
        public int ShaderCount { get; }
        public int JointCount { get; }
        public IReadOnlyList<string> JointNames { get; }
        public int FrameCount { get; }

        public AppearanceInfo(string kind, int vertexCount, int shaderCount, int jointCount,
            IReadOnlyList<string> jointNames, int frameCount)
        {
            Kind = kind;
            VertexCount = vertexCount;
            ShaderCount = shaderCount;
            JointCount = jointCount;
            JointNames = jointNames ?? new string[0];
            FrameCount = frameCount;
        }
    }

    /// <summary>
    /// Summarizes mesh/skeleton/animation IFF assets into structural counts (vertex/shader/joint/
    /// frame + joint names), ported from the swg_blender readers' chunk layout. Pure — NO JSON,
    /// console, or file writes. Returns <c>null</c> for an unrecognized root (unknown types are
    /// normal; the caller hides the structured view). Counts are bounds-checked before any
    /// allocation (forged counts throw <see cref="DecoderException"/>, never OutOfMemoryException).
    /// </summary>
    public static class AppearanceSummary
    {
        public static AppearanceInfo Summarize(IffDocument doc)
        {
            var root = doc?.Root as IffContainerChunk;
            if (root == null) return null;

            switch (root.SubTypeId)
            {
                case "MESH": return SummarizeStaticMesh(root);
                case "SKMG": return SummarizeSkeletalMesh(root);
                case "SKTM": return SummarizeSkeleton(root);
                case "KFAT":
                case "CKAT": return SummarizeAnimation(root);
                default: return null;
            }
        }

        // MESH: shader count from the SPS "CNT" chunk; vertex count summed from each VTXA's
        // 0003/INFO chunk (2nd int32). Best-effort over the nested version FORMs.
        private static AppearanceInfo SummarizeStaticMesh(IffContainerChunk root)
        {
            int shaderCount = 0;
            var cnt = FindFirstLeaf(root, "CNT ");
            if (cnt == null) cnt = FindFirstLeaf(root, "CNT");
            if (cnt != null && cnt.Data.Length >= 4)
            {
                shaderCount = new IffPayloadCursor(cnt.Data).ReadInt32Le();
                if (shaderCount < 0) shaderCount = 0;
            }

            int vertexCount = 0;
            SumVtxaVertices(root, ref vertexCount);

            return new AppearanceInfo("mesh", vertexCount, shaderCount, 0, null, 0);
        }

        // SKMG INFO chunk (little-endian int32 fields): position(vertex) count at offset 16,
        // per-shader-data(shader) count at offset 28.
        private static AppearanceInfo SummarizeSkeletalMesh(IffContainerChunk root)
        {
            int vertexCount = 0, shaderCount = 0;
            var info = FindFirstLeaf(root, "INFO");
            if (info != null && info.Data.Length >= 32)
            {
                var c = new IffPayloadCursor(info.Data);
                Skip(c, 16); vertexCount = NonNegative(c.ReadInt32Le());
                Skip(c, 8);  // advance from offset 20 to 28
                shaderCount = NonNegative(c.ReadInt32Le());
            }
            return new AppearanceInfo("skeletal-mesh", vertexCount, shaderCount, 0, null, 0);
        }

        // SKTM: INFO chunk = joint count (int32); NAME chunk = jointCount NUL-terminated names.
        private static AppearanceInfo SummarizeSkeleton(IffContainerChunk root)
        {
            int jointCount = 0;
            var info = FindFirstLeaf(root, "INFO");
            if (info != null && info.Data.Length >= 4)
            {
                jointCount = NonNegative(new IffPayloadCursor(info.Data).ReadInt32Le());
            }

            var names = new List<string>();
            var nameChunk = FindFirstLeaf(root, "NAME");
            if (nameChunk != null && jointCount > 0)
            {
                var cursor = new IffPayloadCursor(nameChunk.Data);
                // Forged-count guard: each name needs at least 1 byte (its NUL terminator).
                if (jointCount > nameChunk.Data.Length)
                {
                    throw new DecoderException(DecoderError.CountExceedsCap,
                        "Skeleton joint count " + jointCount + " exceeds the " + nameChunk.Data.Length
                        + "-byte NAME chunk.");
                }
                for (int i = 0; i < jointCount && cursor.Remaining > 0; i++)
                {
                    names.Add(cursor.ReadCString(Encoding.ASCII));
                }
            }

            return new AppearanceInfo("skeleton", 0, 0, jointCount, names, 0);
        }

        // KFAT/CKAT INFO chunk: fps (float32) then frame count (int32) at offset 4.
        private static AppearanceInfo SummarizeAnimation(IffContainerChunk root)
        {
            int frameCount = 0;
            var info = FindFirstLeaf(root, "INFO");
            if (info != null && info.Data.Length >= 8)
            {
                var c = new IffPayloadCursor(info.Data);
                Skip(c, 4); // skip the fps float
                frameCount = NonNegative(c.ReadInt32Le());
            }
            return new AppearanceInfo("animation", 0, 0, 0, null, frameCount);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static void SumVtxaVertices(IffContainerChunk container, ref int total)
        {
            foreach (var child in container.Children)
            {
                if (child is IffContainerChunk c)
                {
                    if (c.SubTypeId == "VTXA")
                    {
                        var info = FindFirstLeaf(c, "INFO");
                        if (info != null && info.Data.Length >= 8)
                        {
                            var cur = new IffPayloadCursor(info.Data);
                            Skip(cur, 4); // flags
                            int v = cur.ReadInt32Le();
                            if (v > 0) total += v;
                        }
                    }
                    else
                    {
                        SumVtxaVertices(c, ref total);
                    }
                }
            }
        }

        // Depth-first search for the first leaf with the given TypeId anywhere under the container.
        private static IffLeafChunk FindFirstLeaf(IffContainerChunk container, string typeId)
        {
            foreach (var child in container.Children)
            {
                if (child is IffLeafChunk leaf && leaf.TypeId == typeId) return leaf;
            }
            foreach (var child in container.Children)
            {
                if (child is IffContainerChunk c)
                {
                    var found = FindFirstLeaf(c, typeId);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static void Skip(IffPayloadCursor cursor, int bytes) => cursor.ReadBytes(bytes);

        private static int NonNegative(int v) => v < 0 ? 0 : v;
    }
}
