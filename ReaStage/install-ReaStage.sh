#!/bin/bash
set -e

# Configuration
APP_NAME="ReaStage"
EXEC_NAME="ReaStage"
INSTALL_DIR="$HOME/.local/share/$APP_NAME"
DESKTOP_FILE="$HOME/.local/share/applications/reastage.desktop"
BIN_DIR="$HOME/.local/bin"
SYMLINK_PATH="$BIN_DIR/reastage"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Handle uninstallation flag
if [ "$1" = "--uninstall" ] || [ "$1" = "-u" ]; then
    echo "=== Uninstalling $APP_NAME ==="
    rm -rf "$INSTALL_DIR"
    rm -f "$DESKTOP_FILE"
    rm -f "$SYMLINK_PATH"
    update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
    echo "=== $APP_NAME has been successfully uninstalled. ==="
    exit 0
fi

echo "=== Installing $APP_NAME ==="

# Verify that the executable exists in current directory
if [ ! -f "$SCRIPT_DIR/$EXEC_NAME" ]; then
    echo "Error: Executable '$EXEC_NAME' not found in $SCRIPT_DIR"
    echo "Please run this script from the directory containing published files."
    exit 1
fi

# Create target directories
mkdir -p "$INSTALL_DIR"
mkdir -p "$HOME/.local/share/applications"
mkdir -p "$BIN_DIR"

# Copy published application files
echo "Copying files to $INSTALL_DIR..."
cp -r "$SCRIPT_DIR"/* "$INSTALL_DIR/"

# Make executable
chmod +x "$INSTALL_DIR/$EXEC_NAME"

# Create .desktop file for Application Menu
echo "Creating Application Menu shortcut..."
cat <<EOF > "$DESKTOP_FILE"
[Desktop Entry]
Type=Application
Name=ReaStage
Icon=$INSTALL_DIR/reastage.png
Comment=ReaStage DAW Controller
Exec=$INSTALL_DIR/$EXEC_NAME %f
Terminal=false
Categories=Audio;AudioVideo;
StartupWMClass=ReaStage
EOF

# Create symlink for terminal execution
echo "Creating command symlink in $BIN_DIR..."
ln -sf "$INSTALL_DIR/$EXEC_NAME" "$SYMLINK_PATH"

# Refresh desktop database
update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true

echo ""
echo "=== Installation complete! ==="
echo "Application installed to: $INSTALL_DIR"
echo "You can launch it from Application Menu or by running 'reastage' in terminal."
echo "To uninstall, run: ./install.sh --uninstall"