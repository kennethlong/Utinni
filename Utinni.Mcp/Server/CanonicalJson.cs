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
using System.Globalization;
using System.Text;

namespace Utinni.Mcp.Server;

/// <summary>
/// The net10 RE-IMPLEMENTATION of the Utinni canonical JSON writer + reader for the live-bridge wire
/// (16-03 Task 1; review R3-1, the HIGH fix's net10 half).
///
/// <para><b>Source-of-truth contract (R3-1):</b> <c>UtinniCoreDotNet/Live/CanonicalJson.cs</c> (net472,
/// 16-02) is the documented canonical writer/reader. The net10 <c>Utinni.Mcp</c> host CANNOT
/// project-reference that net472 assembly (the TFM wall), so this is a SEPARATE implementation written
/// field-for-field against the SAME documented contract. The committed golden wire byte-vectors
/// (<c>Utinni.Cli.Tests/Fixtures/live/wire-*.json</c>) are the cross-TFM byte-exact proof: this side
/// asserts byte-exact produce+consume against them and the net472 side asserts the SAME bytes
/// (LivePipeProtocolTests on this side; LiveBridgeProtocolTests on the net472 side).</para>
///
/// <para><b>Determinism contract (enforced by the explicit emission code here, NOT by
/// <c>System.Text.Json</c>'s <c>JsonNamingPolicy</c>/<c>JsonStringEnumConverter</c> reflection order):</b>
/// <list type="bullet">
///   <item><b>FIXED, DECLARED key order</b> per message — the caller (<see cref="LivePipeClient"/>)
///         appends fields in a fixed source order via <see cref="JsonObjectWriter"/>.</item>
///   <item><b>camelCase keys</b> — the writer emits the literal camelCase key strings passed in.</item>
///   <item><b>NO insignificant whitespace</b> — no spaces/newlines between tokens.</item>
///   <item><b>String tier wire encoding</b> — the tier is a plain string on the wire (the server emits
///         <c>ReloadTier.ToString()</c>); the net10 side never references the net472 enum, it reads the
///         string verbatim (CUR-NEW-9).</item>
///   <item><b>Consistent null handling</b> — a null string emits the JSON literal <c>null</c>.</item>
///   <item><b>Invariant-culture numbers</b> — integers via <see cref="CultureInfo.InvariantCulture"/>;
///         bools as <c>true</c>/<c>false</c>.</item>
///   <item><b>JSON-spec string escaping</b> — quote, backslash, and C0 control chars escaped
///         deterministically.</item>
/// </list></para>
///
/// <para><b>System.Text.Json MAY be used for PARSING</b> inbound acks if convenient, but the PRODUCED
/// request bytes come from THIS hand-rolled writer so they are deterministic. (This file uses its own
/// tolerant tokenizer for parsing too, mirroring the net472 reader, to keep the contract symmetric.)</para>
/// </summary>
public static class CanonicalJson
{
    /// <summary>
    /// A tiny fixed-order JSON object builder. Each <c>Add*</c> appends one field in call order;
    /// determinism comes from the caller invoking the adders in a FIXED source order. Never reorders,
    /// never reflects, never sorts. Field-for-field mirror of the net472 JsonObjectWriter.
    /// </summary>
    public sealed class JsonObjectWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private bool _hasField;

        /// <summary>Opens the object.</summary>
        public JsonObjectWriter()
        {
            _sb.Append('{');
        }

        private void Separator()
        {
            if (_hasField)
            {
                _sb.Append(',');
            }
            _hasField = true;
        }

        private void Key(string camelCaseKey)
        {
            Separator();
            WriteJsonString(_sb, camelCaseKey);
            _sb.Append(':');
        }

        /// <summary>Adds a string field. A null value emits the JSON literal <c>null</c>.</summary>
        public JsonObjectWriter AddString(string camelCaseKey, string? value)
        {
            Key(camelCaseKey);
            if (value == null)
            {
                _sb.Append("null");
            }
            else
            {
                WriteJsonString(_sb, value);
            }
            return this;
        }

        /// <summary>Adds a non-nullable integer field (invariant culture).</summary>
        public JsonObjectWriter AddInt(string camelCaseKey, int value)
        {
            Key(camelCaseKey);
            _sb.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        /// <summary>Adds a boolean field (<c>true</c>/<c>false</c>).</summary>
        public JsonObjectWriter AddBool(string camelCaseKey, bool value)
        {
            Key(camelCaseKey);
            _sb.Append(value ? "true" : "false");
            return this;
        }

        /// <summary>Adds a nested object field, already serialized to its canonical body string.</summary>
        public JsonObjectWriter AddObject(string camelCaseKey, JsonObjectWriter nested)
        {
            Key(camelCaseKey);
            _sb.Append(nested.Body());
            return this;
        }

        /// <summary>Returns the canonical object body (closing brace included) WITHOUT mutating state.</summary>
        public string Body()
        {
            return _sb.ToString() + "}";
        }

        /// <summary>Serializes to canonical UTF-8 bytes (no BOM).</summary>
        public byte[] ToUtf8Bytes()
        {
            return new UTF8Encoding(false).GetBytes(Body());
        }
    }

    /// <summary>
    /// Serializes a <see cref="JsonObjectWriter"/> to canonical UTF-8 bytes (no BOM). The single
    /// OUTPUT chokepoint — every live-wire body the client emits goes through here (R3-1).
    /// </summary>
    public static byte[] Serialize(JsonObjectWriter writer)
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));
        return writer.ToUtf8Bytes();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Reader — a minimal tolerant tokenizer that parses canonical bytes back
    // into a key→value map. Field-for-field mirror of the net472 reader.
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>A parsed JSON value: string, long, bool, null, or nested object.</summary>
    public sealed class JsonValue
    {
        public string? StringValue;
        public long NumberValue;
        public bool BoolValue;
        public bool IsNull;
        public Dictionary<string, JsonValue>? Object;
        public JsonKind Kind;
    }

    public enum JsonKind { String, Number, Bool, Null, Object }

    /// <summary>
    /// Parses canonical UTF-8 JSON bytes into a key→<see cref="JsonValue"/> map. Tolerant enough to
    /// consume the committed goldens; throws <see cref="FormatException"/> on a structurally invalid
    /// body.
    /// </summary>
    public static Dictionary<string, JsonValue> Deserialize(byte[] utf8)
    {
        if (utf8 == null) throw new ArgumentNullException(nameof(utf8));
        string text = new UTF8Encoding(false).GetString(utf8);
        int pos = 0;
        Dictionary<string, JsonValue> obj = ParseObject(text, ref pos);
        SkipWhitespace(text, ref pos);
        if (pos != text.Length)
        {
            throw new FormatException("Trailing content after top-level JSON object.");
        }
        return obj;
    }

    private static Dictionary<string, JsonValue> ParseObject(string s, ref int pos)
    {
        SkipWhitespace(s, ref pos);
        Expect(s, ref pos, '{');
        var map = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
        SkipWhitespace(s, ref pos);
        if (Peek(s, pos) == '}')
        {
            pos++;
            return map;
        }
        while (true)
        {
            SkipWhitespace(s, ref pos);
            string key = ParseString(s, ref pos);
            SkipWhitespace(s, ref pos);
            Expect(s, ref pos, ':');
            JsonValue value = ParseValue(s, ref pos);
            map[key] = value;
            SkipWhitespace(s, ref pos);
            char c = Peek(s, pos);
            if (c == ',')
            {
                pos++;
                continue;
            }
            if (c == '}')
            {
                pos++;
                break;
            }
            throw new FormatException("Expected ',' or '}' in object at position " + pos + ".");
        }
        return map;
    }

    private static JsonValue ParseValue(string s, ref int pos)
    {
        SkipWhitespace(s, ref pos);
        char c = Peek(s, pos);
        if (c == '"')
        {
            return new JsonValue { Kind = JsonKind.String, StringValue = ParseString(s, ref pos) };
        }
        if (c == '{')
        {
            return new JsonValue { Kind = JsonKind.Object, Object = ParseObject(s, ref pos) };
        }
        if (c == 't' || c == 'f')
        {
            bool b = ParseLiteralBool(s, ref pos);
            return new JsonValue { Kind = JsonKind.Bool, BoolValue = b };
        }
        if (c == 'n')
        {
            ParseLiteral(s, ref pos, "null");
            return new JsonValue { Kind = JsonKind.Null, IsNull = true };
        }
        if (c == '-' || (c >= '0' && c <= '9'))
        {
            return new JsonValue { Kind = JsonKind.Number, NumberValue = ParseNumber(s, ref pos) };
        }
        throw new FormatException("Unexpected token '" + c + "' at position " + pos + ".");
    }

    private static string ParseString(string s, ref int pos)
    {
        Expect(s, ref pos, '"');
        var sb = new StringBuilder();
        while (true)
        {
            if (pos >= s.Length) throw new FormatException("Unterminated string.");
            char c = s[pos++];
            if (c == '"')
            {
                return sb.ToString();
            }
            if (c == '\\')
            {
                if (pos >= s.Length) throw new FormatException("Unterminated escape.");
                char e = s[pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (pos + 4 > s.Length) throw new FormatException("Truncated \\u escape.");
                        string hex = s.Substring(pos, 4);
                        pos += 4;
                        sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        break;
                    default:
                        throw new FormatException("Invalid escape '\\" + e + "'.");
                }
            }
            else
            {
                sb.Append(c);
            }
        }
    }

    private static long ParseNumber(string s, ref int pos)
    {
        int start = pos;
        if (Peek(s, pos) == '-') pos++;
        while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9') pos++;
        string num = s.Substring(start, pos - start);
        return long.Parse(num, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static bool ParseLiteralBool(string s, ref int pos)
    {
        if (Peek(s, pos) == 't') { ParseLiteral(s, ref pos, "true"); return true; }
        ParseLiteral(s, ref pos, "false");
        return false;
    }

    private static void ParseLiteral(string s, ref int pos, string literal)
    {
        if (pos + literal.Length > s.Length || s.Substring(pos, literal.Length) != literal)
        {
            throw new FormatException("Expected literal '" + literal + "' at position " + pos + ".");
        }
        pos += literal.Length;
    }

    private static void SkipWhitespace(string s, ref int pos)
    {
        while (pos < s.Length)
        {
            char c = s[pos];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n') pos++;
            else break;
        }
    }

    private static char Peek(string s, int pos)
    {
        if (pos >= s.Length) throw new FormatException("Unexpected end of JSON.");
        return s[pos];
    }

    private static void Expect(string s, ref int pos, char c)
    {
        if (Peek(s, pos) != c)
        {
            throw new FormatException("Expected '" + c + "' at position " + pos + ".");
        }
        pos++;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Shared string escaper — used by the writer; deterministic per JSON spec.
    // ─────────────────────────────────────────────────────────────────────

    private static void WriteJsonString(StringBuilder sb, string value)
    {
        sb.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        sb.Append('"');
    }
}
