// Original project code, MIT. Uses the Windows DirectWrite operating-system API.
#define NOMINMAX
#include <windows.h>
#include <d2d1.h>
#include <dwrite_3.h>
#include <cstring>
// No C/C++ runtime dependency: fixed-size buffers, no exceptions or heap allocations.
extern "C" int _fltused = 0;
#pragma function(memset)
extern "C" void* __cdecl memset(void* target, int value, size_t count) {
    auto bytes = static_cast<volatile unsigned char*>(target);
    while (count--) *bytes++ = static_cast<unsigned char>(value);
    return target;
}
template<class T> struct ComPtr {
    T* p = nullptr;
    ~ComPtr() { if (p) p->Release(); }
    T* operator->() const { return p; }
    T** GetAddressOf() { return &p; }
    T* Get() const { return p; }
};
using Callback = void(__cdecl*)(int, float, float, float, float, float, float);

class OutlineSink final : public IDWriteGeometrySink {
    ULONG refs = 1;
    Callback callback;
    float originX, originY;
public:
    explicit OutlineSink(Callback c, float x = 0, float y = 0) : callback(c), originX(x), originY(y) {}
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id, void** result) override {
        if (!result) return E_POINTER;
        *result = nullptr;
        if (id == __uuidof(IUnknown) || id == __uuidof(IDWriteGeometrySink)) {
            *result = static_cast<IDWriteGeometrySink*>(this); AddRef(); return S_OK;
        }
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++refs; }
    ULONG STDMETHODCALLTYPE Release() override { return --refs; }
    void STDMETHODCALLTYPE SetFillMode(D2D1_FILL_MODE mode) override { callback(4, static_cast<float>(mode), 0, 0, 0, 0, 0); }
    void STDMETHODCALLTYPE SetSegmentFlags(D2D1_PATH_SEGMENT) override {}
    void STDMETHODCALLTYPE BeginFigure(D2D1_POINT_2F p, D2D1_FIGURE_BEGIN) override { callback(0, p.x + originX, p.y + originY, 0, 0, 0, 0); }
    void STDMETHODCALLTYPE AddLines(const D2D1_POINT_2F* points, UINT32 count) override {
        for (UINT32 i = 0; i < count; ++i) callback(1, points[i].x + originX, points[i].y + originY, 0, 0, 0, 0);
    }
    void STDMETHODCALLTYPE AddBeziers(const D2D1_BEZIER_SEGMENT* curves, UINT32 count) override {
        for (UINT32 i = 0; i < count; ++i) {
            const auto& c = curves[i]; callback(2, c.point1.x + originX, c.point1.y + originY, c.point2.x + originX, c.point2.y + originY, c.point3.x + originX, c.point3.y + originY);
        }
    }
    void STDMETHODCALLTYPE EndFigure(D2D1_FIGURE_END end) override { callback(3, static_cast<float>(end), 0, 0, 0, 0, 0); }
    HRESULT STDMETHODCALLTYPE Close() override { return S_OK; }
};

extern "C" __declspec(dllexport) HRESULT __cdecl ClockOutline(
    const wchar_t* path, float weight, const wchar_t* text, float size, Callback callback, float* width) noexcept {
        if (!path || !text || !callback || !width || size <= 0 || weight < 200 || weight > 900) return E_INVALIDARG;
        ComPtr<IDWriteFactory6> factory;
        HRESULT hr = DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory6), reinterpret_cast<IUnknown**>(factory.GetAddressOf()));
        if (FAILED(hr)) return hr;
        ComPtr<IDWriteFontFile> file;
        hr = factory->CreateFontFileReference(path, nullptr, file.GetAddressOf()); if (FAILED(hr)) return hr;
        DWRITE_FONT_AXIS_VALUE axis{ DWRITE_FONT_AXIS_TAG_WEIGHT, weight };
        ComPtr<IDWriteFontFaceReference1> reference;
        hr = factory->CreateFontFaceReference(file.Get(), 0, DWRITE_FONT_SIMULATIONS_NONE, &axis, 1, reference.GetAddressOf()); if (FAILED(hr)) return hr;
        ComPtr<IDWriteFontFace5> face;
        hr = reference->CreateFontFace(face.GetAddressOf()); if (FAILED(hr)) return hr;
        UINT32 count = 0; while (count <= 64 && text[count]) ++count;
        if (!count || count > 64) return E_INVALIDARG;
        UINT32 chars[64];
        for (UINT32 i = 0; i < count; ++i) chars[i] = text[i];
        UINT16 glyphs[64];
        hr = face->GetGlyphIndices(chars, count, glyphs); if (FAILED(hr)) return hr;
        DWRITE_FONT_METRICS fm{}; face->GetMetrics(&fm);
        DWRITE_GLYPH_METRICS metrics[64];
        hr = face->GetDesignGlyphMetrics(glyphs, count, metrics); if (FAILED(hr)) return hr;
        UINT32 digits[10]{48,49,50,51,52,53,54,55,56,57}; UINT16 dg[10]{}; DWRITE_GLYPH_METRICS dm[10]{};
        hr = face->GetGlyphIndices(digits, 10, dg); if (FAILED(hr)) return hr;
        hr = face->GetDesignGlyphMetrics(dg, 10, dm); if (FAILED(hr)) return hr;
        UINT32 digitWidth = 0; for (const auto& m : dm) if (m.advanceWidth > digitWidth) digitWidth = m.advanceWidth;
        FLOAT advances[64];
        DWRITE_GLYPH_OFFSET offsets[64];
        *width = 0;
        for (UINT32 i = 0; i < count; ++i) {
            auto units = text[i] >= L'0' && text[i] <= L'9' ? digitWidth : metrics[i].advanceWidth;
            advances[i] = size * units / fm.designUnitsPerEm;
            offsets[i].advanceOffset = size * (static_cast<float>(units) - metrics[i].advanceWidth) / (2 * fm.designUnitsPerEm);
            offsets[i].ascenderOffset = 0;
            *width += advances[i];
        }
        OutlineSink sink(callback);
        return face->GetGlyphRunOutline(size, glyphs, advances, offsets, count, FALSE, FALSE, &sink);
}

// DirectWrite shapes date/city/condition text, including non-Latin scripts.
// The layout's real variable-font axis is used; WPF never simulates bold.
class TextRenderer final : public IDWriteTextRenderer {
    ULONG refs = 1;
    Callback callback;
public:
    explicit TextRenderer(Callback c) : callback(c) {}
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id, void** result) override {
        if (!result) return E_POINTER; *result = nullptr;
        if (id == __uuidof(IUnknown) || id == __uuidof(IDWriteTextRenderer) || id == __uuidof(IDWritePixelSnapping)) { *result = static_cast<IDWriteTextRenderer*>(this); AddRef(); return S_OK; }
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++refs; }
    ULONG STDMETHODCALLTYPE Release() override { return --refs; }
    HRESULT STDMETHODCALLTYPE IsPixelSnappingDisabled(void*, BOOL* value) override { *value = TRUE; return S_OK; }
    HRESULT STDMETHODCALLTYPE GetCurrentTransform(void*, DWRITE_MATRIX* matrix) override { *matrix = { 1, 0, 0, 1, 0, 0 }; return S_OK; }
    HRESULT STDMETHODCALLTYPE GetPixelsPerDip(void*, FLOAT* value) override { *value = 1; return S_OK; }
    HRESULT STDMETHODCALLTYPE DrawGlyphRun(void*, FLOAT x, FLOAT y, DWRITE_MEASURING_MODE, DWRITE_GLYPH_RUN const* run, DWRITE_GLYPH_RUN_DESCRIPTION const*, IUnknown*) override {
        OutlineSink sink(callback, x, y);
        return run->fontFace->GetGlyphRunOutline(run->fontEmSize, run->glyphIndices, run->glyphAdvances, run->glyphOffsets, run->glyphCount, run->isSideways, (run->bidiLevel & 1) != 0, &sink);
    }
    HRESULT STDMETHODCALLTYPE DrawUnderline(void*, FLOAT, FLOAT, DWRITE_UNDERLINE const*, IUnknown*) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE DrawStrikethrough(void*, FLOAT, FLOAT, DWRITE_STRIKETHROUGH const*, IUnknown*) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE DrawInlineObject(void*, FLOAT, FLOAT, IDWriteInlineObject*, BOOL, BOOL, IUnknown*) override { return S_OK; }
};

extern "C" __declspec(dllexport) HRESULT __cdecl TextOutline(
    const wchar_t* path, const wchar_t* family, float weight, const wchar_t* text, float size, Callback callback, float* width) noexcept {
    if (!path || !family || !text || !callback || !width || size <= 0 || weight < 200 || weight > 900) return E_INVALIDARG;
    UINT32 length = 0; while (length <= 512 && text[length]) ++length;
    if (!length || length > 512) return E_INVALIDARG;
    ComPtr<IDWriteFactory6> factory;
    HRESULT hr = DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory6), reinterpret_cast<IUnknown**>(factory.GetAddressOf())); if (FAILED(hr)) return hr;
    ComPtr<IDWriteFontFile> file; hr = factory->CreateFontFileReference(path, nullptr, file.GetAddressOf()); if (FAILED(hr)) return hr;
    ComPtr<IDWriteFontSetBuilder1> builder; hr = factory->CreateFontSetBuilder(builder.GetAddressOf()); if (FAILED(hr)) return hr;
    hr = builder->AddFontFile(file.Get()); if (FAILED(hr)) return hr;
    ComPtr<IDWriteFontSet> set; hr = builder->CreateFontSet(set.GetAddressOf()); if (FAILED(hr)) return hr;
    ComPtr<IDWriteFontCollection1> collection; hr = factory->CreateFontCollectionFromFontSet(set.Get(), collection.GetAddressOf()); if (FAILED(hr)) return hr;
    ComPtr<IDWriteTextFormat> format; hr = factory->CreateTextFormat(family, collection.Get(), DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, size, L"", format.GetAddressOf()); if (FAILED(hr)) return hr;
    format->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP);
    ComPtr<IDWriteTextLayout> layout; hr = factory->CreateTextLayout(text, length, format.Get(), 20000, 1000, layout.GetAddressOf()); if (FAILED(hr)) return hr;
    ComPtr<IDWriteTextLayout4> variable; hr = layout->QueryInterface(__uuidof(IDWriteTextLayout4), reinterpret_cast<void**>(variable.GetAddressOf())); if (FAILED(hr)) return hr;
    DWRITE_FONT_AXIS_VALUE axis{ DWRITE_FONT_AXIS_TAG_WEIGHT, weight };
    hr = variable->SetFontAxisValues(&axis, 1, { 0, length }); if (FAILED(hr)) return hr;
    DWRITE_TEXT_METRICS metrics{}; hr = layout->GetMetrics(&metrics); if (FAILED(hr)) return hr;
    *width = metrics.widthIncludingTrailingWhitespace;
    TextRenderer renderer(callback); return layout->Draw(nullptr, &renderer, 0, 0);
}
