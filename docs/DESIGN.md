# Design direction

Ctrl+K opens a picker beside the selected file in Explorer or on the desktop. The header identifies the file and up to three quick actions save with standard settings beside the source. **Options** opens a form for selecting a format, output folder and relevant settings. The form scrolls independently of **Convert**. Unsupported inputs show a short explanation and a file chooser. Opening Konvert directly offers a file chooser and Settings.

| Design choice | Implementation |
| --- | --- |
| Separate the picker from its backdrop | The default Glass theme uses translucent graphite, a soft highlight and a fine rim. Labels and icons stay legible against the tinted surface. [Apple Materials](https://developer.apple.com/design/human-interface-guidelines/materials) · [Meet Liquid Glass](https://developer.apple.com/videos/play/wwdc2025/219/) |
| Give color and shape a purpose | Icy cyan highlights selection, keyboard focus and the primary action. Small circular glyph badges distinguish quick formats. [Apple Color](https://developer.apple.com/design/human-interface-guidelines/color) · [Google Material 3 Expressive research](https://design.google/library/expressive-material-design-google-research) |
| Keep motion brief | The picker fades in over 150 ms. Switching views takes 130 ms. Both respect Reduce motion, Windows animation settings and high contrast. [Apple Motion](https://developer.apple.com/design/human-interface-guidelines/motion) |

An outside click dismisses an idle picker or hides an active export while it finishes. Escape goes back from Options, dismisses the picker or cancels an active export. The picker uses visible keyboard focus and named controls for assistive technology.

## Themes and settings

The seven themes are Glass, Pearl, Graphite, Midnight, Ocean, Lavender and Forest. Appearance previews each theme and marks the selection. Shared color roles keep controls consistent across themes. Icons live in `IconControl.cs` and control templates live in `App.xaml`.

The picker uses per-pixel alpha and frozen brushes. Windows composes the translucent tint over the desktop. Eligible Settings windows use Windows Desktop Acrylic. Reduce transparency, Windows transparency preferences, remote sessions and high contrast select solid surfaces. [Microsoft Acrylic guidance](https://learn.microsoft.com/windows/apps/design/style/acrylic).

Screen informs the compact controls and live theme previews. Clippboard informs the event-driven shortcut and temporary utility window. PowerToys Image Resizer shows how a file action can start in Explorer with advanced choices in a secondary panel. [ShardCoders Store reference](https://apps.microsoft.com/detail/9PNHDHKMHDLG) · [PowerToys Image Resizer](https://learn.microsoft.com/windows/powertoys/image-resizer).
