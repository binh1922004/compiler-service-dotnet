import os
import sys
import json
import shutil
import subprocess
import glob
import zipfile
import argparse
import traceback

# Maximum characters of stderr to include in error messages
MAX_STDERR_LEN = 2000


def _log(msg):
    """Print progress messages to stderr so stdout stays clean for JSON."""
    print(msg, file=sys.stderr, flush=True)


class BNOJTestCaseBuilder:
    def __init__(self, generator_cmd, solution_cmd, target_folder, time_limit_sec=5.0):
        self.generator_cmd = generator_cmd
        self.solution_cmd = solution_cmd
        self.target_folder = os.path.abspath(target_folder)

        self.testcases_dir = os.path.join(self.target_folder, "testcases")
        self.zip_output = os.path.join(self.target_folder, "testcases.zip")
        self.time_limit = time_limit_sec

    def setup_workspace(self):
        """Cleans and prepares the testcase directory."""
        if os.path.exists(self.testcases_dir):
            shutil.rmtree(self.testcases_dir)
        os.makedirs(self.testcases_dir, exist_ok=True)
        _log(f"[*] Workspace ready: {self.testcases_dir}")

    def run_input_generator(self):
        """Runs the user's script, captures console output, and parses it into subfolders."""
        _log(f"[*] Running input generator and capturing console output: {self.generator_cmd}")
        try:
            result = subprocess.run(
                self.generator_cmd,
                cwd=self.target_folder,
                shell=True,
                capture_output=True,
                text=True,
                check=True
            )

            output_content = result.stdout
            raw_testcases = output_content.split("---TEST_BOUNDARY---")

            test_id = 1
            for tc_data in raw_testcases:
                tc_data = tc_data.strip()

                if tc_data:
                    # 1. Create the specific subfolder: test_01, test_02, etc.
                    tc_folder_name = f"test_{test_id:02d}"
                    tc_folder_path = os.path.join(self.testcases_dir, tc_folder_name)
                    os.makedirs(tc_folder_path, exist_ok=True)

                    # 2. Write the input data to test.inp inside that subfolder
                    inp_path = os.path.join(tc_folder_path, "test.inp")
                    with open(inp_path, 'w') as f:
                        f.write(tc_data + "\n")

                    test_id += 1

            total = test_id - 1
            if total == 0:
                raise RuntimeError(
                    "Input generator produced no test cases. "
                    f"stdout was empty or contained no '---TEST_BOUNDARY---' delimiters.\n"
                    f"Generator stdout (first 500 chars): {output_content[:500]!r}"
                )

            _log(f"[*] Input generation successful. Parsed {total} testcases into subfolders.")
            return total

        except subprocess.CalledProcessError as e:
            stderr_text = (e.stderr or "").strip()[:MAX_STDERR_LEN]
            stdout_text = (e.stdout or "").strip()[:500]
            detail = (
                f"Input generator failed with exit code {e.returncode}.\n"
                f"Command: {self.generator_cmd}\n"
                f"stderr:\n{stderr_text}"
            )
            if stdout_text:
                detail += f"\nstdout (first 500 chars):\n{stdout_text}"
            _log(f"[ERROR] Generator crashed:\n{detail}")
            raise RuntimeError(detail)

    def generate_outputs(self):
        """Finds all test.inp files in subfolders and pipes them through the solution code."""
        _log(f"[*] Generating outputs using: {self.solution_cmd}")

        # Search recursively for all test.inp files inside the subfolders
        inp_files = glob.glob(os.path.join(self.testcases_dir, "**", "test.inp"), recursive=True)

        if not inp_files:
            raise FileNotFoundError("No 'test.inp' files were found in the subfolders.")

        success_count = 0
        for inp_path in sorted(inp_files):
            # Write test.out in the exact same directory as the test.inp
            tc_folder = os.path.dirname(inp_path)
            tc_name = os.path.basename(tc_folder)
            out_path = os.path.join(tc_folder, "test.out")

            # Read input content for error reporting
            try:
                with open(inp_path, 'r') as f:
                    input_preview = f.read(300)
            except Exception:
                input_preview = "<could not read input>"

            try:
                with open(inp_path, 'r') as fin:
                    proc = subprocess.run(
                        self.solution_cmd,
                        stdin=fin,
                        capture_output=True,
                        text=True,
                        cwd=self.target_folder,
                        shell=True,
                        timeout=self.time_limit,
                        check=True
                    )
                # Write stdout to the output file
                with open(out_path, 'w') as fout:
                    fout.write(proc.stdout)
                success_count += 1

            except subprocess.TimeoutExpired:
                detail = (
                    f"Solution exceeded time limit ({self.time_limit}s) on {tc_name}.\n"
                    f"Command: {self.solution_cmd}\n"
                    f"Input preview:\n{input_preview}"
                )
                _log(f"[ERROR] {detail}")
                raise RuntimeError(detail)

            except subprocess.CalledProcessError as e:
                stderr_text = (e.stderr or "").strip()[:MAX_STDERR_LEN]
                stdout_text = (e.stdout or "").strip()[:500]
                detail = (
                    f"Solution failed with exit code {e.returncode} on {tc_name}.\n"
                    f"Command: {self.solution_cmd}\n"
                    f"stderr:\n{stderr_text}"
                )
                if stdout_text:
                    detail += f"\nstdout (first 500 chars):\n{stdout_text}"
                detail += f"\nInput preview:\n{input_preview}"
                _log(f"[ERROR] {detail}")
                raise RuntimeError(detail)

        _log(f"[*] Successfully generated {success_count}/{len(inp_files)} outputs.")

    def package_testcases(self):
        """Zips the testcases folder maintaining the nested structure."""
        _log(f"[*] Packaging testcases to {self.zip_output}...")
        with zipfile.ZipFile(self.zip_output, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for root, dirs, files in os.walk(self.testcases_dir):
                for file in files:
                    file_path = os.path.join(root, file)
                    # arcname preserves the test_01/test.inp structure inside the zip
                    arcname = os.path.relpath(file_path, self.testcases_dir)
                    zipf.write(file_path, arcname)

        _log(f"[*] Zip successfully created.")
        return self.zip_output

    def count_testcases(self):
        """Counts the number of test case subfolders."""
        return len(glob.glob(os.path.join(self.testcases_dir, "test_*")))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="BNOJ Testcase Generator Worker")
    parser.add_argument("--gen", required=True, help="Command to run the input generator")
    parser.add_argument("--sol", required=True, help="Command to run the solution")
    parser.add_argument("--outdir", required=True, help="Folder to store the testcases and zip file")
    parser.add_argument("--timeout", type=float, default=5.0, help="Time limit per testcase in seconds")

    args = parser.parse_args()

    builder = BNOJTestCaseBuilder(
        generator_cmd=args.gen,
        solution_cmd=args.sol,
        target_folder=args.outdir,
        time_limit_sec=args.timeout
    )

    try:
        builder.setup_workspace()
        builder.run_input_generator()
        builder.generate_outputs()
        zip_path = builder.package_testcases()
        test_count = builder.count_testcases()

        # Single JSON line on stdout for the C# caller to parse
        print(json.dumps({
            "status": "success",
            "zipPath": zip_path,
            "testCount": test_count
        }))

    except Exception as e:
        # Determine which phase failed based on the error context
        error_msg = str(e)
        if "Input generator" in error_msg or "no test cases" in error_msg.lower():
            phase = "input_generation"
        elif "Solution" in error_msg or "time limit" in error_msg.lower():
            phase = "output_generation"
        else:
            phase = "unknown"

        print(json.dumps({
            "status": "failed",
            "phase": phase,
            "error": error_msg,
            "errorType": type(e).__name__,
            "traceback": traceback.format_exc()
        }))
        sys.exit(1)