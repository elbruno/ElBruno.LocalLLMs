# install_windows.ps1 — Set up the fine-tuning environment on Windows
# Requires: Python 3.10+ (miniconda/venv), NVIDIA GPU with CUDA drivers
#
# Usage:
#   .\install_windows.ps1

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ElBruno.LocalLLMs — Windows Setup"
Write-Host "  Fine-tuning environment installer"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check Python
try {
    $pyVersion = python --version 2>&1
    Write-Host "[OK] $pyVersion" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Python not found. Install Python 3.10+ first." -ForegroundColor Red
    exit 1
}

# Check pip
try {
    $pipVersion = pip --version 2>&1
    Write-Host "[OK] pip available" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] pip not found." -ForegroundColor Red
    exit 1
}

# Check NVIDIA GPU
try {
    $nvsmi = nvidia-smi --query-gpu=name,memory.total --format=csv,noheader 2>&1
    Write-Host "[OK] GPU: $nvsmi" -ForegroundColor Green
} catch {
    Write-Host "[WARN] nvidia-smi not found. CUDA training will not work without an NVIDIA GPU." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Step 1/4: Installing PyTorch with CUDA 12.4..." -ForegroundColor Cyan
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] PyTorch installation failed." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] PyTorch installed." -ForegroundColor Green

Write-Host ""
Write-Host "Step 2/4: Installing training dependencies..." -ForegroundColor Cyan
pip install transformers datasets trl peft accelerate sentencepiece protobuf scipy
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Training dependencies installation failed." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Training dependencies installed." -ForegroundColor Green

Write-Host ""
Write-Host "Step 3/4: Installing ONNX dependencies..." -ForegroundColor Cyan
pip install onnxruntime-genai onnx onnx-ir
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARN] ONNX dependencies installation failed. ONNX conversion may not work." -ForegroundColor Yellow
    Write-Host "       You can still train and merge models. Install ONNX deps later if needed." -ForegroundColor Yellow
}
else {
    Write-Host "[OK] ONNX dependencies installed." -ForegroundColor Green
}

Write-Host ""
Write-Host "Step 4/4: Installing HuggingFace Hub..." -ForegroundColor Cyan
pip install huggingface-hub
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] huggingface-hub installation failed." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] HuggingFace Hub installed." -ForegroundColor Green

# Verify installation
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Verifying installation..."
Write-Host "========================================" -ForegroundColor Cyan

python -c @"
import sys
print(f'Python {sys.version}')

checks = []
try:
    import torch
    gpu = torch.cuda.is_available()
    name = torch.cuda.get_device_name(0) if gpu else 'N/A'
    checks.append(f'  torch {torch.__version__} (CUDA: {gpu}, GPU: {name})')
except Exception as e:
    checks.append(f'  torch: FAILED ({e})')

for pkg in ['transformers', 'datasets', 'trl', 'peft', 'accelerate']:
    try:
        mod = __import__(pkg)
        v = getattr(mod, '__version__', '?')
        checks.append(f'  {pkg} {v}')
    except Exception as e:
        checks.append(f'  {pkg}: FAILED ({e})')

try:
    import onnxruntime_genai
    checks.append(f'  onnxruntime-genai OK')
except Exception as e:
    checks.append(f'  onnxruntime-genai: NOT AVAILABLE ({e})')

try:
    import huggingface_hub
    checks.append(f'  huggingface-hub {huggingface_hub.__version__}')
except Exception as e:
    checks.append(f'  huggingface-hub: FAILED ({e})')

for c in checks:
    print(c)
"@

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Setup complete!"
Write-Host "  Run training with:"
Write-Host "    python train_windows.py --variant ToolCalling --skip-upload"
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
