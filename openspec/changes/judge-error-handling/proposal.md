## Why

The `judge.py` script inside the Docker sandbox currently does not capture or return compilation/runtime errors from the executed solution programs in a way that the C# `CompileService` can reliably parse. When the `judge.py` script itself crashes (e.g., Python exception), or when Docker exec produces stderr output, the C# service receives empty/unparsable stdout and falls back to a generic "IE" (Internal Error) status — losing valuable error details that should be surfaced to the user.

Additionally, the `JudgeCode` method in `CompileService.cs` uses `ExecCmdFromContainer` which discards stderr entirely, meaning any error output from the container is silently lost.

## What Changes

- **`Sandbox/judge.py`**: Add a top-level try/except wrapper around `main()` to catch unhandled exceptions and output a structured JSON error to stdout (with status "ERROR") instead of crashing silently to stderr.
- **`Services/CompileService.cs`**: Update the `JudgeCode` method to use `ExecCmdFromContainerWithStderr` (already available) so that stderr from the Docker container is captured alongside stdout. Include stderr in the error response when judge output parsing fails.
- **`Services/CompileService.cs`**: Improve the `SubmitCode` flow to handle the "ERROR" status from `judge.py` and map it to a meaningful `SubmissionResponse` with the error message included.

## Capabilities

### New Capabilities
- `judge-error-propagation`: Ensures errors occurring inside the Docker sandbox (compilation errors, script crashes, runtime errors) are captured and propagated back through the C# service as structured error information.

### Modified Capabilities
_None — no existing specs to modify._

## Impact

- **`Sandbox/judge.py`**: Modified to wrap `main()` in try/except, ensuring JSON error output on stdout for any unhandled failure.
- **`Services/CompileService.cs`**: `JudgeCode` method signature changes from returning `string` (stdout only) to returning `(string Stdout, string Stderr)`. Error handling in `SubmitCode` is enhanced to include stderr context.
- **`Models/SubmissionResponse.cs`**: Already has an `Error` property — no model changes needed.
- **Docker infrastructure**: No changes needed — `ExecCmdFromContainerWithStderr` already exists in `DockerPool`.
