#!/bin/bash
set -e

# Ensure the script is run with root privileges
if [ "$EUID" -ne 0 ]; then
  echo "This script must be run as root (use sudo)."
  exit 1
fi

echo "=== Updating package list ==="
apt update

echo "=== Installing .NET runtime and Avalonia UI dependencies ==="

# Only what the binaries actually bind. Anything they pull in as its own dependency
# (libxcb1, libfreetype6, libxrender1) is deliberately not listed.
#
#   ca-certificates, zlib1g   .NET runtime
#   libssl-dev                libSystem.Security.Cryptography.Native.OpenSsl.so
#   libicu-dev                libSystem.Globalization.Native.so
#   libfontconfig1, libgl1    libSkiaSharp.so - libGL is needed even though the app
#                             renders in software, Skia links it either way
#   libX* , libsm6, libice6   Avalonia's X11 backend
apt install -y \
    ca-certificates \
    zlib1g \
    libssl-dev \
    libicu-dev \
    libfontconfig1 \
    libgl1 \
    libx11-6 \
    libxcursor1 \
    libxext6 \
    libxi6 \
    libxrandr2 \
    libsm6 \
    libice6

echo "=== Setup complete! All dependencies installed successfully. ==="