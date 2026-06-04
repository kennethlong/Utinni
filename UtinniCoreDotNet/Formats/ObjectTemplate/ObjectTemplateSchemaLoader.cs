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
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace UtinniCoreDotNet.Formats.ObjectTemplate
{
    /// <summary>
    /// Plan 13-06 (RESID-01, D-08): loads the committed per-class param→type schema (the
    /// <c>object-template-common.schema.json</c> embedded resource — the common slot/attribute/hair
    /// struct layouts derived from the generated <c>Shared*ObjectTemplate</c> classes) once and caches
    /// it. ZERO native-tool dependency on the editor open path.
    ///
    /// <para><b>Open-path safety (T-13-16):</b> an absent or malformed schema degrades to an EMPTY
    /// schema (every Classify returns no-match) — it NEVER throws on the editor open path, so a bad
    /// schema simply falls back to the existing hex display rather than crashing the editor.</para>
    /// </summary>
    public static class ObjectTemplateSchemaLoader
    {
        private const string EmbeddedResourceName =
            "UtinniCoreDotNet.Formats.ObjectTemplate.object-template-common.schema.json";

        private static readonly object Gate = new object();
        private static ObjectTemplateSchema _cached;

        /// <summary>Loads + caches the embedded common-class schema. Never throws.</summary>
        public static ObjectTemplateSchema LoadCommon()
        {
            if (_cached != null) return _cached;
            lock (Gate)
            {
                if (_cached != null) return _cached;
                _cached = LoadFromJson(ReadEmbedded());
                return _cached;
            }
        }

        /// <summary>Test seam: drop the cache so a subsequent <see cref="LoadCommon"/> re-reads.</summary>
        public static void ResetCacheForTesting()
        {
            lock (Gate) { _cached = null; }
        }

        /// <summary>
        /// Parses a schema JSON document into an <see cref="ObjectTemplateSchema"/>. A null/empty/
        /// malformed document, or an unrecognized type/list-type token, degrades to a partial or empty
        /// schema WITHOUT throwing (open-path safety).
        /// </summary>
        public static ObjectTemplateSchema LoadFromJson(string json)
        {
            var classes = new Dictionary<string, Dictionary<string, ObjectTemplateParamSchema>>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(json)) return new ObjectTemplateSchema(classes);

            object parsed;
            try { parsed = new JavaScriptSerializer().DeserializeObject(json); }
            catch (Exception) { return new ObjectTemplateSchema(classes); }

            var root = parsed as Dictionary<string, object>;
            if (root == null) return new ObjectTemplateSchema(classes);

            var classesObj = Get(root, "classes") as Dictionary<string, object>;
            if (classesObj == null) return new ObjectTemplateSchema(classes);

            foreach (KeyValuePair<string, object> classEntry in classesObj)
            {
                var paramMap = new Dictionary<string, ObjectTemplateParamSchema>(StringComparer.Ordinal);
                var classObj = classEntry.Value as Dictionary<string, object>;
                var paramsArr = classObj != null ? Get(classObj, "params") as object[] : null;
                if (paramsArr != null)
                {
                    foreach (object pt in paramsArr)
                    {
                        var po = pt as Dictionary<string, object>;
                        if (po == null) continue;
                        string name = Get(po, "name") as string;
                        if (string.IsNullOrEmpty(name)) continue;

                        ObjectTemplateParamType type = ParseType(Get(po, "type") as string);
                        ObjectTemplateListType listType = ParseListType(Get(po, "listType") as string);
                        paramMap[name] = new ObjectTemplateParamSchema(name, type, listType);
                    }
                }
                classes[classEntry.Key] = paramMap;
            }

            return new ObjectTemplateSchema(classes);
        }

        private static object Get(Dictionary<string, object> map, string key)
        {
            object v;
            return map.TryGetValue(key, out v) ? v : null;
        }

        private static ObjectTemplateParamType ParseType(string token)
        {
            ObjectTemplateParamType t;
            return Enum.TryParse(token, out t) ? t : ObjectTemplateParamType.TYPE_NONE;
        }

        private static ObjectTemplateListType ParseListType(string token)
        {
            ObjectTemplateListType t;
            return Enum.TryParse(token, out t) ? t : ObjectTemplateListType.LIST_NONE;
        }

        private static string ReadEmbedded()
        {
            try
            {
                Assembly asm = typeof(ObjectTemplateSchemaLoader).Assembly;
                using (Stream s = asm.GetManifestResourceStream(EmbeddedResourceName))
                {
                    if (s == null) return null;
                    using (var reader = new StreamReader(s))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception)
            {
                return null; // open-path safety: a missing/unreadable resource → empty schema
            }
        }
    }
}
