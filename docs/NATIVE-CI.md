# Native build validation

Build compiles the real Uno desktop host on Windows, Linux and Apple Silicon macOS. The macOS job uses the supported `macos-15` standard hosted runner and explicitly checks `uname -m` is `arm64` before running the same font acquisition and `dotnet build ... -p:TextSpaceDesktopOnly=true` steps.

The former `macos-latest` job in run 36451935177 remained queued without a runner while engine tests, Windows/Linux builds, all eight browser suites and ten-package creation succeeded. The macOS job was not treated as passing. Pinning the supported image makes the operating-system baseline explicit rather than depending on a moving alias. It is not a guarantee of scheduling latency or a reason to skip checks. GitHub documents macos-15 as a standard arm64 runner in its hosted-runner reference: https://docs.github.com/en/actions/reference/runners/github-hosted-runners .

The full replacement Build must pass on its exact source head, including the pinned macOS job. No `continue-on-error`, omitted platform, cross-host substitute, manually posted success status or branch-rule bypass is used. Main Build and public Pages acceptance remain separate required delivery gates.

This CI scope establishes compilation on macOS 15 arm64, not exhaustive native interactive behavior or certification on every macOS release. The previous application-identical source also compiled on macos-latest in run 36449604005, but its different-head result is not substituted for this head's full gate. Workflow logs record actual runner and SDK details. Test coverage on additional operating systems and browser engines remains separate work.
