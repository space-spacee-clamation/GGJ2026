import os
from pathlib import Path

def simple_merge(source_dir, output_file="merged.txt"):
    """简化版的合并函数"""
    source_path = Path(source_dir)
    cs_files = source_path.rglob("*.cs")
    
    with open(output_file, 'w', encoding='utf-8') as outfile:
        for cs_file in cs_files:
            outfile.write(f"\n// === {cs_file} ===\n")
            with open(cs_file, 'r', encoding='utf-8') as infile:
                outfile.write(infile.read() + "\n")

# 使用示例
if __name__ == "__main__":
    simple_merge(".", "code.txt")