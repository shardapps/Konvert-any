# Design direction

Ctrl+K reads one selected filesystem item from the active Explorer view or desktop. A 224 DIP circle shows its type in the center with up to three destination formats around it. A bottom chevron reveals the format list, output folder, quality and DPI. Escape closes the drawer before dismissing the popup. Clicking outside dismisses an idle popup.

Glass uses a translucent disc, a static highlight and distinct focus outlines. It draws on Apple's floating controls and Material's hierarchy and color roles. Windows acrylic is used for eligible settings windows. The shaped popup uses per-pixel alpha and respects system high contrast.

Seven themes: Glass, Pearl, Graphite, Midnight, Ocean, Lavender and Forest. All icons are original 24-unit vector paths in `IconControl.cs`; buttons, sliders, switches, text fields and scrollbar templates are maintained in `App.xaml`. Shared color roles keep controls consistent across themes. A separate settings window uses a sidebar for Appearance, Conversion, Windows, Converters and About.

Read-only Shardapps references: Screen's small native controls, theme variety, per-user x64/ARM64 setup and `%LOCALAPPDATA%\Programs\Shardapps` convention; Clippboard's event-driven shortcut and transient utility surface. No sibling source, assets, settings or installed applications were modified.

Design references reviewed:

- [Apple: Meet Liquid Glass](https://developer.apple.com/videos/play/wwdc2025/219/): floating functional layers, clear separation and responsive motion.
- [Apple: Materials](https://developer.apple.com/design/human-interface-guidelines/materials): visual hierarchy and legibility.
- [Material 3 color roles](https://m3.material.io/styles/color/system/overview) and [motion](https://m3.material.io/styles/motion/overview): consistent roles and transitions.
- [Screen Dimmer Store reference supplied by the owner](https://apps.microsoft.com/detail/9PNHDHKMHDLG): ShardCoders publisher/product family context. The public listing could not be extracted by the web reader; sibling repository/install metadata supplied the installation reference.
