# Application Control diagnosis

The observed failure is `FileLoadException / 0x800711C7` loading the Debug `TheCafePOS_WPF.dll`. Code Integrity events 3033/3077 identify policy `{0283ac0f-fff1-49ae-ada1-8a933130cad6}` and a signing-level/policy failure. The DLL reports `NotSigned`. `VerifiedAndReputablePolicyState` is 1 (Smart App Control enabled). The read-only CiTool policy-list attempt was denied, so a complete inventory of additional policies has not been established.

Evidence is saved locally in `artifacts/app-control-diagnostics`: Code Integrity events, binary hashes/signature statuses, and environment summary. Events contain local file paths; review before sharing. No account credentials or private keys are collected. No security settings were changed.

## Findings and limits

- Both Debug and Release compile. That does not establish permission to execute the Debug binary.
- The inspected SQL document store uses parameters and transactions. Password hashing uses PBKDF2-SHA256 with per-account salt; comparisons use fixed-time comparison. Saved login tokens use Windows DPAPI.
- QR creation sends the configured bank/account name, account number, amount and order reference to `img.vietqr.io` over HTTPS. This is an external service, not an offline QR generator.
- No `Process.Start` calls were found in the searched application C# files. This limited review is not a full dependency audit, malware scan or security certification, and does not explain Microsoft's reputation decision.
- No usable signing certificate was found in the certificate stores visible to this session. This may differ from the interactive developer's account or a hardware token.

## Supported next step

Use a publicly trusted RSA code-signing certificate or an appropriate trusted signing service. `scripts/sign-release.ps1` supports an existing certificate/private key in CurrentUser/My and Windows SDK SignTool. Supply its thumbprint, SignTool path and the issuer's HTTPS RFC3161 timestamp URL. The script publishes to a new directory, signs the app EXE/DLL with SHA256, verifies Authenticode, and inventories dependencies. It does not re-sign vendor binaries, install certificates or modify policy. Hardware/cloud signing may require a provider-specific workflow instead.

Do not purchase or provision signing services automatically. A trusted certificate/service and its authorized operator are still required. Self-signing or strong-name signing is not a substitute for public trust. Signed files still need validation against the target device's actual policy; unsigned third-party dependencies may need publisher support.

For a managed device, give the evidence to its policy administrator to determine an approved development policy/environment. Smart App Control itself does not offer a per-app allow exception. Do not treat changing filenames, launchers, build modes or locations as a policy fix. Do not disable protection merely to make this test pass.

Sources:
- https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control
- https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/operations/appcontrol-debugging-and-troubleshooting
