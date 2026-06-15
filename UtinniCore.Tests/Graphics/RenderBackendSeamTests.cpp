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

// ============================================================================
// Phase 18 / RNDR-01 / D-07: IRenderBackend mock-dispatch seam test.
//
// Proves that all 10 IRenderBackend pure-virtual members route through the
// installed backend vtable WITHOUT a live D3D9 device (CI cannot run D3D9), and
// that render_backend::get() is null after the post-test restore (Codex
// leaked-global-state check, review amendment 2).
// ============================================================================

#include <catch2/catch_all.hpp>
#include "swg/graphics/render_backend.h"

namespace
{
// A no-op mock that counts every member call. Installing this via
// render_backend::set() lets the dispatch contract be asserted with no device.
struct MockBackend final : render_backend::IRenderBackend
{
    int newFrameCalls = 0;
    int renderDrawCalls = 0;
    int preResizeCalls = 0;
    int postResizeCalls = 0;
    int rtwCalls = 0;
    int rthCalls = 0;
    int depthCalls = 0;
    int colorCalls = 0;
    int stageGetCalls = 0;
    int stageSetCalls = 0;
    int lastStageSet = -1;

    void newFrame() override { ++newFrameCalls; }
    void renderDrawData(ImDrawData*) override { ++renderDrawCalls; }
    void onPreResize() override { ++preResizeCalls; }
    void onPostResize() override { ++postResizeCalls; }
    int renderTargetWidth() override { ++rtwCalls; return 1920; }
    int renderTargetHeight() override { ++rthCalls; return 1080; }
    ImTextureID sceneDepthTexture() override { ++depthCalls; return (ImTextureID)0; }
    ImTextureID sceneColorTexture() override { ++colorCalls; return (ImTextureID)0; }
    int sceneDepthStage() override { ++stageGetCalls; return 7; }
    void setSceneDepthStage(int stage) override { ++stageSetCalls; lastStageSet = stage; }
};
} // namespace

TEST_CASE("D-07 seam routes all 10 members through the installed backend", "[rndr01][graphics]")
{
    MockBackend mock;
    render_backend::set(&mock);
    REQUIRE(render_backend::get() == &mock);

    // Drive every one of the 10 members through the installed vtable.
    render_backend::get()->newFrame();
    render_backend::get()->renderDrawData(nullptr);
    render_backend::get()->onPreResize();
    render_backend::get()->onPostResize();
    REQUIRE(render_backend::get()->renderTargetWidth() == 1920);
    REQUIRE(render_backend::get()->renderTargetHeight() == 1080);
    render_backend::get()->sceneDepthTexture();
    render_backend::get()->sceneColorTexture();
    REQUIRE(render_backend::get()->sceneDepthStage() == 7);
    render_backend::get()->setSceneDepthStage(3);

    // Each member dispatched exactly once.
    REQUIRE(mock.newFrameCalls == 1);
    REQUIRE(mock.renderDrawCalls == 1);
    REQUIRE(mock.preResizeCalls == 1);
    REQUIRE(mock.postResizeCalls == 1);
    REQUIRE(mock.rtwCalls == 1);
    REQUIRE(mock.rthCalls == 1);
    REQUIRE(mock.depthCalls == 1);
    REQUIRE(mock.colorCalls == 1);
    REQUIRE(mock.stageGetCalls == 1);
    REQUIRE(mock.stageSetCalls == 1);
    REQUIRE(mock.lastStageSet == 3);

    // Restore + leaked-global-state check (review amendment 2): nothing must
    // remain installed for the next test in the suite.
    render_backend::set(nullptr);
    REQUIRE(render_backend::get() == nullptr);
}
