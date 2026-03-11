#!/usr/bin/env bash
# post-create.sh — runs after devcontainer is created
# Installs dependencies and sets up the development environment
set -euo pipefail

echo "==> Restoring .NET packages..."
if [ -f "md2.sln" ]; then
    dotnet restore
else
    echo "    No solution file yet. Run scaffold to create the project structure."
fi

echo "==> Installing Playwright browsers (Chromium only)..."
# Playwright for .NET installs its own browser binaries
# This pre-installs Chromium so first md2 run doesn't need to download it
if dotnet tool list -g | grep -q "playwright"; then
    echo "    Playwright CLI already installed"
else
    dotnet tool install --global Microsoft.Playwright.CLI 2>/dev/null || true
fi

# Install Chromium and its system dependencies
# --with-deps installs required system libraries (libnss, libglib, etc.)
npx --yes playwright install --with-deps chromium 2>/dev/null || {
    echo "    WARNING: Playwright browser install failed."
    echo "    Mermaid diagrams and math rendering will not work until Chromium is installed."
    echo "    Run: npx playwright install --with-deps chromium"
}

echo "==> Setting up dotnet tools..."
if [ -f ".config/dotnet-tools.json" ]; then
    dotnet tool restore
fi

echo "==> Development environment ready!"
echo "    - .NET SDK: $(dotnet --version)"
echo "    - Node.js:  $(node --version)"
echo "    - Chromium:  $(npx playwright --version 2>/dev/null || echo 'not installed')"
