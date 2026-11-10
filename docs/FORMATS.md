# Conversion reference

The menu combines the source extension with installed engine capabilities. Konvert opens modern images with the Windows decoder to check availability and uses FFprobe to inspect media tracks when available. Corrupt, encrypted, or mislabeled input may still fail on conversion. [Route registry](../src/Konvert/FormatRegistry.cs).

## Built in

| Source | Offered destinations | Behavior |
| --- | --- | --- |
| PNG, JPG/JPEG, BMP, GIF, TIF/TIFF, ICO, WDP/JXR/HDP | PNG, JPG, BMP, TIFF, GIF, ICO, PDF | First frame or page. ICO input uses its largest frame. ICO export includes 16, 32, 48, 64, 128, and 256 px. |
| TXT, MD, LOG, JSON, XML, YAML/YML, CSV, TSV, HTML/HTM, CSS, JS, CS, PY, SQL | TXT; HTML except for HTML/HTM inputs | Preserves raw text; HTML wraps escaped text in a readable page |
| DOCX, ODT | TXT | Main document paragraphs, text runs, tabs and breaks. Layout-aware output is available through the optional Office engine. |
| CSV | JSON, TSV | JSON uses the first row as unique, non-empty headings; all values remain strings |
| TSV | JSON, CSV | Quoted tabs, escaped quotes and multiline fields supported |
| JSON | CSV, TSV | Accepts an array of flat objects. The union of keys becomes the header and missing or null values become empty fields. |

Aliases share their base format, and the picker omits same-format exports. Markdown-to-HTML displays escaped Markdown source. HTML-to-TXT preserves source markup. Text decoding supports strict UTF-8 and BOM-marked UTF-16/32.

## Optional Windows image codecs

WebP, HEIC, HEIF, and AVIF inputs offer the built-in image outputs when Windows can open the selected file. The installed extension and the file’s compression determine availability. Reopen the picker after changing codecs. FFmpeg can also open still WebP images for PNG export. [Microsoft WIC codecs](https://learn.microsoft.com/windows/win32/wic/native-wic-codecs).

## Optional LibreOffice

Install LibreOffice locally and restart Konvert, or select `soffice.com` or `soffice.exe` in **Settings → Converters**. Konvert prefers the console launcher when available and searches Program Files and absolute PATH directories. The engine is installed separately.

| Source family | Destinations |
| --- | --- |
| DOCX, DOC, ODT, RTF, TXT | PDF, DOCX, ODT, RTF, HTML, TXT |
| XLSX, XLS, ODS, CSV, TSV | PDF, XLSX, ODS, CSV |
| PPTX, PPT, ODP | PDF, PPTX, ODP |

Native routes take priority where both engines offer a target. LibreOffice uses its extension-selected default export filters and a fresh isolated profile with macro security set to level 3. The 60-second deadline includes engine startup and diagnostic output. Office fonts, complex layout, fields, formulas, links and animations may differ. Spreadsheet-to-CSV is a single-sheet export and loses formatting and formulas. CSV/TSV imports use LibreOffice defaults, which may infer types or delimiters. Use native table routes when preserving string values matters. Password-protected files are unsupported. [LibreOffice command-line options](https://help.libreoffice.org/latest/en-US/text/shared/guide/start_parameters.html?System=WIN).

## Optional FFmpeg

Install FFmpeg separately, add it to PATH, or select `ffmpeg.exe` in Settings. Konvert checks the build for the output encoder and muxer. If the same directory contains FFprobe, it also checks for audio and video streams. **Options** shows additional formats and setup guidance.

If the picker has no export for a file, its message tells you whether to choose a different file or open converter setup. If a job fails, the picker shows the cause and lets you retry or choose a different destination. Existing files and incomplete outputs are protected by staged writes.

| Sources | Destinations |
| --- | --- |
| MP3, WAV, FLAC, OGG, M4A, AAC, AIFF/AIF/AIFC, WMA, Opus, CAF, MKA, AC3, EAC3, AMR | MP3, WAV, FLAC, OGG, M4A, Opus, AAC, AIFF, CAF |
| MP4, MKV, MOV, WebM, AVI, WMV, M4V, MPEG, MPG, TS/M2TS/MTS, FLV, OGV, 3GP, VOB, ASF | MP4, MKV, WebM, MOV, AVI, TS, GIF and the audio outputs above |
| PNG, JPG, BMP, GIF, TIFF, ICO | WebP, first frame |
| WebP | PNG, first frame, when the native Windows route is unavailable |

| Output | Encoder/default |
| --- | --- |
| MP3 | libmp3lame, quality 2 |
| WAV | PCM s16le |
| FLAC | flac |
| OGG | libvorbis, quality 5 |
| M4A / AAC | AAC, 192 kbit/s, in MP4 or ADTS |
| Opus | libopus, 128 kbit/s |
| AIFF / CAF | 16-bit PCM |
| MP4 / MKV / MOV / TS | H.264 libx264, CRF 23, fast preset, yuv420p. Optional first audio track encoded AAC. |
| AVI | MPEG-4 video, quality 4, yuv420p. Optional first audio track encoded MP3. |
| WebM | VP9 libvpx-vp9, CRF 32; optional first audio track encoded Opus |
| GIF | First video stream, 12 fps, scaled to 640px wide, no audio |
| WebP | libwebp lossless |

Audio and video exports select the first matching track. Video exports omit subtitles, chapters, and extra tracks. H.264 and AVI add one pixel of padding to an odd edge for compatible 4:2:0 output. Media exports re-encode with the chosen codec. FFmpeg receives a bounded thread setting, uses local file and pipe protocols, and has a 30-minute conversion deadline.

## Limits and fidelity

- Native image limit is **24 megapixels**. Image export uses the first frame or page, or the largest ICO frame. EXIF orientation, HDR and metadata preservation depend on the decoder and output.
- JPEG and image PDF flatten transparency to white. GIF has a limited palette. TIFF uses ZIP compression. PNG is lossless after pixel normalization, which may change color profiles/metadata.
- Image PDF is a single page with an embedded JPEG; it is not OCR/searchable text. Print size is pixels × 72 / DPI in PDF points. No enlargement or detail recovery is implied.
- Image size limits the longest edge while keeping proportions. It applies to native image output and still-image FFmpeg exports. ICO uses fixed sizes with transparent padding. DPI controls print metadata and image-PDF size. JPEG quality applies to JPG and image PDF.
- Native text, tables, and compressed DOCX/ODT inputs are limited to **16 MiB**. The extracted XML and expanded ODT whitespace also have limits. Tables allow **100,000 rows** and **1,000 columns**. Text-to-text, HTML and delimited rows stream.
- Empty text outputs are allowed. Failed or cancelled operations clean staging files on a best-effort basis. Native codec calls cannot be interrupted mid-call; cancellation completes when that call returns.
- One file at a time. Multiple app processes are not globally serialized. A crash/power loss can leave temporary files. Existing files are never deliberately overwritten.

PDF input conversion, HEIC/AVIF/SVG export, archives, OCR, batches, and media quality presets are planned.
