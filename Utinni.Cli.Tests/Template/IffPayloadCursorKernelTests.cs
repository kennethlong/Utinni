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
//
// Wave 1 (plan 23-03) Task 1 GREEN: the kernel read primitives the schema codec needs that the cursor
// did not yet expose (i16/u16/u32-as-int-free? no — u32 already; we add i16/u16/f64/fixed-char/pad).
// Every new primitive bounds-checks via Need(n) FIRST, so a read one byte past the payload end throws
// DecoderException(Truncated). The test method names embed the VALIDATION --filter token set
// "Template" + "Cursor" so `--filter "Template&Cursor"` selects exactly this battery.
//
// IffPayloadCursor is `internal sealed`; Utinni.Cli.Tests has InternalsVisibleTo, so these tests
// exercise the cursor directly.

using System.Text;
using UtinniCoreDotNet.Formats.Decoders;
using Xunit;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>
    /// Per-primitive decode + truncation goldens for the kernel read primitives added to
    /// <see cref="IffPayloadCursor"/> in Wave 1 (plan 23-03). Filter tokens live in the method names:
    /// Template, Cursor. Each test asserts (a) the value decodes correctly and (b) a read one byte past
    /// the end throws <see cref="DecoderException"/> with <see cref="DecoderError.Truncated"/>.
    /// </summary>
    public sealed class IffPayloadCursorKernelTests
    {
        // ── ReadInt16Le ──

        [Fact]
        [Trait("Category", "TemplateCursor")]
        public void Template_Cursor_ReadInt16Le_DecodesAndThrowsTruncatedPastEnd()
        {
            // -1000 LE = 0x18 0xFC
            var c = new IffPayloadCursor(new byte[] { 0x18, 0xFC });
            Assert.Equal(-1000, c.ReadInt16Le());
            Assert.Equal(0, c.Remaining);

            var oneByte = new IffPayloadCursor(new byte[] { 0x18 });
            DecoderException ex = Assert.Throws<DecoderException>(() => oneByte.ReadInt16Le());
            Assert.Equal(DecoderError.Truncated, ex.Kind);
        }

        // ── ReadUInt16Le ──

        [Fact]
        [Trait("Category", "TemplateCursor")]
        public void Template_Cursor_ReadUInt16Le_DecodesAndThrowsTruncatedPastEnd()
        {
            // 60000 LE = 0x60 0xEA
            var c = new IffPayloadCursor(new byte[] { 0x60, 0xEA });
            Assert.Equal(60000, c.ReadUInt16Le());
            Assert.Equal(0, c.Remaining);

            var oneByte = new IffPayloadCursor(new byte[] { 0x60 });
            DecoderException ex = Assert.Throws<DecoderException>(() => oneByte.ReadUInt16Le());
            Assert.Equal(DecoderError.Truncated, ex.Kind);
        }

        // ── ReadUInt32Le (existing — confirm it stays overflow-safe as long) ──

        [Fact]
        [Trait("Category", "TemplateCursor")]
        public void Template_Cursor_ReadUInt32Le_DecodesHighValueAsLong()
        {
            // 4000000000 LE
            var c = new IffPayloadCursor(new byte[] { 0x00, 0x28, 0x6B, 0xEE });
            Assert.Equal(4000000000L, c.ReadUInt32Le());
        }

        // ── ReadDoubleLe ──

        [Fact]
        [Trait("Category", "TemplateCursor")]
        public void Template_Cursor_ReadDoubleLe_DecodesAndThrowsTruncatedPastEnd()
        {
            byte[] eight = System.BitConverter.GetBytes(2.25); // host is LE
            var c = new IffPayloadCursor(eight);
            Assert.Equal(2.25, c.ReadDoubleLe());
            Assert.Equal(0, c.Remaining);

            var seven = new IffPayloadCursor(new byte[7]);
            DecoderException ex = Assert.Throws<DecoderException>(() => seven.ReadDoubleLe());
            Assert.Equal(DecoderError.Truncated, ex.Kind);
        }

        // ── ReadFixedChar(n) — display trims at first NUL, but consumes the full n bytes ──

        [Fact]
        [Trait("Category", "TemplateCursor")]
        public void Template_Cursor_ReadFixedChar_TrimsAtNulButConsumesFullWidth()
        {
            // "CHAR8" right-padded to 8 with NUL: 43 48 41 52 38 00 00 00
            var c = new IffPayloadCursor(new byte[] { 0x43, 0x48, 0x41, 0x52, 0x38, 0x00, 0x00, 0x00 });
            Assert.Equal("CHAR8", c.ReadFixedChar(8, Encoding.ASCII));
            Assert.Equal(0, c.Remaining); // consumed all 8

            var sevenWide = new IffPayloadCursor(new byte[7]);
            DecoderException ex = Assert.Throws<DecoderException>(() => sevenWide.ReadFixedChar(8, Encoding.ASCII));
            Assert.Equal(DecoderError.Truncated, ex.Kind);
        }

        // ── SkipPad(n) / ReadRawBytes(n) ──

        [Fact]
        [Trait("Category", "TemplateCursor")]
        public void Template_Cursor_SkipPad_ConsumesPadAndThrowsTruncatedPastEnd()
        {
            var c = new IffPayloadCursor(new byte[] { 0x00, 0x00 });
            c.SkipPad(2);
            Assert.Equal(0, c.Remaining);

            var oneByte = new IffPayloadCursor(new byte[] { 0x00 });
            DecoderException ex = Assert.Throws<DecoderException>(() => oneByte.SkipPad(2));
            Assert.Equal(DecoderError.Truncated, ex.Kind);
        }

        [Fact]
        [Trait("Category", "TemplateCursor")]
        public void Template_Cursor_ReadRawBytes_ReturnsExactSpanAndThrowsTruncatedPastEnd()
        {
            var c = new IffPayloadCursor(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
            byte[] raw = c.ReadRawBytes(4);
            Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, raw);
            Assert.Equal(0, c.Remaining);

            var three = new IffPayloadCursor(new byte[] { 0xDE, 0xAD, 0xBE });
            DecoderException ex = Assert.Throws<DecoderException>(() => three.ReadRawBytes(4));
            Assert.Equal(DecoderError.Truncated, ex.Kind);
        }
    }
}
