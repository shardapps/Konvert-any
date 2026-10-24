# Microsoft Store preparation

Product brand: **Shardapps**. Existing sibling apps identify the Store publisher as **ShardCoders**. Konvert's actual Partner Center identity and publisher certificate subject must come from the owner's reserved product; they cannot be inferred from a display name.

`packaging/AppxManifest.xml` is a full-trust desktop package with Windows 11 minimum and an execution alias. `Build-Msix.ps1` publishes self-contained x64/ARM64 binaries, adds required logos/notices, fills explicit identity/publisher/version values and runs MakeAppx's validation. Development packages are unsigned. There is no upload, production secret, auto-publish workflow or fabricated Store product ID.

Before Store submission:

- Reserve the product and use its exact identity, publisher and version values.
- Implement and test a packaged IExplorerCommand extension for the modern Windows 11 context menu; current portable registry integration is not a substitute.
- Review full-trust capability justification, privacy declaration and optional engine behavior under Store policies.
- Run Windows App Certification Kit, clean-machine install/upgrade/uninstall tests, execution alias checks, and physical ARM64 testing.
- Verify keyboard/screen reader behavior, high contrast, reduced motion and 100/150/200% DPI. Replace any incomplete accessibility behavior before broad release.
- Capture actual UI screenshots; provide accurate localized descriptions, support/privacy links and age/content ratings.
- Review signing/submission under a protected release environment and retain a rollback plan.

References: [desktop MSIX packaging](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-root), [context menu integration](https://learn.microsoft.com/en-us/windows/win32/shell/creating-static-cascading-menus). These are implementation references, not evidence of Konvert certification.
