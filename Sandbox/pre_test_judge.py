#!/usr/bin/env python3
"""
pre_test_judge.py — Single-testcase judge for pre-submission "Run Test" feature.

Usage:
    python3 pre_test_judge.py <solution_file> --input <b64_input> --expected <b64_expected> <time_limit_sec> <memory_limit_mb>

    --input and --expected must be base64-encoded UTF-8 strings. This avoids all shell-escaping
    issues with newlines, quotes, and special characters, and requires no extra temp files.

Unlike judge.py, this script receives the input and expected output directly as
base64-encoded command-line arguments instead of reading from .inp/.out files on disk.

Output: a single JSON object on stdout:
    {"status": "AC"|"WA"|"CE"|"RTE"|"TLE"|"MLE"|"ERROR",
     "actualOutput": "...",
     "time": 0.0,
     "memoryMb": 0.0,
     "error": null|"..."}
"""
import os
import sys
import subprocess
import resource
import json
import time
import argparse
import base64
from pathlib import Path

# Maximum allowed inline input size (64 KiB)
MAX_INPUT_SIZE = 65536


def set_limits(time_limit_sec, memory_limit_mb, ext):
    """Set resource limits (mirrors judge.py)."""
    resource.setrlimit(resource.RLIMIT_CPU, (time_limit_sec, time_limit_sec + 1))
    if ext != '.js':
        memory_bytes = memory_limit_mb * 1024 * 1024
        resource.setrlimit(resource.RLIMIT_AS, (memory_bytes, memory_bytes))


def extract_clean_error(stderr, ext):
    """Extract a safe, path-stripped error message (mirrors judge.py)."""
    if not stderr:
        return "Unknown Error"

    lines = [line.strip() for line in stderr.splitlines() if line.strip()]
    if not lines:
        return "Unknown Error"

    try:
        if ext == '.py':
            return lines[-1]
        elif ext == '.js':
            for line in lines:
                if "Error:" in line:
                    return line
            return lines[0]
        elif ext == '.cpp':
            for line in lines:
                if "error:" in line.lower():
                    parts = line.split("error:", 1)
                    if len(parts) > 1:
                        return f"Compile Error: {parts[1].strip()}"
                    return line
            return lines[0][:100]
        else:
            return lines[0][:100]
    except Exception:
        return "Error parsing logs"


def run_solution(solution_file, input_data, time_limit, memory_limit):
    """Execute the solution with the given inline input (mirrors judge.py)."""
    ext = Path(solution_file).suffix
    time_output = "/tmp/pre_test_time_stats.txt"

    try:
        if ext == '.py':
            exec_cmd = ['python3', solution_file]
        elif ext == '.pl':
            exec_cmd = ['perl', solution_file]
        elif ext == '.js':
            exec_cmd = ['node', '/scripts/runner.js', solution_file]
        elif ext == '.rb':
            exec_cmd = ['ruby', solution_file]
        elif ext == '.cpp':
            abs_solution_file = os.path.abspath(solution_file)
            exe_file = abs_solution_file.replace('.cpp', '.out')

            if not os.path.exists(exe_file) or \
               os.path.getmtime(abs_solution_file) > os.path.getmtime(exe_file):
                compile_result = subprocess.run(
                    ['g++', '-O2', '-std=c++17', abs_solution_file, '-o', exe_file],
                    capture_output=True, text=True, timeout=30
                )
                if compile_result.returncode != 0:
                    clean_msg = extract_clean_error(compile_result.stderr, ext)
                    return None, 0, 0, "CE", clean_msg

            exec_cmd = [exe_file]
        else:
            return None, 0, 0, "CE", f"Unsupported file extension: {ext}"

        cmd = ['/usr/bin/time', '-f', '%e %M', '-o', time_output] + exec_cmd
        start_time = time.time()

        result = subprocess.run(
            cmd, input=input_data, capture_output=True, text=True,
            timeout=time_limit + 1,
            preexec_fn=lambda: set_limits(time_limit, memory_limit, ext)
        )

        wall_time = time.time() - start_time
        measured_time = wall_time
        memory_kb = 0
        try:
            with open(time_output, 'r') as f:
                stats = f.read().strip().split()
                if len(stats) >= 2:
                    measured_time = float(stats[0])
                    memory_kb = int(stats[1])
        except Exception:
            pass

        elapsed = max(measured_time, wall_time)

        if result.returncode != 0:
            if result.returncode in [152, 158] or elapsed >= time_limit:
                return None, elapsed, memory_kb, "TLE", None
            if result.returncode == 137:
                return None, elapsed, memory_kb, "MLE", None

            clean_msg = extract_clean_error(result.stderr, ext)

            # Python/JS syntax errors at runtime count as CE
            if ext in ['.py', '.js'] and ("SyntaxError" in clean_msg or "IndentationError" in clean_msg):
                return None, elapsed, memory_kb, "CE", clean_msg
            else:
                return None, elapsed, memory_kb, "RTE", clean_msg

        if elapsed > time_limit:
            return None, elapsed, memory_kb, "TLE", None
        if (memory_kb / 1024) > memory_limit:
            return None, elapsed, memory_kb, "MLE", None

        return result.stdout, elapsed, memory_kb, "AC", None

    except subprocess.TimeoutExpired:
        return None, time_limit + 0.1, 0, "TLE", None
    except Exception as e:
        return None, 0, 0, "RTE", str(e)


def compare_output(actual, expected):
    """Strip trailing whitespace per line and compare (mirrors judge.py)."""
    actual_lines = [line.rstrip() for line in actual.strip().splitlines() if line.strip()]
    expected_lines = [line.rstrip() for line in expected.strip().splitlines() if line.strip()]
    return actual_lines == expected_lines


def main():
    parser = argparse.ArgumentParser(description="Single-testcase pre-submission judge")
    parser.add_argument("solution_file", help="Path to the solution source file")
    parser.add_argument("--input", dest="input_b64", required=True,
                        help="Base64-encoded input string to pass to the solution via stdin")
    parser.add_argument("--expected", dest="expected_b64", required=True,
                        help="Base64-encoded expected output string to compare against")
    parser.add_argument("time_limit", type=int, help="Time limit in seconds")
    parser.add_argument("memory_limit", type=int, help="Memory limit in MB")
    args = parser.parse_args()

    # Decode base64 arguments
    try:
        input_data = base64.b64decode(args.input_b64).decode('utf-8')
        expected_output = base64.b64decode(args.expected_b64).decode('utf-8')
    except Exception as e:
        print(json.dumps({"status": "ERROR", "actualOutput": None, "time": 0, "memoryMb": 0, "error": f"Failed to decode arguments: {e}"}))
        sys.exit(1)

    # Input size guard (task 1.3)
    if len(input_data) > MAX_INPUT_SIZE:
        print(json.dumps({"status": "ERROR", "actualOutput": None, "time": 0, "memoryMb": 0, "error": "Input too large"}))
        sys.exit(1)

    actual_output, elapsed, memory_kb, status, error_msg = run_solution(
        args.solution_file, input_data, args.time_limit, args.memory_limit
    )

    # Determine final verdict
    if status == "AC":
        if compare_output(actual_output, expected_output):
            final_status = "AC"
        else:
            final_status = "WA"
    else:
        final_status = status

    result = {
        "status": final_status,
        "actualOutput": actual_output if actual_output is not None else "",
        "time": round(elapsed, 3),
        "memoryMb": round(memory_kb / 1024, 2),
        "error": error_msg
    }

    print(json.dumps(result))
    sys.exit(0)


if __name__ == "__main__":
    main()
