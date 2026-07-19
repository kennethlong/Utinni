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

#include "terrain.h"
#include "swg/endpoints.h"

namespace swg::terrain
{
using pReloadTerrain = int(__thiscall*)(utinni::Terrain* pThis);
using pSetTimeOfDay = int(__thiscall*)(utinni::Terrain* pThis, float, bool);
using pGetTimeOfDay = float(__thiscall*)(utinni::Terrain* pThis);
using pSetWeatherIndex = int(__cdecl*)(int);

pReloadTerrain reloadTerrain = (pReloadTerrain)0x0051A4F0;
pSetTimeOfDay setTimeOfDay = (pSetTimeOfDay)0x00B5CBD0;
pGetTimeOfDay getTimeOfDay = (pGetTimeOfDay)0x00B5CBC0;
pSetWeatherIndex setWeatherIndex = (pSetWeatherIndex)0x00845C90;
} // namespace swg::terrain

namespace utinni
{
Terrain* Terrain::get()
{
    return memory::read<Terrain*>(0x01947194); // TerrainObject static pointer
}

// The four RVA-backed members below are SWGEmu-only literals with no advertised rebinding.
// Calling one on the advertised client executes unrelated NGE code (2026-07-19 live crash:
// setWeatherIndex's 0x00845C90 landed mid-CuiStringIds-initializer there -> stray std::string
// dtor -> MemoryManager::free(0x1) AV). Fail closed until provider rows exist. Terrain::get()
// and getFilename() stay unguarded: field-proven valid on advertised (the Wave-3 reload leg's
// getCurrentSceneNameFromTerrain depends on them).
void Terrain::setTimeOfDay(float time)
{
    if (swg::endpoints::isAdvertisedClient())
        return;
    swg::terrain::setTimeOfDay(this, time, true);
}

float Terrain::getTimeOfDay()
{
    if (swg::endpoints::isAdvertisedClient())
        return 0.0f;
    return swg::terrain::getTimeOfDay(this);
}

int Terrain::getTimeOfDayHours()
{
    int result = 6 + (int)(getTimeOfDay() * 24.f * 60.f) / 60;
    if (result >= 24)
    {
        result -= 24;
    }
    return result;
}

int Terrain::getTimeOfDayMinutes()
{
    return (int)fmodf(getTimeOfDay() * 24.f * 60.f, 60.f);
}

int Terrain::getWeatherIndex()
{
    if (swg::endpoints::isAdvertisedClient())
        return 0;
    return memory::read<int>(0x01924B6C); // Current terrain weatherIndex static pointer
}

void Terrain::setWeatherIndex(int weatherIndex)
{
    if (swg::endpoints::isAdvertisedClient())
        return;
    swg::terrain::setWeatherIndex(weatherIndex);
}

const char* Terrain::getFilename()
{
    return (const char*)0x019113C1; // Current terrain filename static pointer
}
} // namespace utinni
