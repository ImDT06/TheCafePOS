# WPF UI smoke checks

Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-ui.ps1` from the repository root on Windows with .NET 10.

Uses an isolated database under `artifacts/dialog-preview`; does not edit the shop database. Loads the real application resources and dialog classes, renders 14 dialogs including their tabs, and renders the main window at 960 and 1240 pixels. Outputs PNGs in that directory for visual inspection. The main test window is placed offscreen; keyboard focus checks require an interactive Windows desktop.

Assertions cover empty/focused/typed/whitespace/cleared search hints, stable input position across focus, category navigation and its end states, management search and selection, and dropdown/scroll templates. It does not submit real payments, send messages, or print to a physical printer. It does not prove correctness for every DPI, display size, or screen reader.

The keypad checks also cover currency preview/change, insufficient cash, selection replacement and caret deletion, empty/fractional/negative/oversized values, stock/refund limits, opening via the field command, cancel preservation, and applying an amount without checking out. The harness runs in Release so a running Debug POS does not lock its executable. Physical touch gestures still need verification on the target device.
