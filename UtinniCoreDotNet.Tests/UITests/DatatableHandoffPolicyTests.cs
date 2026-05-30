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

using UtinniCoreDotNet.UI;
using Xunit;

namespace UtinniCoreDotNet.Tests.UITests
{
    /// <summary>
    /// D-10.2 (corrected) TRE Browser → Datatable Editor hand-off gating. Guards the fix for the
    /// extension-only visibility bug: compiled TRE datatables are <c>datatables/**/*.iff</c> (FORM DTII),
    /// not <c>.tab</c>, so the original <c>.tab</c>-only gate hid the menu item for every real datatable.
    /// Pure framework helper — no WinForms, no TheJawaToolboxDotNet reference.
    /// </summary>
    public class DatatableHandoffPolicyTests
    {
        [Theory]
        // .tab source form (anywhere) — always offered.
        [InlineData("foo.tab", false, true)]
        [InlineData("datatables/mob/creatures.tab", false, true)]
        // .iff UNDER a datatables/ path — the real compiled-table case the bug missed.
        [InlineData("datatables/mob/creatures.iff", false, true)]
        [InlineData("datatables/creatures.iff", false, true)]
        [InlineData("DATATABLES/Mob/Creatures.IFF", false, true)] // case-insensitive
        [InlineData("datatables\\mob\\creatures.iff", false, true)] // backslash separators
        // .iff NOT under datatables/ — not offered (it's a generic IFF; use Open in IFF Editor).
        [InlineData("appearance/mesh.iff", false, false)]
        [InlineData("creatures.iff", false, false)]
        // "datatables" must be a PATH SEGMENT, not a substring of another name.
        [InlineData("mydatatables/creatures.iff", false, false)]
        // enumerate-only (encrypted V5000/V6000) payloads can't be opened → never offered.
        [InlineData("datatables/mob/creatures.iff", true, false)]
        [InlineData("foo.tab", true, false)]
        // Non-datatable extensions, null/empty.
        [InlineData("readme.txt", false, false)]
        [InlineData("", false, false)]
        [InlineData(null, false, false)]
        public void ShouldOfferDatatableEditor_GatesOnDatatableCarrierAndResolvability(
            string logicalPath, bool enumerateOnly, bool expected)
        {
            Assert.Equal(expected, DatatableHandoffPolicy.ShouldOfferDatatableEditor(logicalPath, enumerateOnly));
        }

        [Theory]
        [InlineData("datatables/mob/x.iff", true)]
        [InlineData("a/datatables/x.iff", true)]
        [InlineData("Datatables/x.iff", true)]
        [InlineData("appearance/x.iff", false)]
        [InlineData("mydatatables/x.iff", false)]
        [InlineData("datatablesx/x.iff", false)]
        [InlineData(null, false)]
        public void IsUnderDatatablesPath_MatchesPathSegmentOnly(string logicalPath, bool expected)
        {
            Assert.Equal(expected, DatatableHandoffPolicy.IsUnderDatatablesPath(logicalPath));
        }

        [Fact(DisplayName = "IsDtiiPayload: a FORM DTII header is recognized as a datatable")]
        public void IsDtiiPayload_TrueForFormDtiiHeader()
        {
            // FORM | big-endian length | DTII  (bytes[0..4]=="FORM", bytes[8..12]=="DTII")
            byte[] payload = { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0, 0, 0, 8, (byte)'D', (byte)'T', (byte)'I', (byte)'I' };
            Assert.True(DatatableHandoffPolicy.IsDtiiPayload(payload));
        }

        [Fact(DisplayName = "IsDtiiPayload: a non-DTII FORM (e.g. another asset) is rejected")]
        public void IsDtiiPayload_FalseForNonDtiiForm()
        {
            byte[] payload = { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0, 0, 0, 8, (byte)'S', (byte)'M', (byte)'A', (byte)'T' };
            Assert.False(DatatableHandoffPolicy.IsDtiiPayload(payload));
        }

        [Fact(DisplayName = "IsDtiiPayload: a non-FORM top-level chunk is rejected")]
        public void IsDtiiPayload_FalseWhenNotForm()
        {
            byte[] payload = { (byte)'X', (byte)'X', (byte)'X', (byte)'X', 0, 0, 0, 8, (byte)'D', (byte)'T', (byte)'I', (byte)'I' };
            Assert.False(DatatableHandoffPolicy.IsDtiiPayload(payload));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(8)]
        [InlineData(11)]
        public void IsDtiiPayload_FalseForShortOrNullPayload(int length)
        {
            Assert.False(DatatableHandoffPolicy.IsDtiiPayload(new byte[length]));
            Assert.False(DatatableHandoffPolicy.IsDtiiPayload(null));
        }
    }
}
