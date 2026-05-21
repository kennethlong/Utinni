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
#include <unordered_map>
#include <vector>

static std::vector<std::string> logMessageBuffer;
// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registry.
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
static std::unordered_map<int, void(*)(const char* msg)> outputSinkCallbacks;
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
        // arbitrary threads, e.g. WinForms UI thread) can't race the map's
        // bucket structure. Iteration outside the lock so callbacks can
        // re-subscribe without deadlock.
        std::vector<void(*)(const char*)> snapshot;
        {
            std::lock_guard<std::mutex> guard(outputSinkMutex);
            snapshot.reserve(outputSinkCallbacks.size());
            for (const auto& kv : outputSinkCallbacks)
            {
                snapshot.push_back(kv.second);
            }
        }
        for (const auto& func : snapshot)
        {
            func(formattedString.c_str());
        }
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
    outputSinkCallbacks[id] = func;
    return id;
}

bool unsubscribeOutputSinkCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(outputSinkMutex);
    return outputSinkCallbacks.erase(handle) > 0;
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
