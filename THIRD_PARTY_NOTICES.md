# Third-party notices

Konvert code is Apache-2.0. PDF text extraction uses [PdfPig 0.1.16](https://github.com/UglyToad/PdfPig/tree/v0.1.16) under Apache-2.0. The license terms are included in `LICENSE`.

The PdfPig upstream notice follows.

```text
This product is derived from software developed at
The Apache Software Foundation (http://www.apache.org/).

Based on source code originally developed in the PDFBox and
FontBox projects.

Copyright (c) 2002-2007, www.pdfbox.org

Includes the Adobe Glyph List
Copyright 1997, 1998, 2002, 2007, 2010 Adobe Systems Incorporated.

Includes the Zapf Dingbats Glyph List
Copyright 2002, 2010 Adobe Systems Incorporated.
```

.NET and WPF use upstream licenses including MIT and third-party notices. Compact builds require a separately installed Desktop Runtime. Self-contained builds include runtime license/notice files, which must be preserved. See [dotnet/runtime](https://github.com/dotnet/runtime) and [dotnet/wpf](https://github.com/dotnet/wpf).

For self-contained packages, `scripts/Copy-RuntimeNotices.ps1` queries the exact resolved runtime packs and copies their license/notice files and version markers into `licenses/`. Packaging fails if the runtime packs or their licenses cannot be found.

LibreOffice and FFmpeg are optional, user-installed engines, **not bundled** in portable/MSIX payloads. Their licenses apply independently. LibreOffice is primarily MPL-2.0; FFmpeg licensing varies with enabled components and may include LGPL/GPL terms. Check the actual distribution before redistributing an engine. Apache-2.0 does not relicense these products.

The README banner is AI-generated brand artwork. [Prompt and provenance](docs/assets/README.md). Simple K icons come from `scripts/New-BrandAssets.ps1`. Marketing artwork is excluded from application payloads.
