#!/usr/bin/env python3
import os
import sys
import subprocess
import resource
import json
import time
from pathlib import Path

PROBLEMS_BASE_DIR = "/problems"

def normalize_problem_id(problem_input):
    """Normalize problem_id từ input"""
    problem_input = problem_input.strip('/')
    if problem_input.startswith('problems/'):
        problem_input = problem_input[9:]
    if '/' in problem_input:
        problem_input = os.path.basename(problem_input)
    return problem_input

def find_testcases(problem_id):
    """Tìm tất cả testcase cho problem_id"""
    problem_dir = Path(PROBLEMS_BASE_DIR) / problem_id
    
    if not problem_dir.exists():
        return None, f"Problem '{problem_id}' không tồn tại tại {problem_dir}"
    
    testcases = []
    
    # Lặp qua tất cả thư mục con trong bài tập (mỗi thư mục con là một testcase)
    for tc_dir in problem_dir.iterdir():
        if not tc_dir.is_dir():
            continue
            
        inp_files = list(tc_dir.glob("*.inp"))
        out_files = list(tc_dir.glob("*.out"))
        
        # Một thư mục testcase hợp lệ cần có đúng 1 file .inp và 1 file .out
        if len(inp_files) == 1 and len(out_files) == 1:
            testcases.append({
                'num': tc_dir.name, # Lấy tên subfolder làm định danh số/tên testcase
                'inp': str(inp_files[0]),
                'out': str(out_files[0])
            })
            
    # Sắp xếp testcase theo tên folder (có xử lý số thông minh: VD test2 xếp trước test10)
    def sort_key(tc):
        import re
        parts = re.split(r'(\d+)', tc['num'])
        return [int(p) if p.isdigit() else p.lower() for p in parts]
        
    testcases.sort(key=sort_key)
            
    if not testcases:
        return None, f"Không tìm thấy thư mục testcase nào hợp lệ trong {problem_dir}"
    
    return testcases, None

def set_limits(time_limit_sec, memory_limit_mb):
    """Thiết lập giới hạn tài nguyên"""
    resource.setrlimit(resource.RLIMIT_CPU, (time_limit_sec, time_limit_sec + 1))
    memory_bytes = memory_limit_mb * 1024 * 1024
    resource.setrlimit(resource.RLIMIT_AS, (memory_bytes, memory_bytes))

def run_solution(solution_file, input_data, time_limit, memory_limit):
    ext = Path(solution_file).suffix
    time_output = "/tmp/time_stats.txt"
    
    try:
        if ext == '.py':
            exec_cmd = ['python3', solution_file]
        elif ext == '.cpp':
            exe_file = solution_file.replace('.cpp', '.out')
            if not os.path.exists(exe_file) or \
               os.path.getmtime(solution_file) > os.path.getmtime(exe_file):
                compile_result = subprocess.run(
                    ['g++', '-O2', '-std=c++17', solution_file, '-o', exe_file],
                    capture_output=True, text=True, timeout=30
                )
                if compile_result.returncode != 0:
                    return None, 0, 0, "CE"
            exec_cmd = [exe_file]
        else:
            return None, 0, 0, "CE"
        
        cmd = ['/usr/bin/time', '-f', '%e %M', '-o', time_output] + exec_cmd
        start_time = time.time()
        
        result = subprocess.run(
            cmd, input=input_data, capture_output=True, text=True,
            timeout=time_limit + 1,
            preexec_fn=lambda: set_limits(time_limit, memory_limit)
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
        except: pass

        elapsed = max(measured_time, wall_time)

        if result.returncode != 0:
            if result.returncode in [152, 158] or elapsed >= time_limit:
                return None, elapsed, memory_kb, "TLE"
            if result.returncode == 137:
                return None, elapsed, memory_kb, "MLE"
            return None, elapsed, memory_kb, "RTE"
        
        if elapsed > time_limit: return None, elapsed, memory_kb, "TLE"
        if (memory_kb / 1024) > memory_limit: return None, elapsed, memory_kb, "MLE"
        
        return result.stdout, elapsed, memory_kb, None
        
    except subprocess.TimeoutExpired:
        return None, time_limit + 0.1, 0, "TLE"
    except Exception:
        return None, 0, 0, "RTE"

def compare_output(actual, expected):
    actual_lines = [line.rstrip() for line in actual.strip().splitlines() if line.strip()]
    expected_lines = [line.rstrip() for line in expected.strip().splitlines() if line.strip()]
    return actual_lines == expected_lines

def main():
    args = sys.argv[1:]
    is_icpc = False
    if "--icpc" in args:
        is_icpc = True
        args.remove("--icpc")

    if len(args) < 4:
        print(json.dumps({"status": "ERROR", "error": "Usage: judge.py [--icpc] <file> <prob_id> <time> <mem>"}, indent=2))
        sys.exit(1)
    
    solution_file, problem_input, time_limit, memory_limit = args[0], args[1], int(args[2]), int(args[3])
    problem_id = normalize_problem_id(problem_input)
    
    testcases, error = find_testcases(problem_id)
    if error or not testcases:
        print(json.dumps({"status": "ERROR", "error": error or "No testcasesfound"}, indent=2))
        sys.exit(1)

    passed, max_time, max_memory = 0, 0, 0
    test_results = []
    overall_status = "AC"

    for tc in testcases:
        with open(tc['inp'], 'r') as f: input_data = f.read()
        with open(tc['out'], 'r') as f: expected_output = f.read()
        
        actual_output, elapsed, memory_kb, error = run_solution(solution_file, input_data, time_limit, memory_limit)
        
        max_time = max(max_time, elapsed)
        max_memory = max(max_memory, memory_kb)
        
        if error: status = error
        elif compare_output(actual_output, expected_output): status = "AC"
        else: status = "WA"

        res = {"test_id": tc['num'], "status": status, "time": round(elapsed, 3), "memory_mb": round(memory_kb/1024, 2)}
        test_results.append(res)

        if status == "AC":
            passed += 1
        else:
            if overall_status == "AC": overall_status = status
            if is_icpc: break  # Chế độ ICPC: dừng ngay khi sai

    result = {
        "status": overall_status,
        "passed": passed,
        "total": len(testcases),
        "max_time": round(max_time, 3),
        "max_memory_mb": round(max_memory / 1024, 2),
#         "tests": test_results
    }
    
    print(json.dumps(result, indent=2))
    sys.exit(0)

if __name__ == "__main__":
    main()