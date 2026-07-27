#!/usr/bin/env bash
# run-tests.sh — Build and test ElBruno.LocalLLMs on Linux/macOS/WSL
#
# Scheduling examples:
#
# Cron (daily at 2 AM):
# 0 2 * * * /bin/bash /path/to/scripts/run-tests.sh --skip-build >> /var/log/localllms-tests.log 2>&1
#
# Run with HF token:
# ./scripts/run-tests.sh --hf-token "hf_xxxx"
#
# Unit tests only (fast):
# ./scripts/run-tests.sh --skip-integration-tests
#
# Integration only, filter to lifecycle:
# ./scripts/run-tests.sh --skip-unit-tests --filter "FullyQualifiedName~LifecycleTests"
#
# Make executable: chmod +x scripts/run-tests.sh

set -euo pipefail

# ---------------------------------------------------------------------------
# Color output (only when stdout is a terminal)
# ---------------------------------------------------------------------------
if [ -t 1 ]; then
    RED='\033[0;31m'
    GREEN='\033[0;32m'
    YELLOW='\033[1;33m'
    CYAN='\033[0;36m'
    NC='\033[0m'
else
    RED=''; GREEN=''; YELLOW=''; CYAN=''; NC=''
fi

# ---------------------------------------------------------------------------
# Defaults
# ---------------------------------------------------------------------------
SKIP_BUILD=false
SKIP_UNIT=false
SKIP_INTEGRATION=false
FRAMEWORK="net8.0"
HF_TOKEN_VALUE=""
FILTER=""

# ---------------------------------------------------------------------------
# Argument parsing
# ---------------------------------------------------------------------------
show_help() {
    cat <<EOF
Usage: $(basename "$0") [options]

Options:
  --skip-build, -B              Skip the dotnet build step
  --no-build                    Alias for --skip-build
  --skip-unit-tests, -U         Skip unit tests
  --skip-integration-tests, -I  Skip integration tests
  --framework <value>           Target framework (default: net8.0)
  --hf-token <value>            Set HF_TOKEN for private HuggingFace repos
  --filter <value>              xUnit filter string for integration tests
  --help, -h                    Show this help message
EOF
    exit 0
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-build|-B|--no-build)
            SKIP_BUILD=true
            shift ;;
        --skip-unit-tests|-U)
            SKIP_UNIT=true
            shift ;;
        --skip-integration-tests|-I)
            SKIP_INTEGRATION=true
            shift ;;
        --framework)
            FRAMEWORK="${2:?'--framework requires a value'}"
            shift 2 ;;
        --hf-token)
            HF_TOKEN_VALUE="${2:?'--hf-token requires a value'}"
            shift 2 ;;
        --filter)
            FILTER="${2:?'--filter requires a value'}"
            shift 2 ;;
        --help|-h)
            show_help ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}" >&2
            show_help ;;
    esac
done

# ---------------------------------------------------------------------------
# Resolve repo root (walk up from scripts/ until .slnx is found)
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT=""
SEARCH_DIR="$SCRIPT_DIR"

while [[ "$SEARCH_DIR" != "/" ]]; do
    if [[ -f "$SEARCH_DIR/ElBruno.LocalLLMs.slnx" ]]; then
        REPO_ROOT="$SEARCH_DIR"
        break
    fi
    SEARCH_DIR="$(dirname "$SEARCH_DIR")"
done

if [[ -z "$REPO_ROOT" ]]; then
    echo -e "${RED}ERROR: Could not find ElBruno.LocalLLMs.slnx — repo root not found.${NC}" >&2
    exit 99
fi

# ---------------------------------------------------------------------------
# Project paths
# ---------------------------------------------------------------------------
SOLUTION="$REPO_ROOT/ElBruno.LocalLLMs.slnx"
UNIT_TEST_PROJ="$REPO_ROOT/src/tests/ElBruno.LocalLLMs.Tests/ElBruno.LocalLLMs.Tests.csproj"
INT_TEST_PROJ="$REPO_ROOT/src/tests/ElBruno.LocalLLMs.IntegrationTests/ElBruno.LocalLLMs.IntegrationTests.csproj"

# ---------------------------------------------------------------------------
# Banner
# ---------------------------------------------------------------------------
START_SECONDS=$SECONDS
echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN} $(basename "$0")${NC}"
echo -e "${CYAN} $(date '+%Y-%m-%d %H:%M:%S')${NC}"
echo -e "${CYAN} Repo root: $REPO_ROOT${NC}"
echo -e "${CYAN} Framework: $FRAMEWORK${NC}"
echo -e "${CYAN}========================================${NC}"

# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------
if [[ "$SKIP_BUILD" == "false" ]]; then
    echo -e "\n${YELLOW}>>> Build${NC}"
    if ! dotnet build "$SOLUTION" --framework "$FRAMEWORK" -p:TargetFrameworks="$FRAMEWORK"; then
        echo -e "${RED}ERROR: Build failed.${NC}" >&2
        exit 1
    fi
    echo -e "${GREEN}Build succeeded.${NC}"
else
    echo -e "${YELLOW}>>> Build skipped.${NC}"
fi

# ---------------------------------------------------------------------------
# Unit tests
# ---------------------------------------------------------------------------
if [[ "$SKIP_UNIT" == "false" ]]; then
    echo -e "\n${YELLOW}>>> Unit tests${NC}"
    if ! dotnet test "$UNIT_TEST_PROJ" \
            --framework "$FRAMEWORK" \
            --no-build \
            --logger "console;verbosity=minimal"; then
        echo -e "${RED}ERROR: Unit tests failed.${NC}" >&2
        exit 2
    fi
    echo -e "${GREEN}Unit tests passed.${NC}"
else
    echo -e "${YELLOW}>>> Unit tests skipped.${NC}"
fi

# ---------------------------------------------------------------------------
# Integration tests
# ---------------------------------------------------------------------------
if [[ "$SKIP_INTEGRATION" == "false" ]]; then
    echo -e "\n${YELLOW}>>> Integration tests${NC}"

    export RUN_INTEGRATION_TESTS=true

    if [[ -n "$HF_TOKEN_VALUE" ]]; then
        export HF_TOKEN="$HF_TOKEN_VALUE"
        echo -e "${CYAN}HF_TOKEN set.${NC}"
    fi

    # Create a reference file so we can find newly written results afterward
    REF_FILE="$REPO_ROOT/.run-tests-ref-$$"
    touch "$REF_FILE"

    INT_EXIT=0
    if [[ -n "$FILTER" ]]; then
        dotnet test "$INT_TEST_PROJ" \
            --framework "$FRAMEWORK" \
            --no-build \
            --logger "console;verbosity=minimal" \
            --filter "$FILTER" || INT_EXIT=$?
    else
        dotnet test "$INT_TEST_PROJ" \
            --framework "$FRAMEWORK" \
            --no-build \
            --logger "console;verbosity=minimal" || INT_EXIT=$?
    fi

    # Show latest results file written by TestRunReporter (if any)
    RESULTS_DIR="$REPO_ROOT/docs/tests"
    if [[ -d "$RESULTS_DIR" ]]; then
        LATEST_RESULTS="$(find "$RESULTS_DIR" -name "*-run-results.md" -newer "$REF_FILE" 2>/dev/null | sort | tail -1)"
        if [[ -n "$LATEST_RESULTS" ]]; then
            echo -e "\n${CYAN}--- Test run results: $LATEST_RESULTS ---${NC}"
            cat "$LATEST_RESULTS"
        fi
    fi

    rm -f "$REF_FILE"

    if [[ $INT_EXIT -ne 0 ]]; then
        echo -e "${RED}ERROR: Integration tests failed (exit $INT_EXIT).${NC}" >&2
        exit 3
    fi
    echo -e "${GREEN}Integration tests passed.${NC}"
else
    echo -e "${YELLOW}>>> Integration tests skipped.${NC}"
fi

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
ELAPSED=$(( SECONDS - START_SECONDS ))
echo -e "\n${GREEN}========================================${NC}"
echo -e "${GREEN} All checks passed in ${ELAPSED}s${NC}"
echo -e "${GREEN}========================================${NC}"
exit 0
