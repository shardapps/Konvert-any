#include <winrt/Windows.Data.Pdf.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Imaging.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Windows.Storage.Streams.h>
#include <algorithm>
#include <cmath>
#include <filesystem>
#include <iostream>
#include <stdexcept>
#include <string>

namespace {
using namespace winrt;
using namespace Windows::Data::Pdf;
using namespace Windows::Graphics::Imaging;
using namespace Windows::Storage;
using namespace Windows::Storage::Streams;

unsigned Number(const wchar_t* value, unsigned low, unsigned high) {
    std::wstring text(value);
    if (text.empty() || text.find_first_not_of(L"0123456789") != std::wstring::npos)
        throw std::invalid_argument("Invalid page or image setting.");
    const auto number = std::stoul(text);
    if (number < low || number > high) throw std::invalid_argument("Page or image setting is out of range.");
    return static_cast<unsigned>(number);
}

void Render(PdfDocument const& document, unsigned index, std::filesystem::path const& output, unsigned dpi, unsigned edge) {
    auto page = document.GetPage(index);
    const auto size = page.Size();
    const auto longest = std::max(size.Width, size.Height);
    if (!(size.Width > 0 && size.Height > 0) || !std::isfinite(longest))
        throw std::runtime_error("This PDF page has invalid dimensions.");
    const auto scale = edge ? std::min(dpi / 96.0, static_cast<double>(edge) / longest) : dpi / 96.0;
    const auto width = std::max(1.0, std::round(size.Width * scale));
    const auto height = std::max(1.0, std::round(size.Height * scale));
    if (width * height > 16'000'000)
        throw std::runtime_error("This PDF page exceeds 16 megapixels at the chosen size. Lower the DPI or image size.");
    PdfPageRenderOptions options;
    options.BitmapEncoderId(BitmapEncoder::PngEncoderId());
    options.IsIgnoringHighContrast(true);
    options.DestinationWidth(static_cast<unsigned>(width));
    options.DestinationHeight(static_cast<unsigned>(height));
    InMemoryRandomAccessStream rendered;
    page.RenderToStreamAsync(rendered, options).get();
    rendered.Seek(0);
    auto decoder = BitmapDecoder::CreateAsync(rendered).get();
    if (static_cast<uint64_t>(decoder.PixelWidth()) * decoder.PixelHeight() > 32'000'000)
        throw std::runtime_error("Windows rendered a page larger than the image limit. Choose a lower resolution.");
    auto stream = FileRandomAccessStream::OpenAsync(output.wstring(), FileAccessMode::ReadWrite,
        StorageOpenOptions::None, FileOpenDisposition::CreateNew).get();
    if (decoder.PixelWidth() == static_cast<unsigned>(width) && decoder.PixelHeight() == static_cast<unsigned>(height)) {
        rendered.Seek(0);
        RandomAccessStream::CopyAsync(rendered, stream).get();
    } else {
        // WinRT accepts DIPs. Normalize the encoded pixels so the result does not depend on display scaling.
        auto encoder = BitmapEncoder::CreateForTranscodingAsync(stream, decoder).get();
        auto transform = encoder.BitmapTransform();
        transform.ScaledWidth(static_cast<unsigned>(width));
        transform.ScaledHeight(static_cast<unsigned>(height));
        transform.InterpolationMode(BitmapInterpolationMode::Fant);
        encoder.FlushAsync().get();
    }
    stream.FlushAsync().get();
    stream.Close();
    page.Close();
}
}

int wmain(int argc, wchar_t* argv[]) {
    try {
        if (argc != 3 && argc != 7) return 2;
        const std::wstring command = argv[1];
        if (argc == 3 && command != L"--info") return 2;
        if (argc == 7 && command != L"--render") return 2;
        const auto input = std::filesystem::path(argv[2]).make_preferred();
        if (std::filesystem::file_size(input) > 128ULL * 1024 * 1024)
            throw std::runtime_error("PDF inputs are limited to 128 MiB.");
        init_apartment(apartment_type::multi_threaded);
        auto source = FileRandomAccessStream::OpenAsync(input.wstring(), FileAccessMode::Read).get();
        auto document = PdfDocument::LoadFromStreamAsync(source).get();
        if (document.IsPasswordProtected()) throw std::runtime_error("Open and unlock this PDF before converting it.");
        const auto pages = document.PageCount();
        if (command == L"--info") {
            std::cout << pages;
            return pages ? 0 : 1;
        }
        const auto page = Number(argv[4], 0, 10'000);
        const auto dpi = Number(argv[5], 72, 600);
        const auto edge = Number(argv[6], 0, 3840);
        const auto output = std::filesystem::path(argv[3]).make_preferred();
        if (page) {
            if (page > pages) throw std::runtime_error("That page is outside this PDF.");
            Render(document, page - 1, output, dpi, edge);
        } else {
            if (pages == 0 || pages > 100) throw std::runtime_error("A page archive requires between 1 and 100 PDF pages.");
            uintmax_t bytes = 0;
            for (unsigned index = 0; index < pages; ++index) {
                auto number = std::to_wstring(index + 1);
                auto path = output / (L"Page-" + std::wstring(3 - number.size(), L'0') + number + L".png");
                Render(document, index, path, dpi, edge);
                bytes += std::filesystem::file_size(path);
                if (bytes > 512 * 1024 * 1024) throw std::runtime_error("The rendered pages exceed 512 MiB. Try a smaller image size.");
            }
        }
        return 0;
    } catch (hresult_error const&) {
        std::cerr << "Windows could not open or render this PDF. Check whether it is locked or damaged.";
    } catch (std::exception const& error) {
        std::cerr << error.what();
    }
    return 1;
}
