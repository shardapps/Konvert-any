# Design direction · 0.2

Konvert's main interaction is a temporary radial popup around the selected file, rather than a full application window. Ctrl+K reads one selected filesystem item from the active Explorer view or desktop. Source identity stays in the center; destination formats orbit it. Escape and an outside click dismiss idle popups. Work-area clamping keeps the menu on-screen, including monitors with negative coordinates.

The default Glass theme uses translucent surfaces, luminous edges and short entrance motion. It is inspired by Apple's separation of floating controls from content and Material's hierarchy, color roles and approachable shapes. This is an original Windows implementation, not Apple's Liquid Glass renderer. It does not capture the screen to synthesize refraction. Windows acrylic is used for eligible settings/home windows; the shaped popup uses per-pixel alpha. High contrast replaces themed colors, and reduced motion disables entrance animation.

Seven themes: Glass, Pearl, Graphite, Midnight, Ocean, Lavender and Forest. All icons are original 24-unit vector paths in `IconControl.cs`; buttons, sliders, switches, text fields and scrollbar templates are maintained in `App.xaml`. Shared color roles keep controls consistent across themes. A separate settings window uses a sidebar for Appearance, Conversion, Windows, Converters and About.

Read-only Shardapps references: Screen's small native controls, theme variety, per-user x64/ARM64 setup and `%LOCALAPPDATA%\Programs\Shardapps` convention; Clippboard's event-driven shortcut and transient utility surface. No sibling source, assets, settings or installed applications were modified.

Design references reviewed:

- [Apple: Meet Liquid Glass](https://developer.apple.com/videos/play/wwdc2025/219/): floating functional layers, clear separation and responsive motion.
- [Apple: Materials](https://developer.apple.com/design/human-interface-guidelines/materials): visual hierarchy and legibility.
- [Material 3 color roles](https://m3.material.io/styles/color/system/overview) and [motion](https://m3.material.io/styles/motion/overview): consistent roles and transitions.
- [Screen Dimmer Store reference supplied by the owner](https://apps.microsoft.com/detail/9PNHDHKMHDLG): ShardCoders publisher/product family context. The public listing could not be extracted by the web reader; sibling repository/install metadata supplied the installation reference.
