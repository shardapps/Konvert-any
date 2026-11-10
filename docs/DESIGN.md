# Design direction

Ctrl+K reads one selected filesystem item from the active Explorer view or desktop. The 224 DIP circle opens below the selection when there’s room and flips above near the taskbar. It presents the source in the center and up to three destination formats around it. Selecting a format saves beside the original at standard quality. **Options** groups long lists of video and audio destinations by type. It also reveals the output folder and relevant size, quality, or DPI controls. Opening Konvert directly shows Choose file and Settings.

## Composition and interaction

| Design choice | Implementation |
| --- | --- |
| Separate the control layer from the file underneath | The lens has a translucent blue tint, a directional highlight, and two rim layers. Text stays on solid controls and panels for contrast on light and dark desktops. [Apple Materials](https://developer.apple.com/design/human-interface-guidelines/materials) · [Meet Liquid Glass](https://developer.apple.com/videos/play/wwdc2025/219/) |
| Give each color a job | Neutral surfaces distinguish the shell, buttons, and drawer. Icy cyan identifies focus and active states. Foreground colors are paired with the surfaces they appear on, with separate roles for small secondary text and filled actions. [Apple Color](https://developer.apple.com/design/human-interface-guidelines/color) · [Google Material 3 theming](https://developer.android.com/codelabs/m3-design-theming) |
| Use shape and size to clarify priority | The large center shows the source or active conversion. Smaller circular buttons show a format and original vector glyph. The Options capsule uses a label and arrow to communicate disclosure. [Apple Buttons](https://developer.apple.com/design/human-interface-guidelines/buttons) · [Google Material 3 Expressive research](https://design.google/library/expressive-material-design-google-research) |
| Keep motion brief and tied to the action | The lens settles into place over 185 ms and the drawer reveals itself over 165 ms. Both ease out and respect Reduce motion, Windows animation settings, and high contrast. Progress is shown as text with the destination, provider, and elapsed time. [Apple Motion](https://developer.apple.com/design/human-interface-guidelines/motion) · [Google Material design evolution](https://design.google/library/material-design-eras) |

Click outside either picker to dismiss it. During conversion this hides the window and the job continues. Escape cancels an active conversion, otherwise it closes the drawer or dismisses the picker. Keyboard focus and automation names identify each action.

## Themes and settings

The seven themes are Glass, Pearl, Graphite, Midnight, Ocean, Lavender and Forest. Appearance shows a miniature picker for each theme and marks the current selection with a check. The selected sidebar section uses a tinted surface and a labelled icon. Shared color roles keep controls consistent across themes. Icons live in `IconControl.cs` and control templates live in `App.xaml`.

The shaped popup uses per-pixel alpha and frozen vector brushes. Its light and rim layers are drawn over the tint. The desktop shows through by composition, with no screen sampling or blur pass. Windows Desktop Acrylic is used for eligible Settings windows. Reduce transparency, Windows transparency preferences, remote sessions, and high contrast select solid surfaces. [Microsoft Acrylic guidance](https://learn.microsoft.com/windows/apps/design/style/acrylic).

Screen informs the compact controls and live theme previews. Clippboard informs the event-driven shortcut and temporary utility window. PowerToys Image Resizer shows how a file action can start in Explorer with advanced choices in a secondary panel. [ShardCoders Store reference](https://apps.microsoft.com/detail/9PNHDHKMHDLG) · [PowerToys Image Resizer](https://learn.microsoft.com/windows/powertoys/image-resizer).
