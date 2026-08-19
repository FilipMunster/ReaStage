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
apt install -y \
    ca-certificates \
    curl \
    zlib1g \
    libssl-dev \
    libicu-dev \
    fontconfig \
    libfontconfig1 \
    libfreetype6 \
    libgl1 \
    libegl1 \
    libgl1-mesa-dri \
    libx11-6 \
    libx11-xcb1 \
    libxcb1 \
    libxcursor1 \
    libxi6 \
    libxrandr2 \
    libxinerama1 \
    libxext6 \
    libxrender1

echo "=== Setup complete! All dependencies installed successfully. ==="