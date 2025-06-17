#!/usr/bin/env python3
"""
Test CUDA availability and GPU detection
"""

import torch

print("=== CUDA Test ===")
print(f"PyTorch version: {torch.__version__}")
print(f"CUDA available: {torch.cuda.is_available()}")

if torch.cuda.is_available():
    print(f"CUDA version: {torch.version.cuda}")
    print(f"GPU count: {torch.cuda.device_count()}")
    
    for i in range(torch.cuda.device_count()):
        gpu_props = torch.cuda.get_device_properties(i)
        print(f"GPU {i}: {gpu_props.name}")
        print(f"  Memory: {gpu_props.total_memory // (1024**3)} GB")
        print(f"  Compute Capability: {gpu_props.major}.{gpu_props.minor}")
        
    # Test tensor creation on GPU
    try:
        x = torch.tensor([1.0, 2.0, 3.0]).cuda()
        print(f"GPU tensor test: SUCCESS - {x}")
    except Exception as e:
        print(f"GPU tensor test: FAILED - {e}")
else:
    print("CUDA not available - reasons could be:")
    print("1. PyTorch not installed with CUDA support")
    print("2. NVIDIA drivers not installed")
    print("3. CUDA toolkit not installed")
    print("4. GPU not detected")
