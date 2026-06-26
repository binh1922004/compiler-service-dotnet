## Context

The CompilerService runs user-submitted code inside Docker containers via a pool (`DockerPool`). The `judge.py` Python script is executed inside the container to compile/run solutions against test cases and return JSON results to stdout.

**Current problem**: When `judge.py` encounters an unhandled exception (e.g., permission error, missing dependency, malformed arguments), the error goes to stderr and stdout is empty. The C# service's `JudgeCode` method calls `ExecCmdFromContainer` which discards stderr entirely, then tries to deserialize empty stdout — resulting in a `JsonException` caught by the generic `catch (Exception)` block that returns `Status = "IE"` with no error details.

**Key existing infrastructure**:
- `DockerPool.ExecCmdFromContainerWithStderr()` already exists and captures both stdout and stderr
- `SubmissionResponse.Error` property already exists for error messages
- The `GenerateTestCases` flow already uses `ExecCmdFromContainerWithStderr` successfully as a reference pattern

## Goals / Non-Goals

**Goals:**
- Ensure `judge.py` always outputs valid JSON to stdout, even when the script itself crashes
- Capture stderr from Docker container execution during judging (not just during test case generation)
- Propagate meaningful error messages from the sandbox back to the C# service and into `SubmissionResponse.Error`

**Non-Goals:**
- Changing the existing test case generation error handling (it already works well)
- Adding new status codes beyond the existing ones (AC, WA, TLE, MLE, RTE, CE, IE, ERROR)
- Modifying the Docker container infrastructure or `DockerPool` class

## Decisions

### 1. Wrap `judge.py`'s `main()` in a top-level try/except

**Decision**: Add a `try/except` around `main()` that catches any unhandled exception and prints a JSON error to stdout with `status: "ERROR"` and the exception details.

**Rationale**: This is the simplest, most robust approach — no matter what goes wrong inside the script, the C# service always gets parseable JSON. The `GenerateTestCases` flow doesn't need this because `test_case_generator.py` already has its own error handling.

**Alternative considered**: Relying purely on the C# side to handle empty/broken stdout + stderr. Rejected because it's better to fix the problem at the source — the script should always produce structured output.

### 2. Switch `JudgeCode` to use `ExecCmdFromContainerWithStderr`

**Decision**: Change `JudgeCode` to call `ExecCmdFromContainerWithStderr` and return both stdout and stderr. Update `SubmitCode` to include stderr context when judge output parsing fails.

**Rationale**: Follows the same pattern already used by `GenerateTestCases`. The stderr may contain useful compilation errors or Python tracebacks that should be logged and optionally included in error responses.

### 3. Map "ERROR" status from judge.py to "IE" in SubmissionResponse

**Decision**: When `judge.py` returns `status: "ERROR"`, map it to `Status = "IE"` (Internal Error) in the `SubmissionResponse` and populate the `Error` field with the error message from the JSON.

**Rationale**: "ERROR" from `judge.py` indicates an infrastructure/script problem, not a user code problem. "IE" is the existing convention for infrastructure errors in this codebase.

## Risks / Trade-offs

- **Risk**: The top-level try/except in `judge.py` might mask bugs during development → **Mitigation**: The error details (including traceback) are included in the JSON output, so they're still visible in logs.
- **Risk**: Including raw error messages in `SubmissionResponse.Error` could leak internal paths → **Mitigation**: The service already does this for test case generation errors; no change in security posture. If needed, error sanitization can be added later.
