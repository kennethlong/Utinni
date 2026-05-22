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

#include "log.h"
#include "spdlog/spdlog.h"
#include <spdlog/sinks/basic_file_sink.h>

#include <mutex>
#include <vector>

static std::vector<std::string> logMessageBuffer;
// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registry
// backed by insertion-order std::vector<{handle, fn_ptr}>.
//
// CR-01 + WR-07 (03-REVIEW): the registry is touched from two distinct sites
// without coordination -- (a) the dispatch site inside OutputSink::sink_it_
// which spdlog calls while holding ITS sink mutex (synchronizes against other
// sink_it_ invocations only); and (b) subscribe/unsubscribe, which are called
// from arbitrary threads (e.g. WinForms UI thread via the managed Log.cs
// callback registration). Piggybacking on spdlog's sink mutex partially
// protects the iteration but does NOT serialize subscribe/unsubscribe; the
// next sink_it_ then races the in-progress map mutation. Fix: explicit
// outputSinkMutex acquired by subscribe / unsubscribe AND the sink_it_
// snapshot-build. Mirrors the per-registry mutex pattern across the rest of
// the native callback layer.
//
// 2026-05-22 follow-up to ground_scene fix (commit 7201700): switched from
// std::unordered_map to insertion-order vector with stack-allocated fixed-size
// snapshot in dispatch sites. See [[project-rh-snapshot-no-heap-alloc]] memory.
namespace
{
template <typename Fn>
struct CallbackEntry
{
    int handle;
    Fn func;
};

template <typename Fn, typename Invoke>
void dispatchSnapshot(
    const std::vector<CallbackEntry<Fn>>& registry,
    std::mutex& mutex,
    Invoke&& invoke)
{
    constexpr size_t kInlineCap = 16;
    Fn stackSnap[kInlineCap];
    Fn* snapshot = stackSnap;
    std::vector<Fn> heapSnap;
    size_t count = 0;
    {
        std::lock_guard<std::mutex> guard(mutex);
        const size_t total = registry.size();
        if (total <= kInlineCap)
        {
            count = total;
            for (size_t i = 0; i < count; ++i)
            {
                stackSnap[i] = registry[i].func;
            }
        }
        else
        {
            heapSnap.reserve(total);
            for (const auto& e : registry)
            {
                heapSnap.push_back(e.func);
            }
            snapshot = heapSnap.data();
            count = total;
        }
    }
    for (size_t i = 0; i < count; ++i)
    {
        invoke(snapshot[i]);
    }
}
} // namespace

static std::vector<CallbackEntry<void(*)(const char* msg)>> outputSinkCallbacks;
static std::mutex outputSinkMutex;
static int s_nextOutputSinkId = 1;

namespace utinni::log
{
class OutputSink : public spdlog::sinks::base_sink<std::mutex>
{
public:
    OutputSink()
    {
        set_pattern("[%H:%M:%S] [%l]%! %v");
    }

protected:
    void sink_it_(const spdlog::details::log_msg& msg) override
    {
        spdlog::memory_buf_t formatted;
        base_sink<std::mutex>::formatter_->format(msg, formatted);

        const std::string formattedString = fmt::to_string(formatted);

        logMessageBuffer.emplace_back(formattedString);
        // R-H snapshot dispatch per D-12. CR-01 + WR-07: snapshot built under
        // outputSinkMutex so concurrent subscribe / unsubscribe (called from
        // arbitrary threads, e.g. WinForms UI thread) can't race the registry
        // during iteration. Iteration outside the lock so callbacks can
        // re-subscribe without deadlock. Stack-snapshot via dispatchSnapshot
        // keeps the per-log path heap-free.
        const char* msgCStr = formattedString.c_str();
        dispatchSnapshot(outputSinkCallbacks, outputSinkMutex,
            [msgCStr](void(*func)(const char*)) { func(msgCStr); });
    }

    void flush_() override { }
};
    
void create()
{
    spdlog::set_level(spdlog::level::debug);

    spdlog::set_pattern("[%H:%M:%S] [%l]%! %v");
    spdlog::flush_on(spdlog::level::info);

    const std::string logName = "utinni";
    const std::string previousLogFilename = getPath() + logName + "_previous.log";
    const std::string logFilename = getPath() + logName + ".log";

    // Delete previous log and rename the current log to previous log, so spdlog can create a clean new log
    std::remove(previousLogFilename.c_str());
    std::rename(logFilename.c_str(), previousLogFilename.c_str());

    auto file_logger = spdlog::basic_logger_mt("UtinniLog", logFilename);
    file_logger->sinks().emplace_back(std::make_shared<OutputSink>());
    spdlog::set_default_logger(file_logger);

}

void critical(const char* text)
{
    spdlog::critical(text);
}

void debug(const char* text)
{
    spdlog::debug(text);
}

void error(const char* text)
{
    spdlog::error(text);
}

void info(const char* text)
{
    spdlog::info(text);
}

void warning(const char* text)
{
    spdlog::warn(text);
}

// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09.
int subscribeOutputSinkCallback(void(*func)(const char* msg))
{
    std::lock_guard<std::mutex> guard(outputSinkMutex);
    int id = s_nextOutputSinkId++;
    if (id == 0) { id = s_nextOutputSinkId++; } // WR-04 skip-zero
    outputSinkCallbacks.push_back({id, func});
    return id;
}

bool unsubscribeOutputSinkCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(outputSinkMutex);
    for (auto it = outputSinkCallbacks.begin(); it != outputSinkCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            outputSinkCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

void addOutputSinkCallback(void(*func)(const char* msg))
{
    subscribeOutputSinkCallback(func);
}

int getMessageBufferCount()
{
    return logMessageBuffer.size();
}

const char* getMessageAt(int i)
{
    return logMessageBuffer[i].c_str();
}

}
