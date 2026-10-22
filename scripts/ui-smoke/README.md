# WPF UI smoke checks

Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-ui.ps1` from the repository root on Windows with .NET 10.

Uses an isolated database under `artifacts/dialog-preview`; does not edit the shop database. Loads the real application resources and dialog classes, renders 14 dialogs including their tabs, and renders the main window at 960 and 1240 pixels. Outputs PNGs in that directory for visual inspection. The main test window is placed offscreen; keyboard focus checks require an interactive Windows desktop.

Assertions cover empty/focused/typed/whitespace/cleared search hints, stable input position across focus, category navigation and its end states, management search and selection, and dropdown/scroll templates. It does not submit real payments, send messages, or print to a physical printer. It does not prove correctness for every DPI, display size, or screen reader.

The keypad checks also cover currency preview/change, insufficient cash, selection replacement and caret deletion, empty/fractional/negative/oversized values, stock/refund limits, opening via the field command, cancel preservation, and applying an amount without checking out. The harness runs in Release so a running Debug POS does not lock its executable. Physical touch gestures still need verification on the target device.

Cart checks select a real menu item, page through four toppings at a time, preserve topping selections, render populated carts at both widths, open the Material icon action menu, cancel editing, split one unit, delete and undo at the original position, and reject stale undo after holding the order. Cash dialog checks cover insufficient payment, change preview, and cancel preservation. Additional PNGs include `MainWindow-cart-960`, `MainWindow-cart-1240`, `OptionCustomModal-toppings-page2`, and `CashPaymentModal`.

Checkout checks also verify quantity changes update the displayed total, the early open-shift prompt, packaging shortage details, repeated payment events, invalid/fractional cash input, and retry after stock changes while the cash dialog is open. A real cash checkout against the isolated database verifies one saved order, a cleared cart and the success dialog's three-second automatic close. Screenshots: `MainWindow-checkout-error.png` and `OrderCompleteModal-countdown.png`.

The Management and Report windows are also shown offscreen and rendered after their Material hint animations settle (`ManagementWindow-shown`, `ReportWindow-shown`); the unshown renders show hints overlapping filled values, which is an artefact of rendering a never-shown window. `Toast-undo` renders the Material Snackbar notification with its action.
