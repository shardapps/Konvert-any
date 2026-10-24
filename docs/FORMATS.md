# Conversion reference

The menu combines the source **extension** with installed engine capabilities. It does not prove file contents from the extension. A corrupt, encrypted, mislabeled or unsupported file can fail during conversion; originals are preserved. The registry is [`FormatRegistry.cs`](../src/Konvert/FormatRegistry.cs).

## Built in

| Source | Offered destinations | Behavior |
| --- | --- | --- |
| PNG, JPG/JPEG, BMP, GIF, TIF/TIFF, ICO, WDP | PNG, JPG, BMP, TIFF, GIF, PDF | Windows WPF codecs; first frame/page only |
| TXT, MD, LOG, JSON, XML, YAML/YML, CSV, TSV, HTML/HTM, CSS, JS, CS, PY, SQL | TXT; HTML except for HTML/HTM inputs | Preserves raw text; HTML wraps escaped text in a readable page |
| DOCX | TXT | Main document paragraphs, text runs, tabs and breaks; no layout, headers, footnotes or embedded objects |
| CSV | JSON, TSV | JSON uses the first row as unique, non-empty headings; all values remain strings |
| TSV | JSON, CSV | Quoted tabs, escaped quotes and multiline fields supported |
| JSON | CSV | Requires an array of flat objects; union of keys becomes the header; missing/null values become empty fields |

Same-extension targets are removed. JPG/JPEG and TIF/TIFF aliases may result in an equivalent-format export because the suffix differs. Markdown-to-HTML displays escaped Markdown source, not rendered Markdown. HTML-to-TXT preserves source markup. Text decoding supports strict UTF-8 and BOM-marked UTF-16/32; invalid encodings fail instead of silently changing bytes.

## Optional LibreOffice

Install LibreOffice separately and restart Konvert, or select `soffice.exe` in Settings. Konvert searches Program Files and absolute PATH directories, not the current folder. The engine is not downloaded or bundled by the app.

| Source family | Destinations |
| --- | --- |
| DOCX, DOC, ODT, RTF, TXT | PDF, DOCX, ODT, RTF, HTML, TXT |
| XLSX, XLS, ODS, CSV, TSV | PDF, XLSX, ODS, CSV |
| PPTX, PPT, ODP | PDF, PPTX, ODP |

Native routes take priority where both engines offer a target. LibreOffice uses its extension-selected default export filters and a fresh isolated profile with macro security set to level 3. It has a three-minute timeout. Office fonts, complex layout, fields, formulas, links and animations may differ. Spreadsheet-to-CSV is a single-sheet export and loses formatting/formulas. CSV/TSV imports use LibreOffice defaults, which may infer types or delimiters; use native table routes when preserving string values matters. Password-protected files are not supported. External linked resources are not a security sandbox boundary.

See upstream [command-line parameters](https://help.libreoffice.org/latest/en-US/text/shared/guide/start_parameters.html).

## Optional FFmpeg

Install a trusted FFmpeg build separately, add it to PATH or select `ffmpeg.exe` in Settings. Konvert probes its encoder list on startup/settings changes; required encoders must exist before a target appears. Input demuxers/decoders and actual streams are validated only when converting.

| Sources | Destinations |
| --- | --- |
| MP3, WAV, FLAC, OGG, M4A, AAC, AIFF, WMA, Opus | MP3, WAV, FLAC, OGG, M4A, Opus |
| MP4, MKV, MOV, WebM, AVI, WMV, M4V, MPEG, MPG | MP4, MKV, WebM, GIF and the audio outputs above |

| Output | Encoder/default |
| --- | --- |
| MP3 | libmp3lame, quality 2 |
| WAV | PCM s16le |
| FLAC | flac |
| OGG | libvorbis, quality 5 |
| M4A | AAC, 192 kbit/s |
| Opus | libopus, 128 kbit/s |
| MP4/MKV | H.264 libx264, CRF 23, fast preset, yuv420p; optional first audio track encoded AAC |
| WebM | VP9 libvpx-vp9, CRF 32; optional first audio track encoded Opus |
| GIF | First video stream, 12 fps, scaled to 640px wide, no audio |

Audio extraction selects the first audio track and fails if none exists. Video selects the first video track; subtitles, chapters, additional tracks and metadata are not preserved. These exports re-encode; lossless source does not imply lossless output. Video dimensions must suit the chosen encoder (for example H.264 yuv420p needs even dimensions). JPEG/DPI settings do not apply to media or office exports. Worker thread settings are bounded; engines can still allocate codec-specific memory. Media timeout: 30 minutes. FFmpeg is restricted to local file/pipe input protocols.

## Limits and fidelity

- Native image limit: **24 megapixels**. Decoded pixels can consume much more memory than the compressed file. Only the first frame/page is exported; EXIF orientation and metadata are not guaranteed to be retained or normalized.
- JPEG and image PDF flatten transparency to white. GIF has a limited palette. TIFF uses ZIP compression. PNG is lossless after pixel normalization, which may change color profiles/metadata.
- Image PDF is a single page with an embedded JPEG; it is not OCR/searchable text. Print size is pixels × 72 / DPI in PDF points. No enlargement or detail recovery is implied.
- DPI changes print metadata/PDF physical size while keeping image pixel dimensions. JPEG quality applies to JPG and image-PDF only.
- Native text/table and compressed DOCX inputs: **16 MiB**; DOCX main document XML expanded content is also capped. Tables: **100,000 rows**, **1,000 columns**. Table conversion holds bounded input text/JSON in memory; CSV/TSV rows are yielded incrementally. Text-to-text/HTML streams.
- Empty text outputs are allowed. Failed or cancelled operations clean staging files on a best-effort basis. Native codec calls cannot be interrupted mid-call; cancellation completes when that call returns.
- One file at a time. Multiple app processes are not globally serialized. A crash/power loss can leave temporary files. Existing files are never deliberately overwritten.

Not available yet: PDF input conversion, WebP/HEIC/SVG export, archive conversion, OCR, batches, font substitution controls or per-format media quality presets. Request formats with reproducible examples and fidelity expectations.
