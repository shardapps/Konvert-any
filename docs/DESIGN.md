# Design direction

Ctrl+K reads one selected filesystem item from the active Explorer view or desktop. The 224 DIP circle opens below the selection when there’s room and flips above near the taskbar. It presents the source in the center and up to three destination formats around it. Selecting a format saves beside the original at standard quality. **Options** reveals more formats, the output folder, and relevant quality or DPI controls.

## Composition and interaction

| Design choice | Implementation |
| --- | --- |
| Separate the control layer from the file underneath | Glass uses one translucent graphite disc with a static edge sheen. Text, tooltips, and the options drawer use more opaque surfaces to hold contrast over varied desktops. [Apple Materials](https://developer.apple.com/design/human-interface-guidelines/materials) · [Adopting Liquid Glass](https://developer.apple.com/documentation/TechnologyOverviews/adopting-liquid-glass) |
| Give each color a job | Neutral surfaces distinguish the shell, buttons, and drawer. Icy cyan identifies focus and active states. Foreground colors are paired with the surfaces they appear on, with separate roles for small secondary text and filled actions. [Apple Color](https://developer.apple.com/design/human-interface-guidelines/color) · [Google Material 3 theming](https://developer.android.com/codelabs/m3-design-theming) |
| Use shape and size to clarify priority | The large center explains the source or active conversion. Smaller circular buttons label quick destinations with a format and category. The Options capsule uses a label and arrow to communicate disclosure. Repeated controls keep their position and shape across themes. [Apple Buttons](https://developer.apple.com/design/human-interface-guidelines/buttons) · [Google Material 3 Expressive research](https://design.google/library/expressive-material-design-google-research) |
| Keep motion brief and tied to the action | The file popup settles over 145 ms. The drawer fades in over 140 ms. Both use ease-out and respect Reduce motion, Windows animation settings, and high contrast. Progress stays readable as text showing the destination, provider, and elapsed time. [Apple Motion](https://developer.apple.com/design/human-interface-guidelines/motion) · [Google Material design evolution](https://design.google/library/material-design-eras) |

Click outside to dismiss the picker. During conversion this hides the window and the job continues. Escape cancels an active conversion, otherwise it closes the drawer or dismisses the picker. A visible error gives the user a retry path. Keyboard focus and automation names identify each action.

## Themes and settings

The seven themes are Glass, Pearl, Graphite, Midnight, Ocean, Lavender and Forest. Appearance shows a miniature picker for each theme and marks the current selection with a check. The selected sidebar section uses a tinted surface and a labelled icon. Shared color roles keep controls consistent across themes. Icons live in `IconControl.cs` and control templates live in `App.xaml`.

Windows acrylic is used for eligible settings windows. The shaped popup uses per-pixel alpha and system high contrast colors when enabled. Glass is a Windows implementation of these design principles.

Screen informs the compact controls, theme variety, and Shardapps per-user install path. Clippboard informs the event-driven shortcut and temporary utility window. [ShardCoders Store reference](https://apps.microsoft.com/detail/9PNHDHKMHDLG).
