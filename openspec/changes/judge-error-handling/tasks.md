## 1. Python Script Error Handling

- [ ] 1.1 Add top-level try/except wrapper around `main()` in `Sandbox/judge.py` that catches all unhandled exceptions and outputs a JSON error object to stdout with `status: "ERROR"`, `error` (exception message), and `traceback` (full traceback string)
- [ ] 1.2 Verify that normal `judge.py` execution (AC, WA, TLE, MLE, RTE, CE) is unaffected by the try/except wrapper

## 2. C# Service Error Capture

- [ ] 2.1 Update `CompileService.JudgeCode` to call `ExecCmdFromContainerWithStderr` instead of `ExecCmdFromContainer`, returning both stdout and stderr
- [ ] 2.2 Update `CompileService.SubmitCode` to handle the new `(stdout, stderr)` return from `JudgeCode` — pass both to JSON deserialization and error handling logic

## 3. Error Response Mapping

- [x] 3.1 In `CompileService.SubmitCode`, handle the case where `judge.py` returns `status: "ERROR"` — map to `SubmissionResponse` with `Status = "IE"` and populate `Error` field with the error message from the JSON
- [x] 3.2 In `CompileService.SubmitCode`, handle the case where JSON deserialization fails (empty or malformed stdout) — return `SubmissionResponse` with `Status = "IE"` and `Error` containing both raw stdout and stderr for debugging

## 4. Testing & Verification

- [x] 4.1 Update existing unit tests in `CompilerServiceTests.cs` to account for the new `JudgeCode` signature (if mocks need updating)
- [x] 4.2 Verify the project builds successfully with `dotnet build`
