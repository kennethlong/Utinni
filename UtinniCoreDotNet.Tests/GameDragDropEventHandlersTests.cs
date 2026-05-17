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
using System.Reflection;
using System.Windows.Forms;
using UtinniCoreDotNet.UI;
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    /// <summary>
    /// C-05 regression tests: GameDragDropEventHandlers must use static events + forwarder
    /// lambdas so that handlers subscribed AFTER Initialize() still receive events.
    /// Tests use a TestPanel (Panel subclass) to avoid PanelGame's P/Invoke ctor; they call
    /// GameDragDropEventHandlers.Initialize(panel) directly, mirroring the production path.
    /// </summary>
    public class GameDragDropEventHandlersTests : IDisposable
    {
        // Minimal Panel subclass that can be passed to Initialize without the PanelGame
        // P/Invoke dependencies (WndProc calls into SWG, etc.)
        private class TestPanel : Panel
        {
            public TestPanel()
            {
                AllowDrop = true;
            }

            // Expose protected raise methods for testing
            public void RaiseDragDrop(DragEventArgs e) => OnDragDrop(e);
            public void RaiseDragEnter(DragEventArgs e) => OnDragEnter(e);
            public void RaiseDragLeave(EventArgs e) => OnDragLeave(e);
            public void RaiseDragOver(DragEventArgs e) => OnDragOver(e);
        }

        private static DragEventArgs MakeDragEventArgs()
        {
            // IDataObject may be null for testing purposes — the forwarder lambda
            // only invokes the subscriber; it doesn't consume the data.
            return new DragEventArgs(null, 0, 0, 0, DragDropEffects.None, DragDropEffects.None);
        }

        /// <summary>
        /// Reset static event subscribers between tests to avoid cross-test pollution.
        /// </summary>
        private void ClearSubscribers()
        {
            // Reset each static event by setting the backing field via reflection.
            // This is necessary because static events persist across tests in the same process.
            foreach (var fieldName in new[] { "OnDragDrop", "OnDragEnter", "OnDragLeave", "OnDragOver" })
            {
                var field = typeof(GameDragDropEventHandlers)
                    .GetField(fieldName,
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(null, null);
                }
            }
        }

        public void Dispose() => ClearSubscribers();

        [Fact]
        public void OnDragDrop_SubscriberAfterInitialize_ReceivesEvent()
        {
            // Arrange
            ClearSubscribers();
            using (var panel = new TestPanel())
            {
                GameDragDropEventHandlers.Initialize(panel);

                bool fired = false;
                // Subscribe AFTER Initialize — this is the C-05 bug scenario
                GameDragDropEventHandlers.OnDragDrop += (s, e) => fired = true;

                // Act
                panel.RaiseDragDrop(MakeDragEventArgs());

                // Assert
                Assert.True(fired, "Subscriber added after Initialize() should receive DragDrop event.");
            }
        }

        [Fact]
        public void OnDragEnter_SubscriberAfterInitialize_ReceivesEvent()
        {
            // Arrange
            ClearSubscribers();
            using (var panel = new TestPanel())
            {
                GameDragDropEventHandlers.Initialize(panel);

                bool fired = false;
                GameDragDropEventHandlers.OnDragEnter += (s, e) => fired = true;

                // Act
                panel.RaiseDragEnter(MakeDragEventArgs());

                // Assert
                Assert.True(fired, "Subscriber added after Initialize() should receive DragEnter event.");
            }
        }

        [Fact]
        public void OnDragDrop_MultipleSubscribers_AllReceive()
        {
            // Arrange
            ClearSubscribers();
            using (var panel = new TestPanel())
            {
                GameDragDropEventHandlers.Initialize(panel);

                int count = 0;
                GameDragDropEventHandlers.OnDragDrop += (s, e) => count++;
                GameDragDropEventHandlers.OnDragDrop += (s, e) => count++;

                // Act
                panel.RaiseDragDrop(MakeDragEventArgs());

                // Assert
                Assert.Equal(2, count);
            }
        }

        [Fact]
        public void OnDragDrop_NoSubscribers_DoesNotCrash()
        {
            // Arrange — no subscribers; the ?.Invoke null-conditional in the forwarder
            // must prevent a NullReferenceException
            ClearSubscribers();
            using (var panel = new TestPanel())
            {
                GameDragDropEventHandlers.Initialize(panel);
                // Deliberately no subscribers

                // Act & Assert — must not throw
                Exception ex = Record.Exception(() => panel.RaiseDragDrop(MakeDragEventArgs()));
                Assert.Null(ex);
            }
        }
    }
}
