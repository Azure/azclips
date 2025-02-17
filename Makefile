# Detect OS
ifeq ($(OS),Windows_NT)
	DETECTED_OS := Windows
	EXE_EXT := .exe
	# Use PowerShell commands for Windows
	MKDIR_CMD := powershell mkdir -Force
else
	DETECTED_OS := $(shell uname -s)
	EXE_EXT :=
	# Use Unix commands for Linux/MacOS
	MKDIR_CMD := mkdir -p
endif

AZLITE_DIR=./azlite
OUTPUT_DIR=./build
MODULES_DIR=./modules

# Get just the module names without the full path
PLUGIN_MODULES=$(patsubst $(MODULES_DIR)/%/plugin/main.go,%,$(wildcard $(MODULES_DIR)/*/plugin/main.go))

# Create output directories if they don't exist
$(OUTPUT_DIR):
	$(MKDIR_CMD) "$(OUTPUT_DIR)"

$(OUTPUT_DIR)/plugins: | $(OUTPUT_DIR)
	$(MKDIR_CMD) "$(OUTPUT_DIR)/plugins"

build-plugins: clean-processes $(OUTPUT_DIR)/plugins $(PLUGIN_MODULES)

$(PLUGIN_MODULES):
	go build -o "$(OUTPUT_DIR)/plugins/$@.azm" "$(MODULES_DIR)/$@/plugin/main.go"

build-azlite: $(AZLITE_DIR)
	go build -o "$(OUTPUT_DIR)/azlite$(EXE_EXT)" "$(AZLITE_DIR)/main.go"

build: clean-processes build-proto build-plugins build-azlite

build-proto:
	buf generate

run: build
ifeq ($(DETECTED_OS),Windows)
	cd $(OUTPUT_DIR) && powershell -Command "$$env:AZURE_CLI_PLUGIN_PATH='$(OUTPUT_DIR)/plugins'; ./azlite$(EXE_EXT)"
else
	cd $(OUTPUT_DIR) && AZURE_CLI_PLUGIN_PATH="$(OUTPUT_DIR)/plugins" ./azlite$(EXE_EXT)
endif

clean-processes:
ifeq ($(DETECTED_OS),Windows)
	@powershell -Command "Get-Process | Where-Object {$$_.Name -like '*.azm'} | Stop-Process -Force -ErrorAction SilentlyContinue"
else
	@pkill -f "azlite|.*\.azm" || true
endif

clean:
	go clean
	rm -rf $(OUTPUT_DIR)

.PHONY: build build-plugins build-azlite clean
